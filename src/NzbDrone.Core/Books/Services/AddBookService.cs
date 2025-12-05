using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.ImportLists.Exclusions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Books
{
    public interface IAddBookService
    {
        Book AddBook(Book book, bool doRefresh = true);
        List<Book> AddBooks(List<Book> books, bool doRefresh = true);
    }

    public class AddBookService : IAddBookService
    {
        private readonly IAuthorService _authorService;
        private readonly IAddAuthorService _addAuthorService;
        private readonly IBookService _bookService;
        private readonly IProvideBookInfo _bookInfo;
        private readonly IImportListExclusionService _importListExclusionService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IAuthorMetadataService _authorMetadataService;
        private readonly IAuthorMetadataRepository _authorMetadataRepository;
        private readonly Logger _logger;

        public AddBookService(IAuthorService authorService,
                               IAddAuthorService addAuthorService,
                               IBookService bookService,
                               IProvideBookInfo bookInfo,
                               IImportListExclusionService importListExclusionService,
                               IRootFolderService rootFolderService,
                               IAuthorMetadataService authorMetadataService,
                               IAuthorMetadataRepository authorMetadataRepository,
                               Logger logger)
        {
            _authorService = authorService;
            _addAuthorService = addAuthorService;
            _bookService = bookService;
            _bookInfo = bookInfo;
            _importListExclusionService = importListExclusionService;
            _rootFolderService = rootFolderService;
            _authorMetadataService = authorMetadataService;
            _authorMetadataRepository = authorMetadataRepository;
            _logger = logger;
        }

        public Book AddBook(Book book, bool doRefresh = true)
        {
            _logger.Debug($"Adding book {book}");

            book = AddSkyhookData(book);

            // we allow adding extra editions, so check if the book already exists
            var dbBook = _bookService.FindById(book.ForeignBookId);
            if (dbBook == null && book.Editions.Value.Any(e => !string.IsNullOrEmpty(e.Isbn13)))
            {
                var isbn = book.Editions.Value.First(e => !string.IsNullOrEmpty(e.Isbn13)).Isbn13;
                dbBook = _bookService.FindBookByIsbn13(isbn);
            }

            if (dbBook != null)
            {
                book.UseDbFieldsFrom(dbBook);
            }

            // Remove any import list exclusions preventing addition
            _importListExclusionService.Delete(book.ForeignBookId);
            _importListExclusionService.Delete(book.AuthorMetadata.Value.ForeignAuthorId);

            // Note it's a manual addition so it's not deleted on next refresh
            book.AddOptions.AddType = BookAddType.Manual;
            book.Editions.Value.Single(x => x.Monitored).ManualAdd = true;
            
            // When manually adding a book (e.g., from advanced search), set it to monitored
            // since the user explicitly chose to add it
            book.Monitored = true;

            // Add the author if necessary
            var dbAuthor = _authorService.FindById(book.AuthorMetadata.Value.ForeignAuthorId);
            if (dbAuthor == null)
            {
                var author = book.Author?.Value ?? new Author
                {
                    Metadata = new LazyLoaded<AuthorMetadata>(book.AuthorMetadata.Value),
                    AddOptions = new AddAuthorOptions()
                };

                author.Metadata.Value.ForeignAuthorId = book.AuthorMetadata.Value.ForeignAuthorId;

                if (string.IsNullOrWhiteSpace(author.RootFolderPath))
                {
                    var root = _rootFolderService.All().FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Path));
                    if (root == null || string.IsNullOrWhiteSpace(root.Path))
                    {
                        throw new ValidationException(new List<ValidationFailure>
                                                      {
                                                          new ValidationFailure("RootFolder", "No root folders are configured; cannot add author.", author.Metadata.Value.ForeignAuthorId)
                                                      });
                    }
                    author.RootFolderPath = root.Path;
                }

                // Ensure we don't auto-monitor other books (like the newest one) when adding a specific book
                if (author.AddOptions == null)
                {
                    author.AddOptions = new AddAuthorOptions();
                }
                author.AddOptions.Monitor = MonitorTypes.None;

                dbAuthor = _addAuthorService.AddAuthor(author, false);
            }

            book.Author = dbAuthor;
            book.AuthorMetadataId = dbAuthor.AuthorMetadataId;
            _bookService.AddBook(book, doRefresh);

            return book;
        }

        public List<Book> AddBooks(List<Book> books, bool doRefresh = true)
        {
            var added = DateTime.UtcNow;
            var addedBooks = new List<Book>();

            foreach (var a in books)
            {
                a.Added = added;
                try
                {
                    addedBooks.Add(AddBook(a, doRefresh));
                }
                catch (Exception ex)
                {
                    // Could be a bad id from an import list
                    _logger.Error(ex, "Failed to import id: {0} - {1}", a.ForeignBookId, a.Title);
                }
            }

            return addedBooks;
        }

        private Book AddSkyhookData(Book newBook)
        {
            var editionId = newBook.Editions?.Value?.FirstOrDefault(x => x.Monitored)?.ForeignEditionId;

            Tuple<string, Book, List<AuthorMetadata>> tuple = null;
            try
            {
                tuple = _bookInfo.GetBookInfo(newBook.ForeignBookId);
            }
            catch (BookNotFoundException)
            {
                _logger.Error("Book with Foreign Id {0} was not found, it may have been removed from Goodreads.", newBook.ForeignBookId);

                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("GoodreadsId", "A book with this ID was not found", newBook.ForeignBookId)
                                              });
            }

            if (tuple == null || tuple.Item2 == null || tuple.Item3 == null || !tuple.Item3.Any())
            {
                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("Metadata", "Unable to fetch metadata for this book from the provider.", newBook.ForeignBookId)
                                              });
            }

            newBook.UseMetadataFrom(tuple.Item2);
            newBook.Added = DateTime.UtcNow;

            var editions = tuple.Item2.Editions?.Value;
            if (editions == null || !editions.Any())
            {
                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("Editions", "No editions were returned for this book.", newBook.ForeignBookId)
                                              });
            }

            newBook.Editions = new LazyLoaded<List<Edition>>(editions);
            newBook.Editions.Value.ForEach(x => x.Monitored = false);

            // Try to re-apply the originally selected edition; otherwise fall back to first
            var selected = newBook.Editions.Value.FirstOrDefault(x => x.ForeignEditionId == editionId) ??
                           newBook.Editions.Value.FirstOrDefault();

            if (selected != null)
            {
                selected.Monitored = true;
            }
            else
            {
                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("EditionSelection", "No suitable edition was found for this book.", newBook.ForeignBookId)
                                              });
            }

            if (newBook.AuthorMetadata == null || newBook.AuthorMetadata.Value == null)
            {
                newBook.AuthorMetadata = new LazyLoaded<AuthorMetadata>(tuple.Item3.FirstOrDefault());
            }

            if (newBook.AuthorMetadata?.Value == null)
            {
                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("Author", "No author metadata was returned for this book.", newBook.ForeignBookId)
                                              });
            }

            newBook.AuthorMetadataId = newBook.AuthorMetadata.Value.Id;

            var authorId = newBook.AuthorMetadata.Value.ForeignAuthorId;
            var metadata = tuple.Item3.FirstOrDefault(x => x.ForeignAuthorId == authorId);

            if (metadata == null)
            {
                _logger.Warn("Author metadata for {0} not found in book info, using first available author.", authorId);
                metadata = tuple.Item3.FirstOrDefault();
            }

            if (metadata == null)
            {
                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("Author", "No matching author metadata was found for this book.", newBook.ForeignBookId)
                                              });
            }

            newBook.AuthorMetadata = new LazyLoaded<AuthorMetadata>(metadata);

            // Save all author metadata to database and store their IDs
            if (tuple.Item3 != null && tuple.Item3.Any())
            {
                _authorMetadataService.UpsertMany(tuple.Item3);
                
                // Fetch the saved metadata to get their IDs (after upsert, new items have IDs)
                var foreignIds = tuple.Item3.Select(m => m.ForeignAuthorId).ToList();
                var savedMetadata = _authorMetadataRepository.FindById(foreignIds);
                
                newBook.AuthorMetadataIds = savedMetadata.Select(m => m.Id).ToList();
            }

            return newBook;
        }
    }
}
