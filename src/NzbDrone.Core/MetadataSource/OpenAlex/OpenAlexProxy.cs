using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Http;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MetadataSource.OpenAlex
{
    public class OpenAlexProxy : IOpenAlexProxy
    {
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly Logger _logger;
        private readonly IHttpRequestBuilderFactory _requestBuilder;
        private readonly OpenAlexSettings _settings;

        public OpenAlexProxy(ICachedHttpResponseService cachedHttpClient, OpenAlexSettings settings, Logger logger)
        {
            _cachedHttpClient = cachedHttpClient;
            _settings = settings;
            _logger = logger;

            _requestBuilder = new HttpRequestBuilder(_settings.BaseUrl)
                .SetHeader("User-Agent", _settings.UserAgent)
                .KeepAlive()
                .CreateFactory();
        }

        public Author GetAuthorInfo(string readarrId, bool useCache = true, bool limitWorks = false)
        {
            // readarrId is expected to be the OpenAlex ID (e.g. A123456789 or full URL)
            var id = NormalizeId(readarrId);

            if (id.StartsWith("S", StringComparison.InvariantCultureIgnoreCase))
            {
                return GetSourceInfo(id, useCache, limitWorks);
            }

            var request = _requestBuilder.Create()
                .Resource($"authors/{id}")
                .Build();

            var response = ExecuteRequest<OpenAlexAuthor>(request, useCache);

            var author = MapAuthor(response);

            if (author != null)
            {
                var books = FetchAllWorks($"author.id:{id}", useCache, limitWorks ? 1000 : (int?)null);
                author.Books = new LazyLoaded<List<Book>>(books);
            }

            return author;
        }

        private Author GetSourceInfo(string id, bool useCache, bool limitWorks)
        {
            var request = _requestBuilder.Create()
                .Resource($"sources/{id}")
                .Build();

            var response = ExecuteRequest<OpenAlexSource>(request, useCache);
            var author = MapSource(response);

            if (author != null)
            {
                var books = FetchAllWorks($"primary_location.source.id:{id}", useCache, limitWorks ? 1000 : (int?)null);
                author.Books = new LazyLoaded<List<Book>>(books);
            }

            return author;
        }

        private List<Book> FetchAllWorks(string filter, bool useCache, int? maxCount = null)
        {
            var books = new List<Book>();
            var cursor = "*";

            while (!string.IsNullOrWhiteSpace(cursor))
            {
                var worksRequest = _requestBuilder.Create()
                    .Resource("works")
                    .AddQueryParam("filter", filter)
                    .AddQueryParam("per_page", "200")
                    .AddQueryParam("sort", "cited_by_count:desc")
                    .AddQueryParam("cursor", cursor)
                    .Build();

                var worksResponse = ExecuteRequest<OpenAlexListResponse<OpenAlexWork>>(worksRequest, useCache);

                if (worksResponse?.Results == null || worksResponse.Results.Count == 0)
                {
                    break;
                }

                books.AddRange(worksResponse.Results.Select(MapBook));

                if (maxCount.HasValue && books.Count >= maxCount.Value)
                {
                    break;
                }

                cursor = worksResponse.Meta?.NextCursor;
            }

            return books;
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            // OpenAlex doesn't easily support "changed since" for authors in a way Readarr expects
            return new HashSet<string>();
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            // Search both journals/sources and researchers
            // Journals will be marked with Type = Journal, researchers with Type = Person
            var authors = new List<Author>();

            // Search journals/sources first
            var sourceRequest = _requestBuilder.Create()
                .Resource("sources")
                .AddQueryParam("search", title)
                .AddQueryParam("sort", "relevance_score:desc")
                .Build();

            var sourceResponse = ExecuteRequest<OpenAlexListResponse<OpenAlexSource>>(sourceRequest, true);
            if (sourceResponse?.Results != null)
            {
                authors.AddRange(sourceResponse.Results.Select(MapSource));
            }

            // Then search researchers/authors
            var request = _requestBuilder.Create()
                .Resource("authors")
                .AddQueryParam("search", title)
                .AddQueryParam("sort", "relevance_score:desc")
                .Build();

            var response = ExecuteRequest<OpenAlexListResponse<OpenAlexAuthor>>(request, true);
            authors.AddRange(response.Results.Select(MapAuthor));

            return authors;
        }

        public List<Book> SearchForNewBook(string query)
        {
            var request = _requestBuilder.Create()
                .Resource("works")
                .AddQueryParam("search", query)
                .AddQueryParam("sort", "relevance_score:desc")
                .AddQueryParam("per_page", "50")
                .Build();

            var response = ExecuteRequest<OpenAlexListResponse<OpenAlexWork>>(request, true);

            return response.Results.Select(MapBook).ToList();
        }

        public List<Book> SearchByConcept(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                return new List<Book>();
            }

            var cleanedTopic = topic.Trim();

            try
            {
                var topicRequest = _requestBuilder.Create()
                    .Resource("topics")
                    .AddQueryParam("search", cleanedTopic)
                    .AddQueryParam("per_page", "1")
                    .Build();

                var topicResponse = ExecuteRequest<OpenAlexListResponse<OpenAlexTopic>>(topicRequest, true);
                var topicResult = topicResponse?.Results?.FirstOrDefault();

                if (topicResult == null)
                {
                    return new List<Book>();
                }

                var topicId = ExtractTopicId(topicResult.Id);
                if (string.IsNullOrWhiteSpace(topicId))
                {
                    return new List<Book>();
                }

                var request = _requestBuilder.Create()
                    .Resource("works")
                    .AddQueryParam("filter", $"topics.id:{topicId}")
                    .AddQueryParam("sort", "cited_by_count:desc")
                    .AddQueryParam("per_page", "50")
                    .Build();

                var response = ExecuteRequest<OpenAlexListResponse<OpenAlexWork>>(request, true);

                if (response?.Results == null)
                {
                    return new List<Book>();
                }

                return response.Results.Select(MapBook).ToList();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error searching OpenAlex topics for {0}", cleanedTopic);
                return new List<Book>();
            }
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            var workId = NormalizeId(id);

            var request = _requestBuilder.Create()
                .Resource($"works/{workId}")
                .Build();

            var response = ExecuteRequest<OpenAlexWork>(request, true);

            var book = MapBook(response);
            var authors = response.Authorships.Select(a => MapAuthorMetadata(a.Author)).ToList();

            // Note: Source/journal information is stored in Edition.Disambiguation, not as an author
            // Journals should not appear in the authors/researchers list

            return new Tuple<string, Book, List<AuthorMetadata>>(workId, book, authors);
        }

        public Book GetBookByDoi(string doi, bool useCache = true)
        {
            var normalizedDoi = DoiUtility.Normalize(doi);
            if (string.IsNullOrWhiteSpace(normalizedDoi))
            {
                return null;
            }

            try
            {
                // OpenAlex search by DOI
                var request = _requestBuilder.Create()
                    .Resource("works")
                    .AddQueryParam("filter", $"doi:{normalizedDoi}")
                    .Build();

                var response = ExecuteRequest<OpenAlexListResponse<OpenAlexWork>>(request, useCache);

                if (response?.Results != null && response.Results.Any())
                {
                    return MapBook(response.Results.First());
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error fetching book from OpenAlex by DOI: {0}", normalizedDoi);
            }

            return null;
        }

        private T ExecuteRequest<T>(HttpRequest request, bool useCache)
            where T : new()
        {
            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;

            var response = _cachedHttpClient.Get(request, useCache, TimeSpan.FromDays(1));

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return default(T);
                }

                throw new HttpException(request, response);
            }

            return Json.Deserialize<T>(response.Content);
        }

        private string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (id.StartsWith("https://openalex.org/"))
            {
                return id.Substring("https://openalex.org/".Length);
            }

            return id;
        }

        private string ExtractTopicId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var lastSegment = id.Substring(id.LastIndexOf('/') + 1);

            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                return null;
            }

            if (lastSegment.StartsWith("T", StringComparison.OrdinalIgnoreCase))
            {
                return lastSegment;
            }

            return $"T{lastSegment}";
        }

        private Author MapAuthor(OpenAlexAuthor source)
        {
            if (source == null)
            {
                return null;
            }

            var displayName = source.DisplayName ?? "Unknown Author";
            var sortName = displayName?.ToLowerInvariant();

            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = NormalizeId(source.Id),
                TitleSlug = NormalizeId(source.Id),
                Name = displayName,
                Disambiguation = source.LastKnownInstitutions?.FirstOrDefault()?.DisplayName,
                SortName = sortName,
                NameLastFirst = displayName,
                SortNameLastFirst = sortName,
                Status = AuthorStatusType.Continuing,
                Type = AuthorMetadataType.Person,
                Links = new List<Links>()
            };

            var author = new Author
            {
                CleanName = displayName.CleanAuthorName(),
                Metadata = metadata,
                Series = new LazyLoaded<List<Series>>(new List<Series>())
            };

            return author;
        }

        private Author MapSource(OpenAlexSource source)
        {
            if (source == null)
            {
                return null;
            }

            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = NormalizeId(source.Id),
                TitleSlug = NormalizeId(source.Id),
                Name = source.DisplayName,
                Disambiguation = "Journal",
                SortName = source.DisplayName?.ToLowerInvariant(),
                NameLastFirst = source.DisplayName,
                SortNameLastFirst = source.DisplayName?.ToLowerInvariant(),
                Status = AuthorStatusType.Continuing,
                Type = AuthorMetadataType.Journal,
                Links = new List<Links>()
            };

            var author = new Author
            {
                CleanName = source.DisplayName.CleanAuthorName(),
                Metadata = metadata,
                Series = new LazyLoaded<List<Series>>(new List<Series>())
            };

            return author;
        }

        private AuthorMetadata MapAuthorMetadata(OpenAlexAuthor source)
        {
            if (source == null)
            {
                return null;
            }

            return new AuthorMetadata
            {
                ForeignAuthorId = NormalizeId(source.Id),
                Name = source.DisplayName,
                TitleSlug = NormalizeId(source.Id),
                Disambiguation = source.LastKnownInstitutions?.FirstOrDefault()?.DisplayName,
                SortName = source.DisplayName?.ToLowerInvariant(),
                NameLastFirst = source.DisplayName,
                SortNameLastFirst = source.DisplayName?.ToLowerInvariant(),
                Status = AuthorStatusType.Continuing,
                Links = new List<Links>()
            };
        }

        private Book MapBook(OpenAlexWork source)
        {
            if (source == null)
            {
                return null;
            }

            var title = source.DisplayName ?? "Unknown Work";
            var titleSlug = NormalizeId(source.Id);

            var book = new Book
            {
                ForeignBookId = titleSlug,
                Title = title,
                TitleSlug = titleSlug,
                ReleaseDate = ParseDate(source.PublicationDate, source.PublicationYear),
                Links = new List<Links>(),
                Ratings = new Ratings
                {
                    Votes = source.CitedByCount,
                    Value = source.CitedByCount > 0 ? 1 : 0
                },
                CleanTitle = title.CleanBookTitle(),
                AnyEditionOk = true
            };

            if (source.Ids?.Doi != null)
            {
                book.Links.Add(new Links { Name = "DOI", Url = source.Ids.Doi });
            }

            if (source.OpenAccess?.OaUrl != null)
            {
                book.Links.Add(new Links { Name = "PDF", Url = source.OpenAccess.OaUrl });
            }

            // Create a default edition
            var edition = new Edition
            {
                ForeignEditionId = titleSlug,
                Title = title,
                TitleSlug = titleSlug,
                ReleaseDate = book.ReleaseDate,
                Disambiguation = source.PrimaryLocation?.Source?.DisplayName ?? source.PrimaryLocation?.RawSourceName,
                Language = "en", // Default
                Ratings = book.Ratings,
                Monitored = true
            };

            if (source.Ids?.Doi != null)
            {
                edition.Isbn13 = source.Ids.Doi;
                edition.Links.Add(new Links { Name = "DOI", Url = source.Ids.Doi });
            }

            book.Editions = new List<Edition> { edition };

            if (source.Authorships != null && source.Authorships.Any())
            {
                var primaryAuthor = source.Authorships.FirstOrDefault()?.Author;
                book.Author = MapAuthor(primaryAuthor);
            }
            else
            {
                book.Author = new Author
                {
                    Name = "Unknown Author",
                    CleanName = "unknownauthor",
                    Metadata = new AuthorMetadata
                    {
                        Name = "Unknown Author",
                        SortName = "unknown author",
                        NameLastFirst = "Author, Unknown",
                        SortNameLastFirst = "author, unknown",
                        Status = AuthorStatusType.Continuing,
                        Links = new List<Links>()
                    }
                };
            }

            return book;
        }

        private DateTime? ParseDate(string date, int? year)
        {
            if (DateTime.TryParse(date, out var result))
            {
                return result;
            }

            if (year.HasValue)
            {
                return new DateTime(year.Value, 1, 1);
            }

            return null;
        }
    }
}
