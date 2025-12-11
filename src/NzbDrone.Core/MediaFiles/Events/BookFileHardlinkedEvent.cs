using NzbDrone.Common.Messaging;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class BookFileHardlinkedEvent : IEvent
    {
        public BookFile BookFile { get; private set; }
        public Author Author { get; private set; }
        public string HardlinkPath { get; private set; }

        public BookFileHardlinkedEvent(BookFile bookFile, Author author, string hardlinkPath)
        {
            BookFile = bookFile;
            Author = author;
            HardlinkPath = hardlinkPath;
        }
    }
}


