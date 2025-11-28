using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Azw;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RootFolders;
using PdfSharpCore.Pdf.IO;
using UglyToad.PdfPig;

namespace NzbDrone.Core.MediaFiles
{
    public interface IEBookTagService
    {
        ParsedTrackInfo ReadTags(IFileInfo file);
        void WriteTags(BookFile trackfile, bool newDownload, bool force = false);
        void SyncTags(List<Edition> books);
        List<RetagBookFilePreview> GetRetagPreviewsByAuthor(int authorId);
        List<RetagBookFilePreview> GetRetagPreviewsByBook(int bookId);
        void RetagFiles(RetagFilesCommand message);
        void RetagAuthor(RetagAuthorCommand message);
    }

    public class EBookTagService : IEBookTagService
    {
        private readonly IAuthorService _authorService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public EBookTagService(IAuthorService authorService,
            IMediaFileService mediaFileService,
            IRootFolderService rootFolderService,
            IConfigService configService,
            Logger logger)
        {
            _authorService = authorService;
            _mediaFileService = mediaFileService;
            _rootFolderService = rootFolderService;
            _configService = configService;

            _logger = logger;
        }

        public ParsedTrackInfo ReadTags(IFileInfo file)
        {
            var extension = file.Extension.ToLower();
            _logger.Trace($"Got extension '{extension}'");

            switch (extension)
            {
                case ".pdf":
                    return ReadPdf(file.FullName);
                case ".epub":
                case ".kepub":
                    _logger.Debug("EPUB metadata parsing is not supported, falling back to filename parsing for {0}", file.FullName);
                    return Parser.Parser.ParseTitle(file.FullName);
                case ".azw3":
                case ".mobi":
                    return ReadAzw3(file.FullName);
                default:
                    return Parser.Parser.ParseTitle(file.FullName);
            }
        }

        public void WriteTags(BookFile bookFile, bool newDownload, bool force = false)
        {
            // Calibre integration removed
            _logger.Debug($"Writing tags for {bookFile} - SKIPPED (Calibre removed)");
        }

        public void SyncTags(List<Edition> editions)
        {
            // Calibre integration removed
            _logger.Debug($"Syncing ebook tags - SKIPPED (Calibre removed)");
        }

        public List<RetagBookFilePreview> GetRetagPreviewsByAuthor(int authorId)
        {
            return new List<RetagBookFilePreview>();
        }

        public List<RetagBookFilePreview> GetRetagPreviewsByBook(int bookId)
        {
            return new List<RetagBookFilePreview>();
        }

        public void RetagFiles(RetagFilesCommand message)
        {
            _logger.Info("Retagging files - SKIPPED (Calibre removed)");
        }

        public void RetagAuthor(RetagAuthorCommand message)
        {
            _logger.Info("Retagging author files - SKIPPED (Calibre removed)");
        }

        private ParsedTrackInfo ReadAzw3(string file)
        {
            _logger.Trace($"Reading {file}");
            var result = new ParsedTrackInfo();

            try
            {
                var book = new Azw3File(file);
                result.Authors = book.Authors;
                result.BookTitle = book.Title;
                result.Isbn = StripIsbn(book.Isbn);
                result.Asin = book.Asin;
                result.Language = book.Language;
                result.Disambiguation = book.Description;
                result.Publisher = book.Publisher;
                result.Label = book.Imprint;
                result.Source = book.Source;

                result.Quality = new QualityModel
                {
                    Quality = book.Version <= 6 ? Quality.MOBI : Quality.AZW3,
                    QualityDetectionSource = QualityDetectionSource.TagLib
                };
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error reading file");

                result.Quality = new QualityModel
                {
                    Quality = Path.GetExtension(file) == ".mobi" ? Quality.MOBI : Quality.AZW3,
                    QualityDetectionSource = QualityDetectionSource.Extension
                };
            }

            _logger.Trace($"Got {result.ToJson()}");

            return result;
        }

        private ParsedTrackInfo ReadPdf(string file)
        {
            _logger.Trace($"Reading {file}");
            var result = new ParsedTrackInfo
            {
                Quality = new QualityModel
                {
                    Quality = Quality.PDF,
                    QualityDetectionSource = QualityDetectionSource.TagLib
                }
            };

            try
            {
                var book = PdfReader.Open(file, PdfDocumentOpenMode.InformationOnly);
                if (book.Info != null)
                {
                    result.Authors = new List<string> { book.Info.Author };
                    result.BookTitle = book.Info.Title;

                    // Try to extract DOI from PDF metadata fields
                    result.Doi = ExtractDoiFromPdfMetadata(book.Info);

                    _logger.Trace(book.Info.ToJson());
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error reading pdf metadata");
                result.Quality.QualityDetectionSource = QualityDetectionSource.Extension;
            }

            // If no DOI found in metadata, try extracting from filename
            if (result.Doi.IsNullOrWhiteSpace())
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                result.Doi = DoiUtility.ExtractFromFilename(filename);
                if (result.Doi.IsNotNullOrWhiteSpace())
                {
                    _logger.Debug($"Extracted DOI from filename: {result.Doi}");
                }
            }

            // If still no DOI, try extracting from PDF content (first few pages)
            if (result.Doi.IsNullOrWhiteSpace())
            {
                result.Doi = ExtractDoiFromPdfContent(file);
                if (result.Doi.IsNotNullOrWhiteSpace())
                {
                    _logger.Debug($"Extracted DOI from PDF content: {result.Doi}");
                }
            }

            _logger.Trace($"Got:\n{result.ToJson()}");

            return result;
        }

        private string ExtractDoiFromPdfMetadata(PdfSharpCore.Pdf.PdfDocumentInformation info)
        {
            // Try various metadata fields where DOI might be stored
            var fieldsToCheck = new[]
            {
                info.Subject,
                info.Keywords,
                info.Creator,
                info.Producer,
                info.Title
            };

            foreach (var field in fieldsToCheck)
            {
                if (field.IsNotNullOrWhiteSpace())
                {
                    var doi = DoiUtility.ExtractFromText(field);
                    if (doi.IsNotNullOrWhiteSpace())
                    {
                        _logger.Debug($"Found DOI in PDF metadata field: {doi}");
                        return doi;
                    }
                }
            }

            return null;
        }

        private string ExtractDoiFromPdfContent(string file)
        {
            try
            {
                using var document = PdfDocument.Open(file);

                // Only scan first 3 pages - DOI is typically on first page or in header/footer
                var pagesToScan = Math.Min(3, document.NumberOfPages);
                var textBuilder = new StringBuilder();

                for (var i = 1; i <= pagesToScan; i++)
                {
                    var page = document.GetPage(i);
                    textBuilder.AppendLine(page.Text);

                    // Check for DOI after each page to potentially exit early
                    var pageText = textBuilder.ToString();
                    var doi = DoiUtility.ExtractFromText(pageText);
                    if (doi.IsNotNullOrWhiteSpace())
                    {
                        return doi;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Error extracting text from PDF for DOI detection");
                return null;
            }
        }

        private string GetIsbnChars(string input)
        {
            if (input == null)
            {
                return null;
            }

            return new string(input.Where(c => char.IsDigit(c) || c == 'X' || c == 'x').ToArray());
        }

        private string StripIsbn(string input)
        {
            var isbn = GetIsbnChars(input);

            if (isbn == null)
            {
                return null;
            }
            else if ((isbn.Length == 10 && ValidateIsbn10(isbn)) ||
                (isbn.Length == 13 && ValidateIsbn13(isbn)))
            {
                return isbn;
            }

            return null;
        }

        private static char Isbn10Checksum(string isbn)
        {
            var sum = 0;
            for (var i = 0; i < 9; i++)
            {
                sum += int.Parse(isbn[i].ToString()) * (10 - i);
            }

            var result = sum % 11;

            if (result == 0)
            {
                return '0';
            }
            else if (result == 1)
            {
                return 'X';
            }

            return (11 - result).ToString()[0];
        }

        private static char Isbn13Checksum(string isbn)
        {
            var result = 0;
            for (var i = 0; i < 12; i++)
            {
                result += int.Parse(isbn[i].ToString()) * ((i % 2 == 0) ? 1 : 3);
            }

            result %= 10;

            return result == 0 ? '0' : (10 - result).ToString()[0];
        }

        private static bool ValidateIsbn10(string isbn)
        {
            return ulong.TryParse(isbn.Substring(0, 9), out _) && isbn[9] == Isbn10Checksum(isbn);
        }

        private static bool ValidateIsbn13(string isbn)
        {
            return ulong.TryParse(isbn, out _) && isbn[12] == Isbn13Checksum(isbn);
        }
    }
}
