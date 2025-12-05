using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;

namespace NzbDrone.Core.MediaFiles
{
    public interface IBookFileHardlinkService
    {
        void CreateHardlinksForBook(BookFile bookFile, Book book, Author journalAuthor, bool overwriteExisting = false);
        void CreateHardlinksForAuthor(Author author);
    }

    public class BookFileHardlinkService : IBookFileHardlinkService
    {
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IProvideBookInfo _bookInfo;
        private readonly IBuildFileNames _buildFileNames;
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAuthorMetadataRepository _authorMetadataRepository;
        private readonly Logger _logger;

        public BookFileHardlinkService(
            IAuthorService authorService,
            IBookService bookService,
            IMediaFileService mediaFileService,
            IProvideBookInfo bookInfo,
            IBuildFileNames buildFileNames,
            IDiskProvider diskProvider,
            IConfigService configService,
            IEventAggregator eventAggregator,
            IAuthorMetadataRepository authorMetadataRepository,
            Logger logger)
        {
            _authorService = authorService;
            _bookService = bookService;
            _mediaFileService = mediaFileService;
            _bookInfo = bookInfo;
            _buildFileNames = buildFileNames;
            _diskProvider = diskProvider;
            _configService = configService;
            _eventAggregator = eventAggregator;
            _authorMetadataRepository = authorMetadataRepository;
            _logger = logger;
        }

        public void CreateHardlinksForBook(BookFile bookFile, Book book, Author journalAuthor, bool overwriteExisting = false)
        {
            // Check if multi-author hardlinking is enabled
            if (!_configService.CreateMultiAuthorHardlinks)
            {
                return;
            }

            // Get journal author from book if not provided
            if (journalAuthor == null && book.AuthorMetadataId > 0)
            {
                journalAuthor = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);
            }

            // Fallback to lazy-loaded author
            if (journalAuthor == null)
            {
                journalAuthor = book.Author?.Value;
            }

            // Only create hardlinks for papers (books linked to journals)
            var isJournal = journalAuthor?.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                            string.Equals(journalAuthor?.Metadata?.Value?.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase);

            if (!isJournal)
            {
                // Not a paper, skip hardlinking
                _logger.Debug("Book {0} is not linked to a journal, skipping hardlink creation", book);
                return;
            }

            try
            {
                // Get all authors for this book from metadata
                var bookAuthors = GetAuthorsForBook(book);

                if (bookAuthors == null || !bookAuthors.Any())
                {
                    _logger.Debug("No authors found for book {0}, skipping hardlink creation", book);
                    return;
                }

                // Find which authors exist in the database (excluding the journal itself)
                var existingAuthors = bookAuthors
                    .Where(a => a.Type != AuthorMetadataType.Journal &&
                                !string.Equals(a.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase))
                    .Select(a => _authorService.FindById(a.ForeignAuthorId))
                    .Where(a => a != null)
                    .ToList();

                if (!existingAuthors.Any())
                {
                    _logger.Debug("No existing author entities found for book {0}, skipping hardlink creation", book);
                    return;
                }

                // Get the edition for this book file
                var edition = book.Editions?.Value?.FirstOrDefault(e => e.Id == bookFile.EditionId);
                if (edition == null)
                {
                    _logger.Warn("Edition not found for book file {0}, cannot create hardlinks", bookFile);
                    return;
                }

                // Primary file path (in journal folder)
                var primaryFilePath = bookFile.Path;

                if (!_diskProvider.FileExists(primaryFilePath))
                {
                    _logger.Warn("Primary file does not exist at {0}, cannot create hardlinks", primaryFilePath);
                    return;
                }

                // Create hardlinks for each author
                foreach (var author in existingAuthors)
                {
                    try
                    {
                        CreateHardlinkForAuthor(bookFile, book, edition, journalAuthor, author, primaryFilePath, overwriteExisting);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Failed to create hardlink for author {0} and book {1}", author, book);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating hardlinks for book {0}", book);
            }
        }

        public void CreateHardlinksForAuthor(Author author)
        {
            // Check if multi-author hardlinking is enabled
            if (!_configService.CreateMultiAuthorHardlinks)
            {
                return;
            }

            // Only process person authors (not journals)
            var isJournal = author?.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                            string.Equals(author?.Metadata?.Value?.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase);

            if (isJournal)
            {
                return;
            }

            try
            {
                // Find all books where this author appears
                var allBooks = _bookService.GetAllBooks();
                var matchingBooks = new List<Book>();

                foreach (var book in allBooks)
                {
                    var bookAuthors = GetAuthorsForBook(book);
                    if (bookAuthors != null && bookAuthors.Any(a => a.ForeignAuthorId == author.Metadata.Value.ForeignAuthorId))
                    {
                        // Check if book is linked to a journal
                        var bookAuthor = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);
                        var bookIsJournal = bookAuthor?.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                                            string.Equals(bookAuthor?.Metadata?.Value?.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase);

                        if (bookIsJournal)
                        {
                            matchingBooks.Add(book);
                        }
                    }
                }

                if (!matchingBooks.Any())
                {
                    _logger.Debug("No papers found for author {0}, skipping hardlink creation", author);
                    return;
                }

                // Create hardlinks for each matching book's files
                foreach (var book in matchingBooks)
                {
                    var bookFiles = _mediaFileService.GetFilesByBook(book.Id);
                    var journalAuthor = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);

                    foreach (var bookFile in bookFiles)
                    {
                        try
                        {
                            CreateHardlinksForBook(bookFile, book, journalAuthor);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn(ex, "Failed to create hardlinks for book {0} when processing author {1}", book, author);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating hardlinks for author {0}", author);
            }
        }

        private void CreateHardlinkForAuthor(BookFile bookFile, Book book, Edition edition, Author journalAuthor, Author author, string primaryFilePath, bool overwriteExisting = false)
        {
            // Build the file path for this author's folder
            var fileName = _buildFileNames.BuildBookFileName(author, edition, bookFile);
            var extension = Path.GetExtension(primaryFilePath);
            var authorFilePath = _buildFileNames.BuildBookFilePath(author, edition, fileName, extension);

            // Check if a file/hardlink already exists at the target path
            if (_diskProvider.FileExists(authorFilePath))
            {
                // Check if it's likely the same file (hardlink) by comparing size and modified time
                // This is not perfect but should catch most cases where the hardlink is valid
                try
                {
                    var existingFileInfo = _diskProvider.GetFileInfo(authorFilePath);
                    var primaryFileInfo = _diskProvider.GetFileInfo(primaryFilePath);
                    
                    // Compare file size and modified time (within 1 second tolerance)
                    // If they match, it's likely the same file (hardlink), so skip
                    if (existingFileInfo.Length == primaryFileInfo.Length &&
                        Math.Abs((existingFileInfo.LastWriteTimeUtc - primaryFileInfo.LastWriteTimeUtc).TotalSeconds) <= 1)
                    {
                        // Likely the same file (hardlink already exists and is valid)
                        _logger.Trace("Hardlink already exists at {0} for author {1}", authorFilePath, author);
                        return;
                    }
                    else
                    {
                        // Different file - could be a broken hardlink from a rename, or a different file
                        if (overwriteExisting)
                        {
                            // We're recreating after a rename, so delete the broken hardlink and recreate
                            _logger.Debug("Removing existing file at {0} (likely broken hardlink) before creating new hardlink for author {1}", 
                                authorFilePath, author);
                            try
                            {
                                _diskProvider.DeleteFile(authorFilePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn(ex, "Failed to delete existing file at {0}, skipping hardlink creation", authorFilePath);
                                return;
                            }
                        }
                        else
                        {
                            // For safety, skip rather than overwriting
                            _logger.Warn("File already exists at {0} for author {1} but appears to be different from primary file. Skipping hardlink creation to avoid overwriting.", 
                                authorFilePath, author);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Error checking existing file at {0}, skipping hardlink creation", authorFilePath);
                    return;
                }
            }

            // Ensure the author's folder structure exists
            var authorFolder = Path.GetDirectoryName(authorFilePath);
            if (!_diskProvider.FolderExists(authorFolder))
            {
                _diskProvider.CreateFolder(authorFolder);
            }

            // Create the hardlink
            var hardlinkCreated = _diskProvider.TryCreateHardLink(primaryFilePath, authorFilePath);

            if (hardlinkCreated)
            {
                _logger.Debug("Created hardlink for author {0}: {1} -> {2}", author.Name, primaryFilePath, authorFilePath);
                _eventAggregator.PublishEvent(new BookFileHardlinkedEvent(bookFile, author, authorFilePath));
            }
            else
            {
                _logger.Warn("Failed to create hardlink for author {0} from {1} to {2}. Hardlinks may not be supported on this filesystem.", 
                    author.Name, primaryFilePath, authorFilePath);
            }
        }

        private List<AuthorMetadata> GetAuthorsForBook(Book book)
        {
            try
            {
                // First, try to get authors from stored AuthorMetadataIds
                if (book.AuthorMetadataIds != null && book.AuthorMetadataIds.Any())
                {
                    // AuthorMetadataIds contains integer IDs (primary keys), use Get(IEnumerable<int>)
                    var authorMetadata = _authorMetadataRepository.Get(book.AuthorMetadataIds).ToList();
                    
                    if (authorMetadata.Any())
                    {
                        _logger.Trace("Retrieved {0} authors from stored AuthorMetadataIds for book {1}", authorMetadata.Count, book);
                        return authorMetadata;
                    }
                    else
                    {
                        _logger.Debug("AuthorMetadataIds {0} found for book {1} but no matching AuthorMetadata records found in database", 
                            string.Join(", ", book.AuthorMetadataIds), book);
                    }
                }

                // Fallback: try to get authors from metadata source if stored IDs are not available
                _logger.Debug("No stored AuthorMetadataIds for book {0}, fetching from metadata source", book);
                if (!string.IsNullOrWhiteSpace(book.ForeignBookId))
                {
                    var bookInfo = _bookInfo.GetBookInfo(book.ForeignBookId);
                    if (bookInfo != null && bookInfo.Item3 != null && bookInfo.Item3.Any())
                    {
                        return bookInfo.Item3;
                    }
                }

                // Fallback: return empty list if we can't get author metadata
                return new List<AuthorMetadata>();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error getting authors for book {0}", book);
                return new List<AuthorMetadata>();
            }
        }
    }
}

