using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles.Handlers
{
    public class AuthorAddedHardlinkHandler : IHandle<AuthorAddedEvent>,
                                               IHandle<BookFileRenamedEvent>
    {
        private readonly IBookFileHardlinkService _hardlinkService;
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly Logger _logger;

        public AuthorAddedHardlinkHandler(
            IBookFileHardlinkService hardlinkService,
            IBookService bookService,
            IAuthorService authorService,
            Logger logger)
        {
            _hardlinkService = hardlinkService;
            _bookService = bookService;
            _authorService = authorService;
            _logger = logger;
        }

        public void Handle(AuthorAddedEvent message)
        {
            try
            {
                _logger.Debug("Author {0} was added, creating hardlinks for any papers with this author", message.Author);
                _hardlinkService.CreateHardlinksForAuthor(message.Author);
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error creating hardlinks for newly added author {0}", message.Author);
            }
        }

        public void Handle(BookFileRenamedEvent message)
        {
            try
            {
                // When a file is renamed/moved, hardlinks break because the file moves to a new location
                // Recreate hardlinks for all authors after rename
                var bookFile = message.BookFile;
                var edition = bookFile.Edition?.Value;
                var book = edition?.Book?.Value;

                if (book != null)
                {
                    // Get the journal author
                    Author journalAuthor = null;
                    if (book.AuthorMetadataId > 0)
                    {
                        journalAuthor = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);
                    }

                    if (journalAuthor == null)
                    {
                        journalAuthor = book.Author?.Value;
                    }

                    if (journalAuthor != null)
                    {
                        _logger.Debug("File {0} was renamed, recreating hardlinks for all authors", bookFile.Path);
                        // Pass overwriteExisting=true to replace any broken hardlinks from the old path
                        _hardlinkService.CreateHardlinksForBook(bookFile, book, journalAuthor, overwriteExisting: true);
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "Error recreating hardlinks after file rename for {0}", message.BookFile);
            }
        }
    }
}

