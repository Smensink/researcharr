using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;

namespace NzbDrone.Core.Books.Handlers
{
    public class PostMigrationFileMoveHandler : IHandle<ApplicationStartedEvent>
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMoveBookFiles _bookFileMover;
        private readonly IBuildFileNames _buildFileNames;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IMainDatabase _mainDatabase;
        private readonly Logger _logger;

        public PostMigrationFileMoveHandler(
            IBookService bookService,
            IAuthorService authorService,
            IMediaFileService mediaFileService,
            IMoveBookFiles bookFileMover,
            IBuildFileNames buildFileNames,
            IManageCommandQueue commandQueueManager,
            IMainDatabase mainDatabase,
            Logger logger)
        {
            _bookService = bookService;
            _authorService = authorService;
            _mediaFileService = mediaFileService;
            _bookFileMover = bookFileMover;
            _buildFileNames = buildFileNames;
            _commandQueueManager = commandQueueManager;
            _mainDatabase = mainDatabase;
            _logger = logger;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            try
            {
                // Only run if migration 51 (journal migration) has been applied
                var currentMigration = _mainDatabase.Migration;
                if (currentMigration < 51)
                {
                    _logger.Debug("Migration 51 not yet applied (current: {0}), skipping file move", currentMigration);
                    return;
                }

                // Check if we need to move files after journal migration
                // This runs after startup to handle files that were migrated from author to journal
                // It's safe to run multiple times - it only moves files that are in the wrong location
                MoveFilesForMigratedBooks();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error moving files for migrated books");
            }
        }

        private void MoveFilesForMigratedBooks()
        {
            _logger.Debug("Checking for books that need file moves after journal migration...");

            // Find all books that are linked to journals (Type = Journal)
            var allBooks = _bookService.GetAllBooks();
            var booksToMove = new List<Book>();

            foreach (var book in allBooks)
            {
                try
                {
                    // Get the current author (should be a journal after migration)
                    var journalAuthor = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);
                    
                    if (journalAuthor == null)
                    {
                        continue;
                    }

                    // Check if this is a journal
                    var isJournal = journalAuthor.Metadata?.Value?.Type == AuthorMetadataType.Journal ||
                                    string.Equals(journalAuthor.Metadata?.Value?.Disambiguation, "Journal", StringComparison.InvariantCultureIgnoreCase);

                    if (!isJournal)
                    {
                        // Not a journal, skip
                        continue;
                    }

                    // Get files for this book
                    var bookFiles = _mediaFileService.GetFilesByBook(book.Id);
                    
                    if (!bookFiles.Any())
                    {
                        continue;
                    }

                    // Check if files are in the wrong location (not in journal folder)
                    var needsMove = false;
                    foreach (var bookFile in bookFiles)
                    {
                        var edition = bookFile.Edition?.Value;
                        if (edition == null)
                        {
                            continue;
                        }

                        // Calculate where the file should be (in journal folder)
                        var expectedFileName = _buildFileNames.BuildBookFileName(journalAuthor, edition, bookFile);
                        var expectedPath = _buildFileNames.BuildBookFilePath(journalAuthor, edition, expectedFileName, Path.GetExtension(bookFile.Path));

                        // If file is not in the expected location, it needs to be moved
                        if (!bookFile.Path.PathEquals(expectedPath, StringComparison.Ordinal))
                        {
                            needsMove = true;
                            break;
                        }
                    }

                    if (needsMove)
                    {
                        booksToMove.Add(book);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Error checking if book {0} needs file move", book);
                }
            }

            if (!booksToMove.Any())
            {
                _logger.Debug("No books need file moves after migration");
                return;
            }

            _logger.Info("Found {0} books that need file moves after journal migration. Moving files...", booksToMove.Count);

            // Move files for each book
            var movedCount = 0;
            var failedCount = 0;

            foreach (var book in booksToMove)
            {
                try
                {
                    var journalAuthor = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);
                    var bookFiles = _mediaFileService.GetFilesByBook(book.Id);

                    foreach (var bookFile in bookFiles)
                    {
                        try
                        {
                            var edition = bookFile.Edition?.Value;
                            if (edition == null)
                            {
                                continue;
                            }

                            // Calculate new path in journal folder
                            var newFileName = _buildFileNames.BuildBookFileName(journalAuthor, edition, bookFile);
                            var newPath = _buildFileNames.BuildBookFilePath(journalAuthor, edition, newFileName, Path.GetExtension(bookFile.Path));

                            // Only move if path is different
                            if (!bookFile.Path.PathEquals(newPath, StringComparison.Ordinal))
                            {
                                _logger.Debug("Moving file for book {0}: {1} -> {2}", book, bookFile.Path, newPath);
                                _bookFileMover.MoveBookFile(bookFile, journalAuthor);
                                _mediaFileService.Update(bookFile);
                                movedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn(ex, "Failed to move file {0} for book {1}", bookFile.Path, book);
                            failedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to move files for book {0}", book);
                    failedCount++;
                }
            }

            _logger.Info("File move after migration complete: {0} files moved, {1} failed", movedCount, failedCount);
        }
    }
}

