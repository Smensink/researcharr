using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.OpenAlex;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Books.Import
{
    public interface IImportSearchService
    {
        ImportSearchJob CreateJob(string name, ImportSearchSource source, List<ImportSearchItem> items);
        List<ImportSearchJob> AllJobs();
        List<ImportSearchItem> GetItems(int jobId);
        void ProcessJob(int jobId);
        List<ImportSearchItem> ParseStream(string fileName, Stream stream, out ImportSearchSource source);
        List<ImportSearchItem> ParseQuery(string query, out ImportSearchSource source);
    }

    public class ImportSearchService : IImportSearchService, IExecute<ProcessImportSearchCommand>
    {
        private readonly IImportSearchJobRepository _jobRepo;
        private readonly IImportSearchItemRepository _itemRepo;
        private readonly IBookService _bookService;
        private readonly IAddBookService _addBookService;
        private readonly IOpenAlexProxy _openAlexProxy;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public ImportSearchService(IImportSearchJobRepository jobRepo,
                                   IImportSearchItemRepository itemRepo,
                                   IBookService bookService,
                                   IAddBookService addBookService,
                                   IOpenAlexProxy openAlexProxy,
                                   IManageCommandQueue commandQueueManager,
                                   Logger logger)
        {
            _jobRepo = jobRepo;
            _itemRepo = itemRepo;
            _bookService = bookService;
            _addBookService = addBookService;
            _openAlexProxy = openAlexProxy;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public ImportSearchJob CreateJob(string name, ImportSearchSource source, List<ImportSearchItem> items)
        {
            var job = new ImportSearchJob
            {
                Name = name,
                Source = source,
                Status = ImportSearchStatus.Pending,
                Created = DateTime.UtcNow,
                Total = items.Count,
                Matched = 0,
                Queued = 0,
                Completed = 0,
                Failed = 0
            };

            _jobRepo.Insert(job);

            items.ForEach(i => i.JobId = job.Id);
            _itemRepo.InsertMany(items);

            return job;
        }

        public List<ImportSearchJob> AllJobs()
        {
            return _jobRepo.All().OrderByDescending(j => j.Created).ToList();
        }

        public List<ImportSearchItem> GetItems(int jobId)
        {
            return _itemRepo.All().Where(i => i.JobId == jobId).ToList();
        }

        public List<ImportSearchItem> ParseStream(string fileName, Stream stream, out ImportSearchSource source)
        {
            source = ImportSearchSource.Unknown;
            var items = new List<ImportSearchItem>();

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            if (content.Contains("<PubmedArticle", StringComparison.OrdinalIgnoreCase))
            {
                source = ImportSearchSource.PubMed;
                items.AddRange(ParsePubMedXml(content));
            }
            else
            {
                source = ImportSearchSource.RIS;
                items.AddRange(ParseRis(content));
            }

            return items;
        }

        public List<ImportSearchItem> ParseQuery(string query, out ImportSearchSource source)
        {
            source = ImportSearchSource.PubMed;

            if (query.IsNullOrWhiteSpace())
            {
                return new List<ImportSearchItem>();
            }

            var works = _openAlexProxy.SearchForNewBook(query);
            return works.Select(w => new ImportSearchItem
            {
                Title = w.Title,
                Authors = w.Author?.Value != null ? w.Author.Value.Name : null,
                Doi = w.Editions.Value.FirstOrDefault(e => !e.Isbn13.IsNullOrWhiteSpace())?.Isbn13,
                Status = ImportSearchItemStatus.Pending,
                BookId = null
            }).ToList();
        }

        private IEnumerable<ImportSearchItem> ParseRis(string content)
        {
            var items = new List<ImportSearchItem>();
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var current = new ImportSearchItem { Status = ImportSearchItemStatus.Pending };
            foreach (var line in lines)
            {
                if (line.StartsWith("TY", StringComparison.OrdinalIgnoreCase))
                {
                    current = new ImportSearchItem { Status = ImportSearchItemStatus.Pending };
                }
                else if (line.StartsWith("TI  -", StringComparison.OrdinalIgnoreCase) || line.StartsWith("T1  -", StringComparison.OrdinalIgnoreCase))
                {
                    current.Title = line.Substring(6).Trim();
                }
                else if (line.StartsWith("AU  -", StringComparison.OrdinalIgnoreCase) || line.StartsWith("A1  -", StringComparison.OrdinalIgnoreCase))
                {
                    var author = line.Substring(6).Trim();
                    current.Authors = current.Authors.IsNullOrWhiteSpace() ? author : $"{current.Authors}; {author}";
                }
                else if (line.StartsWith("DO  -", StringComparison.OrdinalIgnoreCase))
                {
                    current.Doi = line.Substring(6).Trim();
                }
                else if (line.StartsWith("PMID", StringComparison.OrdinalIgnoreCase) || line.StartsWith("ID  -", StringComparison.OrdinalIgnoreCase))
                {
                    current.Pmid = line.Substring(line.IndexOf('-') + 1).Trim();
                }
                else if (line.StartsWith("ER  -", StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(current);
                }
            }

            return items.Where(i => !i.Title.IsNullOrWhiteSpace() || !i.Doi.IsNullOrWhiteSpace() || !i.Pmid.IsNullOrWhiteSpace());
        }

        private IEnumerable<ImportSearchItem> ParsePubMedXml(string content)
        {
            var doc = XDocument.Parse(content);
            var ns = XNamespace.None;
            var items = new List<ImportSearchItem>();

            foreach (var article in doc.Descendants("PubmedArticle"))
            {
                var item = new ImportSearchItem { Status = ImportSearchItemStatus.Pending };
                item.Title = article.Descendants("ArticleTitle").FirstOrDefault()?.Value;
                var authors = article.Descendants("Author").Select(a =>
                {
                    var last = a.Element("LastName")?.Value;
                    var fore = a.Element("ForeName")?.Value;
                    return $"{fore} {last}".Trim();
                }).Where(a => !a.IsNullOrWhiteSpace());
                item.Authors = string.Join("; ", authors);
                item.Doi = article.Descendants("ArticleId")
                    .FirstOrDefault(id => (string)id.Attribute("IdType") == "doi")?.Value;
                item.Pmid = article.Descendants("ArticleId")
                    .FirstOrDefault(id => (string)id.Attribute("IdType") == "pubmed")?.Value;
                items.Add(item);
            }

            return items;
        }

        public void ProcessJob(int jobId)
        {
            var job = _jobRepo.Get(jobId);
            if (job == null)
            {
                return;
            }

            job.Status = ImportSearchStatus.Processing;
            job.Started = DateTime.UtcNow;
            _jobRepo.Update(job);

            job.Matched = 0;
            job.Queued = 0;
            job.Completed = 0;
            job.Failed = 0;

            var allBooks = _bookService.GetAllBooks();
            var items = GetItems(jobId);

            foreach (var item in items)
            {
                try
                {
                    item.Status = ImportSearchItemStatus.Pending;
                    _itemRepo.Update(item);

                    var normalizedDoi = DoiUtility.Normalize(item.Doi);
                    var matchedBookId = MatchExistingBook(normalizedDoi, item.Title, item.Authors, allBooks);

                    if (!matchedBookId.HasValue)
                    {
                        matchedBookId = AddFromOpenAlex(normalizedDoi, item.Title);
                    }

                    if (matchedBookId.HasValue)
                    {
                        item.BookId = matchedBookId.Value;
                        item.Status = ImportSearchItemStatus.Queued;
                        _itemRepo.Update(item);

                        _commandQueueManager.Push(new BookSearchCommand(new List<int> { matchedBookId.Value }), CommandPriority.Normal, CommandTrigger.Manual);

                        job.Matched++;
                        job.Queued++;
                    }
                    else
                    {
                        item.Status = ImportSearchItemStatus.Failed;
                        item.Message = "No matching book found in library (add author/paper first)";
                        _itemRepo.Update(item);
                        job.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    item.Status = ImportSearchItemStatus.Failed;
                    item.Message = ex.Message;
                    _itemRepo.Update(item);
                    job.Failed++;
                }
            }

            job.Ended = DateTime.UtcNow;
            job.Completed = job.Queued;
            job.Status = ImportSearchStatus.Completed;
            _jobRepo.Update(job);
        }

        private int? MatchExistingBook(string doi, string title, string authors, List<Book> allBooks)
        {
            // Try DOI first by scanning existing books' links for DOI
            if (!doi.IsNullOrWhiteSpace())
            {
                foreach (var book in allBooks)
                {
                    if (book.Links != null && book.Links.Any(l => l.Name.Equals("DOI", StringComparison.OrdinalIgnoreCase) && string.Equals(DoiUtility.Normalize(l.Url), doi, StringComparison.OrdinalIgnoreCase)))
                    {
                        return book.Id;
                    }
                }
            }

            // Fallback: search metadata by title
            if (!title.IsNullOrWhiteSpace())
            {
                var match = allBooks.FirstOrDefault(b => string.Equals(b.Title, title, StringComparison.OrdinalIgnoreCase));
                return match?.Id;
            }

            return null;
        }

        private int? AddFromOpenAlex(string normalizedDoi, string title)
        {
            try
            {
                Book book = null;

                if (!normalizedDoi.IsNullOrWhiteSpace())
                {
                    book = _openAlexProxy.GetBookByDoi(normalizedDoi);
                }

                if (book == null && !title.IsNullOrWhiteSpace())
                {
                    book = _openAlexProxy.SearchForNewBook(title)?.FirstOrDefault();
                }

                if (book == null)
                {
                    return null;
                }

                var added = _addBookService.AddBook(book, false);
                return added?.Id;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to add book from OpenAlex during import");
                return null;
            }
        }

        public void Execute(ProcessImportSearchCommand message)
        {
            ProcessJob(message.JobId);
        }
    }
}
