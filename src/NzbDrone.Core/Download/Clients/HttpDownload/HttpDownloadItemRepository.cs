using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download.Clients.HttpDownload
{
    public interface IHttpDownloadItemRepository : IBasicRepository<HttpDownloadItem>
    {
        List<HttpDownloadItem> GetByDownloadClientId(int downloadClientId);
        HttpDownloadItem FindByDownloadId(string downloadId);
        void DeleteByDownloadId(string downloadId);
    }

    public class HttpDownloadItemRepository : BasicRepository<HttpDownloadItem>, IHttpDownloadItemRepository
    {
        public HttpDownloadItemRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<HttpDownloadItem> GetByDownloadClientId(int downloadClientId)
        {
            return Query(x => x.DownloadClientId == downloadClientId).ToList();
        }

        public HttpDownloadItem FindByDownloadId(string downloadId)
        {
            return Query(x => x.DownloadId == downloadId).FirstOrDefault();
        }

        public void DeleteByDownloadId(string downloadId)
        {
            Delete(x => x.DownloadId == downloadId);
        }
    }
}
