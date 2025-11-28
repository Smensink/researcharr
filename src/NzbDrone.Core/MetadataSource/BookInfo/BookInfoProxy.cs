using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.OpenAlex;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public class BookInfoProxy : IProvideAuthorInfo, IProvideBookInfo, ISearchForNewBook, ISearchForNewAuthor, ISearchForNewEntity
    {
        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            Converters = { new STJUtcConverter() }
        };

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly Logger _logger;
        private readonly IMetadataRequestBuilder _requestBuilder;
        private readonly ICached<HashSet<string>> _cache;
        private readonly CachingService _authorCache;
        private readonly IOpenAlexProxy _openAlexProxy;
        private readonly IMetadataAggregator _metadataAggregator;

        public BookInfoProxy(IHttpClient httpClient,
                             ICachedHttpResponseService cachedHttpClient,
                             IAuthorService authorService,
                             IBookService bookService,
                             IEditionService editionService,
                             IMetadataRequestBuilder requestBuilder,
                             Logger logger,
                             ICacheManager cacheManager,
                             IOpenAlexProxy openAlexProxy,
                             IMetadataAggregator metadataAggregator)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _requestBuilder = requestBuilder;
            _cache = cacheManager.GetCache<HashSet<string>>(GetType());
            _logger = logger;
            _openAlexProxy = openAlexProxy;
            _metadataAggregator = metadataAggregator;

            _authorCache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 })));
            _authorCache.DefaultCachePolicy = new CacheDefaults
            {
                DefaultCacheDurationSeconds = 60
            };
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            var httpRequest = _requestBuilder.GetRequestBuilder().Create()
                .SetSegment("route", "author/changed")
                .AddQueryParam("since", startTime.ToString("o"))
                .Build();

            httpRequest.SuppressHttpError = true;

            var httpResponse = _httpClient.Get<RecentUpdatesResource>(httpRequest);

            if (httpResponse.Resource == null || httpResponse.Resource.Limited)
            {
                return null;
            }

            return new HashSet<string>(httpResponse.Resource.Ids.Select(x => x.ToString()));
        }

        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true, bool limitWorks = false, Action<List<Book>, int?> onWorkBatch = null, DateTime? updatedSince = null)
        {
            _logger.Debug("Getting Author details for {0}", foreignAuthorId);

            try
            {
                var author = _openAlexProxy.GetAuthorInfo(foreignAuthorId, useCache, limitWorks, onWorkBatch, updatedSince);

                if (author.Books != null && author.Books.Value.Any())
                {
                    var authors = new Dictionary<string, AuthorMetadata> { { author.Metadata.Value.ForeignAuthorId, author.Metadata.Value } };
                    foreach (var book in author.Books.Value)
                    {
                        AddDbIds(author.Metadata.Value.ForeignAuthorId, book, authors);
                    }
                }

                return author;
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Unexpected error getting author info: {foreignAuthorId}", foreignAuthorId);
                throw new BookInfoException("Failed to get author info", e);
            }
        }

        public HashSet<string> GetChangedBooks(DateTime startTime)
        {
            return _cache.Get("ChangedBooks", () => GetChangedBooksUncached(startTime), TimeSpan.FromMinutes(30));
        }

        private HashSet<string> GetChangedBooksUncached(DateTime startTime)
        {
            return null;
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
        {
            try
            {
                var tuple = _openAlexProxy.GetBookInfo(foreignBookId);
                var book = tuple.Item2;
                var authors = tuple.Item3
                    .GroupBy(x => x.ForeignAuthorId)
                    .Select(g => g.First())
                    .ToDictionary(x => x.ForeignAuthorId);

                try
                {
                    var doi = GetDoiFromLinks(book);
                    var identifier = !string.IsNullOrWhiteSpace(doi) ? doi : foreignBookId;

                    var enhanced = _metadataAggregator.GetEnhancedBookMetadata(identifier);
                    if (enhanced != null)
                    {
                        book = _metadataAggregator.MergeBookMetadata(new List<Book> { book, enhanced });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error enhancing metadata for {0}", foreignBookId);
                }

                AddDbIds(tuple.Item1, book, authors);
                return new Tuple<string, Book, List<AuthorMetadata>>(tuple.Item1, book, authors.Values.ToList());
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Unexpected error getting book info: {foreignBookId}", foreignBookId);
                throw new BookInfoException("Failed to get book info", e);
            }
        }

        public List<object> SearchForNewEntity(string title)
        {
            var searchTerm = title?.Trim() ?? string.Empty;

            var isConceptQuery = IsConceptQuery(searchTerm);

            var books = isConceptQuery ? new List<Book>() : SearchForNewBook(searchTerm, null, false);
            var authors = isConceptQuery ? new List<Author>() : SearchForNewAuthor(searchTerm);

            var result = new List<object>();

            foreach (var author in authors)
            {
                result.Add(author);
            }

            foreach (var book in books)
            {
                var author = book.Author.Value;

                if (!result.Any(r => r is Author a && a.ForeignAuthorId == author.ForeignAuthorId))
                {
                    result.Add(author);
                }

                result.Add(book);
            }

            if (isConceptQuery)
            {
                var concept = ExtractConcept(searchTerm);
                var topicBooks = SearchByConcept(concept);
                foreach (var book in topicBooks)
                {
                    var author = book.Author.Value;

                    if (!result.Any(r => r is Author a && a.ForeignAuthorId == author.ForeignAuthorId))
                    {
                        result.Add(author);
                    }

                    if (!result.Any(r => r is Book b && b.ForeignBookId == book.ForeignBookId))
                    {
                        result.Add(book);
                    }
                }
            }

            return result;
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            try
            {
                return _openAlexProxy.SearchForNewAuthor(title);
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Error searching for new author: {0}", title);
                return new List<Author>();
            }
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var q = title.ToLower().Trim();
            if (author != null)
            {
                q += " " + author;
            }

            try
            {
                return Search(q, getAllEditions);
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, ex.Message);
                throw new BookInfoException("Search for '{0}' failed. Unable to communicate with Metadata Source.", ex, title);
            }
            catch (Exception ex) when (ex is not BookInfoException)
            {
                _logger.Warn(ex, ex.Message);
                throw new BookInfoException("Search for '{0}' failed. Invalid response received.", ex, title);
            }
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            return Search(isbn, true);
        }

        public List<Book> SearchByAsin(string asin)
        {
            return Search(asin, true);
        }

        public List<Book> SearchByDoi(string doi)
        {
            var normalized = DoiUtility.Normalize(doi);
            var results = new List<Book>();

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                try
                {
                    var enhanced = _metadataAggregator.GetEnhancedBookMetadata(normalized);
                    if (enhanced != null)
                    {
                        results.Add(enhanced);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Error searching by DOI via aggregator: {0}", normalized);
                }
            }

            if (results.Any())
            {
                return results;
            }

            // OpenAlex supports DOI lookups - the search will match the DOI
            return Search(normalized ?? doi, true);
        }

        private List<Book> Search(string query, bool getAllEditions)
        {
            try
            {
                var books = _openAlexProxy.SearchForNewBook(query);

                foreach (var book in books)
                {
                    if (book.AuthorMetadata != null && book.AuthorMetadata.IsLoaded)
                    {
                        var authors = new Dictionary<string, AuthorMetadata> { { book.AuthorMetadata.Value.ForeignAuthorId, book.AuthorMetadata.Value } };
                        AddDbIds(book.AuthorMetadata.Value.ForeignAuthorId, book, authors);
                    }
                }

                return books;
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Error searching for book: {0}", query);
                return new List<Book>();
            }
        }

        private List<Book> SearchByConcept(string topic)
        {
            try
            {
                var books = _openAlexProxy.SearchByConcept(topic);

                foreach (var book in books)
                {
                    if (book.AuthorMetadata != null && book.AuthorMetadata.IsLoaded)
                    {
                        var authors = new Dictionary<string, AuthorMetadata> { { book.AuthorMetadata.Value.ForeignAuthorId, book.AuthorMetadata.Value } };
                        AddDbIds(book.AuthorMetadata.Value.ForeignAuthorId, book, authors);
                    }
                }

                return books;
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Error searching by topic: {0}", topic);
                return new List<Book>();
            }
        }

        private static bool IsConceptQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            return query.StartsWith("topic:", StringComparison.OrdinalIgnoreCase) ||
                   query.StartsWith("concept:", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractConcept(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return string.Empty;
            }

            var colonIndex = query.IndexOf(':');
            if (colonIndex < 0 || colonIndex >= query.Length - 1)
            {
                return query;
            }

            return query[(colonIndex + 1)..].Trim();
        }

        private void AddDbIds(string authorId, Book book, Dictionary<string, AuthorMetadata> authors)
        {
            var dbBook = _bookService.FindById(book.ForeignBookId);
            if (dbBook != null)
            {
                book.UseDbFieldsFrom(dbBook);

                var editions = _editionService.GetEditionsByBook(dbBook.Id).ToDictionary(x => x.ForeignEditionId);

                // If we have any database editions, exactly one will be monitored.
                // So unmonitor all the found editions and let the UseDbFieldsFrom set
                // the monitored status
                foreach (var edition in book.Editions.Value)
                {
                    edition.Monitored = false;
                    if (editions.TryGetValue(edition.ForeignEditionId, out var dbEdition))
                    {
                        edition.UseDbFieldsFrom(dbEdition);
                    }
                }

                // Double check at least one edition is monitored
                if (book.Editions.Value.Any() && !book.Editions.Value.Any(x => x.Monitored))
                {
                    var mostPopular = book.Editions.Value.OrderByDescending(x => x.Ratings.Popularity).First();
                    mostPopular.Monitored = true;
                }
            }

            var authorMetadataId = book.AuthorMetadata?.Value?.ForeignAuthorId;
            var authorIdFromBook = book.Author?.Value?.Metadata?.Value?.ForeignAuthorId;
            var targetAuthorId = authorMetadataId ?? authorIdFromBook ?? authorId;

            AuthorMetadata metadata = null;
            if (!string.IsNullOrWhiteSpace(targetAuthorId) && authors != null)
            {
                authors.TryGetValue(targetAuthorId, out metadata);
            }

            if (metadata == null && authors != null)
            {
                metadata = authors.Values.FirstOrDefault(x => !string.Equals(x.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase));
            }

            var author = metadata != null ? _authorService.FindById(metadata.ForeignAuthorId) : null;

            if (author == null && metadata != null)
            {
                author = new Author
                {
                    CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                    Metadata = metadata
                };
            }

            if ((author == null || metadata == null) && authors != null)
            {
                var journalMetadata = authors.Values.FirstOrDefault(x => string.Equals(x.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase));

                if (journalMetadata != null)
                {
                    metadata = journalMetadata;
                    author = _authorService.FindById(journalMetadata.ForeignAuthorId) ?? new Author
                    {
                        CleanName = Parser.Parser.CleanAuthorName(journalMetadata.Name),
                        Metadata = journalMetadata,
                        AddOptions = new AddAuthorOptions
                        {
                            BooksToMonitor = new List<string> { book.ForeignBookId }
                        }
                    };
                }
            }

            if (author == null || metadata == null)
            {
                throw new BookInfoException(string.Format("Expected author or journal metadata for book data {0}", book));
            }

            book.Author = author;
            book.AuthorMetadata = metadata;
            book.AuthorMetadataId = author.AuthorMetadataId;
        }

        private string GetDoiFromLinks(Book book)
        {
            if (book?.Links == null || !book.Links.Any())
            {
                return null;
            }

            var doiLink = book.Links.FirstOrDefault(l => l.Name?.Equals("doi", StringComparison.OrdinalIgnoreCase) == true);
            return doiLink?.Url;
        }
    }
}
