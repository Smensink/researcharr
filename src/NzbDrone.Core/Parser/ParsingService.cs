using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    public interface IParsingService
    {
        Author GetAuthor(string title);
        RemoteBook Map(ParsedBookInfo parsedBookInfo, SearchCriteriaBase searchCriteria = null);
        RemoteBook Map(ParsedBookInfo parsedBookInfo, int authorId, IEnumerable<int> bookIds);
        List<Book> GetBooks(ParsedBookInfo parsedBookInfo, Author author, SearchCriteriaBase searchCriteria = null);

        ParsedBookInfo ParseBookTitleFuzzy(string title);

        // Music stuff here
        Book GetLocalBook(string filename, Author author);
    }

    public class ParsingService : IParsingService
    {
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public ParsingService(IAuthorService authorService,
                              IBookService bookService,
                              IEditionService editionService,
                              IMediaFileService mediaFileService,
                              Logger logger)
        {
            _bookService = bookService;
            _editionService = editionService;
            _authorService = authorService;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public Author GetAuthor(string title)
        {
            var parsedBookInfo = Parser.ParseBookTitle(title);

            if (parsedBookInfo != null && !parsedBookInfo.AuthorName.IsNullOrWhiteSpace())
            {
                title = parsedBookInfo.AuthorName;
            }

            var authorInfo = _authorService.FindByName(title);

            if (authorInfo == null)
            {
                _logger.Debug("Trying inexact author match for {0}", title);
                authorInfo = _authorService.FindByNameInexact(title);
            }

            return authorInfo;
        }

        public RemoteBook Map(ParsedBookInfo parsedBookInfo, SearchCriteriaBase searchCriteria = null)
        {
            // Ensure ParsedBookInfo is never null - create a minimal one if needed
            if (parsedBookInfo == null)
            {
                parsedBookInfo = new ParsedBookInfo
                {
                    Quality = new QualityModel(Quality.PDF) // Default quality for academic papers
                };
            }
            // Ensure Quality is always set
            else if (parsedBookInfo.Quality == null)
            {
                parsedBookInfo.Quality = new QualityModel(Quality.PDF);
            }

            var remoteBook = new RemoteBook
            {
                ParsedBookInfo = parsedBookInfo,
            };

            // GetAuthor can handle null parsedBookInfo (it will check searchCriteria)
            var author = GetAuthor(parsedBookInfo, searchCriteria);

            if (author == null)
            {
                return remoteBook;
            }

            remoteBook.Author = author;
            remoteBook.Books = GetBooks(parsedBookInfo, author, searchCriteria);

            // If we matched an author but no book, still allow downstream specs to consider it
            // by populating a synthetic Book from the parsed title when we have one.
            if (!remoteBook.Books.Any() && parsedBookInfo.BookTitle.IsNotNullOrWhiteSpace())
            {
                remoteBook.Books.Add(new Book
                {
                    Id = 0,
                    Title = parsedBookInfo.BookTitle,
                    CleanTitle = Parser.CleanAuthorName(parsedBookInfo.BookTitle),
                    Author = author,
                    AuthorMetadataId = author.AuthorMetadataId
                });
            }

            return remoteBook;
        }

        public List<Book> GetBooks(ParsedBookInfo parsedBookInfo, Author author, SearchCriteriaBase searchCriteria = null)
        {
            // If we have search criteria with books, verify the parsed title matches before returning them
            // This prevents returning wrong books when parsing matched incorrectly
            if (searchCriteria != null && searchCriteria.Books != null && searchCriteria.Books.Any())
            {
                // If we have a parsed book title, verify it matches one of the search criteria books
                if (parsedBookInfo?.BookTitle != null)
                {
                    var parsedTitle = Parser.Parser.NormalizeTitleSeparators(parsedBookInfo.BookTitle);
                    var cleanParsedTitle = Parser.CleanAuthorName(parsedTitle);
                    
                    // Check for exact or close match in search criteria books
                    var matchingBook = searchCriteria.Books.FirstOrDefault(b => 
                        b.Title.Equals(parsedTitle, StringComparison.OrdinalIgnoreCase) ||
                        b.CleanTitle.Equals(cleanParsedTitle, StringComparison.OrdinalIgnoreCase) ||
                        Parser.Parser.NormalizeTitleSeparators(b.Title).Equals(parsedTitle, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchingBook != null)
                    {
                        // Return only the matching book, not all books
                        return new List<Book> { matchingBook };
                    }
                    
                    // If no exact match, try fuzzy matching with a high threshold
                    var wordDelimiters = new HashSet<char>(" .,_-=()[]|\"`''");
                    var fuzzyMatches = searchCriteria.Books
                        .Select(b =>
                        {
                            var normalizedTitle = Parser.Parser.NormalizeTitleSeparators(b.Title);
                            var (_, _, score) = parsedTitle.ToLowerInvariant().FuzzyMatch(normalizedTitle.ToLowerInvariant(), 0.5, wordDelimiters);
                            return new { Book = b, Score = score };
                        })
                        .Where(x => x.Score >= 0.7) // Require at least 70% match
                        .OrderByDescending(x => x.Score)
                        .ToList();
                    
                    if (fuzzyMatches.Any())
                    {
                        // Return only the best matching book
                        return new List<Book> { fuzzyMatches.First().Book };
                    }
                    
                    // If parsed title doesn't match any search criteria book, don't return them
                    // This prevents wrong books from being assigned
                    _logger.Debug("Parsed book title '{0}' doesn't match any search criteria books, not returning search criteria books", parsedTitle);
                }
                else
                {
                    // No parsed title, so we can't verify - return search criteria books as fallback
                    // This is safe because BookRequestedSpecification will verify by ID
                    return searchCriteria.Books;
                }
            }

            if (parsedBookInfo == null || parsedBookInfo.BookTitle == null)
            {
                return new List<Book>();
            }

            var bookTitle = parsedBookInfo.BookTitle;
            var result = new List<Book>();

            Book bookInfo = null;

            if (parsedBookInfo.Discography)
            {
                if (parsedBookInfo.DiscographyStart > 0)
                {
                    return _bookService.AuthorBooksBetweenDates(author,
                        new DateTime(parsedBookInfo.DiscographyStart, 1, 1),
                        new DateTime(parsedBookInfo.DiscographyEnd, 12, 31),
                        false);
                }

                if (parsedBookInfo.DiscographyEnd > 0)
                {
                    return _bookService.AuthorBooksBetweenDates(author,
                        new DateTime(1800, 1, 1),
                        new DateTime(parsedBookInfo.DiscographyEnd, 12, 31),
                        false);
                }

                return _bookService.GetBooksByAuthor(author.Id);
            }

            if (searchCriteria != null)
            {
                var cleanTitle = Parser.CleanAuthorName(parsedBookInfo.BookTitle);
                bookInfo = searchCriteria.Books.ExclusiveOrDefault(e => e.Title == bookTitle || e.CleanTitle == cleanTitle);
            }

            if (bookInfo == null)
            {
                // TODO: Search by Title and Year instead of just Title when matching
                bookInfo = _bookService.FindByTitle(author.AuthorMetadataId, parsedBookInfo.BookTitle);
            }

            if (bookInfo == null)
            {
                var edition = _editionService.FindByTitle(author.AuthorMetadataId, parsedBookInfo.BookTitle);
                bookInfo = edition?.Book.Value;
            }

            if (bookInfo == null)
            {
                _logger.Debug("Trying inexact book match for {0}", parsedBookInfo.BookTitle);
                bookInfo = _bookService.FindByTitleInexact(author.AuthorMetadataId, parsedBookInfo.BookTitle);
            }

            if (bookInfo == null)
            {
                _logger.Debug("Trying inexact edition match for {0}", parsedBookInfo.BookTitle);
                var edition = _editionService.FindByTitleInexact(author.AuthorMetadataId, parsedBookInfo.BookTitle);
                bookInfo = edition?.Book.Value;
            }

            if (bookInfo != null)
            {
                result.Add(bookInfo);
            }
            else
            {
                _logger.Debug("Unable to find {0}", parsedBookInfo);
            }

            return result;
        }

        public RemoteBook Map(ParsedBookInfo parsedBookInfo, int authorId, IEnumerable<int> bookIds)
        {
            return new RemoteBook
            {
                ParsedBookInfo = parsedBookInfo,
                Author = _authorService.GetAuthor(authorId),
                Books = _bookService.GetBooks(bookIds)
            };
        }

        private Author GetAuthor(ParsedBookInfo parsedBookInfo, SearchCriteriaBase searchCriteria)
        {
            // If we have search criteria with author and books, use it (especially for book searches)
            if (searchCriteria != null && searchCriteria.Author != null && 
                searchCriteria.Books != null && searchCriteria.Books.Any())
            {
                // For book searches, trust the search criteria author
                return searchCriteria.Author;
            }

            // Try to match parsed author name with search criteria author
            if (searchCriteria != null && searchCriteria.Author != null && 
                parsedBookInfo != null && parsedBookInfo.AuthorName.IsNotNullOrWhiteSpace())
            {
                if (searchCriteria.Author.CleanName == parsedBookInfo.AuthorName.CleanAuthorName())
                {
                    return searchCriteria.Author;
                }
            }

            // If no parsed author name, can't find author
            if (parsedBookInfo == null || parsedBookInfo.AuthorName.IsNullOrWhiteSpace())
            {
                return null;
            }

            var author = _authorService.FindByName(parsedBookInfo.AuthorName);

            if (author == null)
            {
                _logger.Debug("Trying inexact author match for {0}", parsedBookInfo.AuthorName);
                author = _authorService.FindByNameInexact(parsedBookInfo.AuthorName);
            }

            if (author == null)
            {
                _logger.Debug("No matching author {0}", parsedBookInfo.AuthorName);
                return null;
            }

            return author;
        }

        public ParsedBookInfo ParseBookTitleFuzzy(string title)
        {
            var bestScore = 0.0;

            Author bestAuthor = null;
            Book bestBook = null;

            var possibleAuthors = _authorService.GetReportCandidates(title);

            foreach (var author in possibleAuthors)
            {
                _logger.Trace($"Trying possible author {author}");

                var authorMatch = title.FuzzyMatch(author.Metadata.Value.Name, 0.5);
                var possibleBooks = _bookService.GetCandidates(author.AuthorMetadataId, title);

                foreach (var book in possibleBooks)
                {
                    var bookMatch = title.FuzzyMatch(book.Title, 0.5);
                    var score = (authorMatch.Item3 + bookMatch.Item3) / 2;

                    _logger.Trace($"Book {book} has score {score}");

                    if (score > bestScore)
                    {
                        bestAuthor = author;
                        bestBook = book;
                    }
                }

                var possibleEditions = _editionService.GetCandidates(author.AuthorMetadataId, title);
                foreach (var edition in possibleEditions)
                {
                    var editionMatch = title.FuzzyMatch(edition.Title, 0.5);
                    var score = (authorMatch.Item3 + editionMatch.Item3) / 2;

                    _logger.Trace($"Edition {edition} has score {score}");

                    if (score > bestScore)
                    {
                        bestAuthor = author;
                        bestBook = edition.Book.Value;
                    }
                }
            }

            _logger.Trace($"Best match: {bestAuthor} {bestBook}");

            if (bestAuthor != null)
            {
                return Parser.ParseBookTitleWithSearchCriteria(title, bestAuthor, new List<Book> { bestBook });
            }

            return null;
        }

        public Book GetLocalBook(string filename, Author author)
        {
            if (Path.HasExtension(filename))
            {
                filename = Path.GetDirectoryName(filename);
            }

            var tracksInBook = _mediaFileService.GetFilesByAuthor(author.Id)
                .FindAll(s => Path.GetDirectoryName(s.Path) == filename)
                .DistinctBy(s => s.EditionId)
                .ToList();

            return tracksInBook.Count == 1 ? _bookService.GetBook(tracksInBook.First().EditionId) : null;
        }
    }
}
