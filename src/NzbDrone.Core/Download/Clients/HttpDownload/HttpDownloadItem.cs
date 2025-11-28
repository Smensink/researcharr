using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Download.Clients.HttpDownload
{
    public class HttpDownloadItem : ModelBase
    {
        public string DownloadId { get; set; }
        public string Title { get; set; }
        public string OutputPath { get; set; }
        public long TotalSize { get; set; }
        public DownloadItemStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime DateAdded { get; set; }
        public int DownloadClientId { get; set; }
    }
}
