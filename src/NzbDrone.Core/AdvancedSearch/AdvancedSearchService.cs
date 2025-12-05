using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.OpenAlex;
using Newtonsoft.Json;

namespace NzbDrone.Core.AdvancedSearch
{
    public interface IAdvancedSearchService
    {
        OpenAlexListResponse<OpenAlexWork> Search(string search, string filter, string sort, int perPage, string cursor);
        SavedSearch Create(SavedSearch search);
        List<SavedSearch> All();
        void Delete(int id);
        SavedSearch Update(SavedSearch search);
        Book AddFromOpenAlexId(string openAlexId);
        Book AddFromDoi(string doi);

        // Async versions
        Task<OpenAlexListResponse<OpenAlexWork>> SearchAsync(string search, string filter, string sort, int perPage, string cursor);
        Task<Book> AddFromOpenAlexIdAsync(string openAlexId);
        Task<Book> AddFromDoiAsync(string doi);
    }

    public class AdvancedSearchService : IAdvancedSearchService
    {
        private readonly IOpenAlexProxy _openAlexProxy;
        private readonly ISavedSearchRepository _savedSearchRepository;
        private readonly IAddBookService _addBookService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public AdvancedSearchService(IOpenAlexProxy openAlexProxy,
                                     ISavedSearchRepository savedSearchRepository,
                                     IAddBookService addBookService,
                                     IManageCommandQueue commandQueueManager,
                                     IMediaFileService mediaFileService,
                                     Logger logger)
        {
            _openAlexProxy = openAlexProxy;
            _savedSearchRepository = savedSearchRepository;
            _addBookService = addBookService;
            _commandQueueManager = commandQueueManager;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public OpenAlexListResponse<OpenAlexWork> Search(string search, string filter, string sort, int perPage, string cursor)
        {
            if (perPage <= 0)
            {
                perPage = 25;
            }

            return _openAlexProxy.SearchWorksAdvanced(search, filter, sort, perPage, cursor);
        }

        public Book AddFromOpenAlexId(string openAlexId)
        {
            Ensure.That(openAlexId, nameof(openAlexId)).IsNotNullOrWhiteSpace();
            var tuple = _openAlexProxy.GetBookInfo(openAlexId);
            if (tuple == null)
            {
                return null;
            }

            var book = tuple.Item2;
            var addedBook = _addBookService.AddBook(book, false);

            if (addedBook != null && addedBook.Id > 0)
            {
                // Check if book already has files - only search if not already imported
                var existingFiles = _mediaFileService.GetFilesByBook(addedBook.Id);
                if (existingFiles == null || !existingFiles.Any())
                {
                    _logger.Debug("Book {0} ({1}) has no files, triggering search", addedBook.Title, addedBook.Id);
                    _commandQueueManager.Push(new BookSearchCommand(new List<int> { addedBook.Id }));
                }
                else
                {
                    _logger.Debug("Book {0} ({1}) already has {2} file(s), skipping search", addedBook.Title, addedBook.Id, existingFiles.Count);
                }
            }

            return addedBook;
        }

        public Book AddFromDoi(string doi)
        {
            Ensure.That(doi, nameof(doi)).IsNotNullOrWhiteSpace();
            var book = _openAlexProxy.GetBookByDoi(doi);
            if (book == null)
            {
                return null;
            }

            var addedBook = _addBookService.AddBook(book, false);

            if (addedBook != null && addedBook.Id > 0)
            {
                // Check if book already has files - only search if not already imported
                var existingFiles = _mediaFileService.GetFilesByBook(addedBook.Id);
                if (existingFiles == null || !existingFiles.Any())
                {
                    _logger.Debug("Book {0} ({1}) has no files, triggering search", addedBook.Title, addedBook.Id);
                    _commandQueueManager.Push(new BookSearchCommand(new List<int> { addedBook.Id }));
                }
                else
                {
                    _logger.Debug("Book {0} ({1}) already has {2} file(s), skipping search", addedBook.Title, addedBook.Id, existingFiles.Count);
                }
            }

            return addedBook;
        }

        public SavedSearch Create(SavedSearch search)
        {
            search.CreatedAt = DateTime.UtcNow;
            _savedSearchRepository.Insert(search);
            return search;
        }

        public List<SavedSearch> All()
        {
            return _savedSearchRepository.All().OrderByDescending(s => s.CreatedAt).ToList();
        }

        public void Delete(int id)
        {
            _savedSearchRepository.Delete(id);
        }

        public SavedSearch Update(SavedSearch search)
        {
            _savedSearchRepository.Update(search);
            return search;
        }

        public static string BuildPubMedQueryFromMeshJson(string meshJson)
        {
            if (string.IsNullOrWhiteSpace(meshJson))
            {
                return null;
            }

            try
            {
                var selections = JsonConvert.DeserializeObject<List<MeshSelection>>(meshJson);
                if (selections == null || selections.Count == 0)
                {
                    return null;
                }

                var clauses = selections.Select(sel =>
                {
                    var terms = new List<string>();
                    if (!string.IsNullOrWhiteSpace(sel.PreferredTerm))
                    {
                        terms.Add(sel.PreferredTerm);
                    }
                    if (sel.Synonyms != null)
                    {
                        terms.AddRange(sel.Synonyms);
                    }
                    var quoted = terms.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => $"\"{t}\"[tiab]");
                    return $"({string.Join(" OR ", quoted)})";
                }).Where(c => !string.IsNullOrWhiteSpace(c));

                return string.Join(" AND ", clauses);
            }
            catch
            {
                return null;
            }
        }

        // Async implementations
        public async Task<OpenAlexListResponse<OpenAlexWork>> SearchAsync(string search, string filter, string sort, int perPage, string cursor)
        {
            if (perPage <= 0)
            {
                perPage = 25;
            }

            return await _openAlexProxy.SearchWorksAdvancedAsync(search, filter, sort, perPage, cursor);
        }

        public async Task<Book> AddFromOpenAlexIdAsync(string openAlexId)
        {
            Ensure.That(openAlexId, nameof(openAlexId)).IsNotNullOrWhiteSpace();
            var tuple = await _openAlexProxy.GetBookInfoAsync(openAlexId);
            if (tuple == null)
            {
                return null;
            }

            var book = tuple.Item2;
            var addedBook = _addBookService.AddBook(book, false);

            if (addedBook != null && addedBook.Id > 0)
            {
                // Check if book already has files - only search if not already imported
                var existingFiles = _mediaFileService.GetFilesByBook(addedBook.Id);
                if (existingFiles == null || !existingFiles.Any())
                {
                    _logger.Debug("Book {0} ({1}) has no files, triggering search", addedBook.Title, addedBook.Id);
                    _commandQueueManager.Push(new BookSearchCommand(new List<int> { addedBook.Id }));
                }
                else
                {
                    _logger.Debug("Book {0} ({1}) already has {2} file(s), skipping search", addedBook.Title, addedBook.Id, existingFiles.Count);
                }
            }

            return addedBook;
        }

        public async Task<Book> AddFromDoiAsync(string doi)
        {
            Ensure.That(doi, nameof(doi)).IsNotNullOrWhiteSpace();
            var book = await _openAlexProxy.GetBookByDoiAsync(doi);
            if (book == null)
            {
                return null;
            }

            var addedBook = _addBookService.AddBook(book, false);

            if (addedBook != null && addedBook.Id > 0)
            {
                // Check if book already has files - only search if not already imported
                var existingFiles = _mediaFileService.GetFilesByBook(addedBook.Id);
                if (existingFiles == null || !existingFiles.Any())
                {
                    _logger.Debug("Book {0} ({1}) has no files, triggering search", addedBook.Title, addedBook.Id);
                    _commandQueueManager.Push(new BookSearchCommand(new List<int> { addedBook.Id }));
                }
                else
                {
                    _logger.Debug("Book {0} ({1}) already has {2} file(s), skipping search", addedBook.Title, addedBook.Id, existingFiles.Count);
                }
            }

            return addedBook;
        }

        private class MeshSelection
        {
            public string DescriptorUi { get; set; }
            public string PreferredTerm { get; set; }
            public List<string> Synonyms { get; set; }
        }
    }
}
