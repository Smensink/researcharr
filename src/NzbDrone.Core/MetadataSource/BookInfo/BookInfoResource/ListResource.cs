using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public class ListResource
    {
        public List<ListBookResource> Books { get; set; } = new List<ListBookResource>();
    }

    public class ListBookResource
    {
        public long Id { get; set; }
        public WorkResource Work { get; set; }
        public List<ListBookAuthorResource> Authors { get; set; } = new List<ListBookAuthorResource>();
    }

    public class ListBookAuthorResource
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }
}
