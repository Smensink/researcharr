using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Http;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MetadataSource.CrossRef
{
    public class CrossRefProxy : ICrossRefProxy
    {
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly Logger _logger;
        private readonly IHttpRequestBuilderFactory _requestBuilder;
        private readonly CrossRefSettings _settings;

        public CrossRefProxy(ICachedHttpResponseService cachedHttpClient, CrossRefSettings settings, Logger logger)
        {
            _cachedHttpClient = cachedHttpClient;
            _settings = settings;
            _logger = logger;

            _requestBuilder = new HttpRequestBuilder(_settings.BaseUrl)
                .SetHeader("User-Agent", _settings.UserAgent)
                .KeepAlive()
                .CreateFactory();
        }

        public Book GetBookByDoi(string doi, bool useCache = true)
        {
            var normalizedDoi = DoiUtility.Normalize(doi);
            if (string.IsNullOrWhiteSpace(normalizedDoi))
            {
                return null;
            }

            // Additional validation: ensure DOI doesn't contain invalid characters or is too long
            // DOIs should be reasonable length (typically < 200 chars total)
            // and should not contain spaces or obvious word boundaries
            if (normalizedDoi.Length > 200)
            {
                _logger.Debug("DOI too long, likely malformed: {0} (length: {1})", normalizedDoi.Substring(0, 100) + "...", normalizedDoi.Length);
                return null;
            }

            // Check for word-like patterns that suggest concatenated text
            // Pattern: lowercase letters that form words (3+ consecutive lowercase letters)
            if (Regex.IsMatch(normalizedDoi, @"[a-z]{3,}", RegexOptions.IgnoreCase))
            {
                // This might be a false positive if the DOI suffix legitimately contains words
                // But if it's very long, it's likely concatenated text
                var slashIndex = normalizedDoi.IndexOf('/');
                if (slashIndex > 0 && normalizedDoi.Length - slashIndex > 50)
                {
                    _logger.Debug("DOI contains word-like patterns and is long, likely malformed: {0}", normalizedDoi.Substring(0, Math.Min(100, normalizedDoi.Length)));
                    return null;
                }
            }

            try
            {
                var request = _requestBuilder.Create()
                    .Resource($"works/{normalizedDoi}")
                    .AddQueryParam("mailto", _settings.MailTo) // Polite pool for faster responses
                    .Build();

                var response = ExecuteRequest<CrossRefWorkResponse>(request, useCache);

                if (response?.Message != null)
                {
                    return MapBook(response.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error fetching book from CrossRef for DOI: {0}", normalizedDoi);
            }

            return null;
        }

        public List<Book> SearchBooks(string title, string author = null, int maxResults = 20, bool useCache = true)
        {
            var results = new List<Book>();

            try
            {
                var query = BuildSearchQuery(title, author);

                var request = _requestBuilder.Create()
                    .Resource("works")
                    .AddQueryParam("query", query)
                    .AddQueryParam("rows", maxResults.ToString())
                    .AddQueryParam("mailto", _settings.MailTo)
                    .Build();

                // CrossRef returns a different structure for search results
                var response = ExecuteRequest<dynamic>(request, useCache);

                if (response?.message?.items != null)
                {
                    foreach (var item in response.message.items)
                    {
                        try
                {
                            var work = Json.Deserialize<CrossRefWork>(item.ToString());
                            var book = MapBook(work);
                            if (book != null)
                            {
                                results.Add(book);
                            }
                        }
                        catch
                        {
                            // Skip malformed items
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error searching CrossRef for title: {0}, author: {1}", title, author);
            }

            return results;
        }

        public Author GetAuthorByOrcid(string orcid, bool useCache = true)
        {
            // CrossRef doesn't have a direct author endpoint, but we could search for works by ORCID
            // This is a simplified implementation
            return null;
        }

        private Book MapBook(CrossRefWork work)
        {
            if (work == null)
            {
                return null;
            }

            var book = new Book
            {
                Title = work.Title?.FirstOrDefault() ?? "Unknown Title",
                ReleaseDate = GetPublishedDate(work),
                Ratings = new Ratings { Value = 0 },
                Links = new List<Links>(),
                AnyEditionOk = true
            };

            // Add DOI link
            if (!string.IsNullOrWhiteSpace(work.Doi))
            {
                book.Links = new List<Links>
                {
                    new Links
                    {
                        Name = "doi",
                        Url = work.Doi
                    }
                };

                var normalizedDoi = DoiUtility.Normalize(work.Doi);
                book.ForeignBookId = normalizedDoi;
                book.ForeignEditionId = normalizedDoi;
                book.TitleSlug = normalizedDoi?.Replace("/", "-");
            }

            // Map authors
            if (work.Authors != null && work.Authors.Any())
            {
                var primaryAuthor = work.Authors.FirstOrDefault();
                if (primaryAuthor != null)
                {
                    var authorName = GetAuthorName(primaryAuthor);
                    var foreignAuthorId = primaryAuthor.Orcid ?? $"crossref-{authorName.Replace(" ", "-")}";

                    var authorMetadata = new AuthorMetadata
                    {
                        Name = authorName,
                        SortName = authorName.ToLowerInvariant(),
                        NameLastFirst = authorName,
                        SortNameLastFirst = authorName.ToLowerInvariant(),
                        ForeignAuthorId = foreignAuthorId,
                        TitleSlug = foreignAuthorId,
                        Status = AuthorStatusType.Continuing,
                        Links = new List<Links>()
                    };

                    book.AuthorMetadata = authorMetadata;
                    book.Author = new Author
                    {
                        CleanName = authorName.CleanAuthorName(),
                        Metadata = authorMetadata,
                        Name = authorName
                    };
                }
            }

            book.CleanTitle = book.Title.CleanBookTitle();
            book.AnyEditionOk = true;

            // Build a single edition with richer metadata
            var edition = new Edition
            {
                ForeignEditionId = book.ForeignEditionId ?? book.ForeignBookId ?? Guid.NewGuid().ToString(),
                Title = book.Title,
                TitleSlug = book.TitleSlug ?? book.ForeignBookId ?? book.Title?.ToLowerInvariant(),
                ReleaseDate = book.ReleaseDate,
                Language = "en",
                Ratings = book.Ratings,
                Monitored = true,
                Overview = work.Abstract,
                Publisher = work.Publisher,
                Disambiguation = work.ContainerTitle?.FirstOrDefault()
            };

            // Add ISBN if available
            if (work.Isbn != null && work.Isbn.Any())
            {
                edition.Isbn13 = work.Isbn.FirstOrDefault();
            }

            if (book.Links != null && book.Links.Any())
            {
                edition.Links.AddRange(book.Links);
            }

            book.Editions = new List<Edition> { edition };

            return book;
        }

        private string GetAuthorName(CrossRefAuthor author)
        {
            if (!string.IsNullOrWhiteSpace(author.Name))
            {
                return author.Name;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(author.Given))
            {
                parts.Add(author.Given);
            }
            if (!string.IsNullOrWhiteSpace(author.Family))
            {
                parts.Add(author.Family);
            }

            return parts.Any() ? string.Join(" ", parts) : "Unknown Author";
        }

        private DateTime? GetPublishedDate(CrossRefWork work)
        {
            var date = work.Published ?? work.PublishedPrint ?? work.PublishedOnline;

            if (date?.DateParts != null && date.DateParts.Any())
            {
                var parts = date.DateParts.First();
                if (parts.Count >= 1)
                {
                    var year = parts[0];
                    var month = parts.Count >= 2 ? parts[1] : 1;
                    var day = parts.Count >= 3 ? parts[2] : 1;

                    try
                    {
                        return new DateTime(year, month, day);
                    }
                    catch
                    {
                        return new DateTime(year, 1, 1);
                    }
                }
            }

            return null;
        }

        private string BuildSearchQuery(string title, string author)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(title))
            {
                parts.Add(title);
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                parts.Add(author);
            }

            return string.Join(" ", parts);
        }

        private T ExecuteRequest<T>(HttpRequest request, bool useCache) where T : new()
        {
            var ttl = useCache ? TimeSpan.FromDays(7) : TimeSpan.Zero;
            var response = _cachedHttpClient.Get<T>(request, useCache, ttl);

            return response.Resource;
        }
    }
}
