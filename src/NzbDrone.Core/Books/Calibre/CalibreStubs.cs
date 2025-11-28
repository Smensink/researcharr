using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Books.Calibre
{
    public class CalibreSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string UrlBase { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Library { get; set; }
        public string OutputFormat { get; set; }
        public int OutputProfile { get; set; }
        public bool UseSsl { get; set; }
    }

    public enum CalibreProfile
    {
        Default = 0
    }

    [Flags]
    public enum CalibreFormat
    {
        Pdf = 1,
        Epub = 2,
        Mobi = 4,
        Azw3 = 8
    }

    public class CalibreBook
    {
        public int Id { get; set; }
        public Dictionary<string, string> Identifiers { get; set; } = new ();
        public List<string> Authors { get; set; } = new ();
        public string Title { get; set; }
        public Dictionary<string, string> Formats { get; set; } = new ();
    }

    public class CalibreException : Exception
    {
        public CalibreException(string message)
            : base(message)
        {
        }

        public CalibreException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public interface ICalibreProxy
    {
        void Test(CalibreSettings settings);
        IList<string> GetAllBookFilePaths(CalibreSettings settings);
        CalibreBook GetBook(int calibreId, CalibreSettings settings);
        void RemoveFormats(int calibreId, IEnumerable<string> formats, CalibreSettings settings);
        BookFile AddAndConvert(BookFile bookFile, CalibreSettings settings);
        void DeleteBook(BookFile bookFile, CalibreSettings settings);
        void DeleteBooks(IEnumerable<BookFile> bookFiles, CalibreSettings settings);
    }

    public class CalibreProxy : ICalibreProxy
    {
        private readonly Logger _logger;

        public CalibreProxy(Logger logger)
        {
            _logger = logger;
        }

        private void Warn(string action)
        {
            _logger.Warn("{0} requested but Calibre integration is not available in Researcharr.", action);
        }

        public void Test(CalibreSettings settings)
        {
            Warn("Calibre test");
        }

        public IList<string> GetAllBookFilePaths(CalibreSettings settings)
        {
            Warn("Calibre file path listing");
            return new List<string>();
        }

        public CalibreBook GetBook(int calibreId, CalibreSettings settings)
        {
            Warn("Calibre book lookup");
            return new CalibreBook { Id = calibreId };
        }

        public void RemoveFormats(int calibreId, IEnumerable<string> formats, CalibreSettings settings)
        {
            Warn("Calibre format removal");
        }

        public BookFile AddAndConvert(BookFile bookFile, CalibreSettings settings)
        {
            Warn("Calibre import");
            return bookFile;
        }

        public void DeleteBook(BookFile bookFile, CalibreSettings settings)
        {
            Warn("Calibre book delete");
        }

        public void DeleteBooks(IEnumerable<BookFile> bookFiles, CalibreSettings settings)
        {
            Warn("Calibre bulk delete");
        }
    }

    public static class Extensions
    {
        public static readonly HashSet<string> KnownLanguages =
            CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Select(c => c.TwoLetterISOLanguageName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.ToLowerInvariant())
                .ToHashSet();
    }
}
