using System;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download
{
    public interface IDownloadService
    {
        Task DownloadReport(RemoteBook remoteBook, int? downloadClientId);
    }

    public class DownloadService : IDownloadService
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IDownloadClientStatusService _downloadClientStatusService;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISeedConfigProvider _seedConfigProvider;
        private readonly Logger _logger;

        public DownloadService(IProvideDownloadClient downloadClientProvider,
                               IDownloadClientStatusService downloadClientStatusService,
                               IIndexerFactory indexerFactory,
                               IIndexerStatusService indexerStatusService,
                               IRateLimitService rateLimitService,
                               IEventAggregator eventAggregator,
                               ISeedConfigProvider seedConfigProvider,
                               Logger logger)
        {
            _downloadClientProvider = downloadClientProvider;
            _downloadClientStatusService = downloadClientStatusService;
            _indexerFactory = indexerFactory;
            _indexerStatusService = indexerStatusService;
            _rateLimitService = rateLimitService;
            _eventAggregator = eventAggregator;
            _seedConfigProvider = seedConfigProvider;
            _logger = logger;
        }

        public async Task DownloadReport(RemoteBook remoteBook, int? downloadClientId)
        {
            var filterBlockedClients = remoteBook.Release.PendingReleaseReason == PendingReleaseReason.DownloadClientUnavailable;

            var tags = remoteBook.Author?.Tags;

            var downloadClient = downloadClientId.HasValue
                ? _downloadClientProvider.Get(downloadClientId.Value)
                : _downloadClientProvider.GetDownloadClient(remoteBook.Release.DownloadProtocol, remoteBook.Release.IndexerId, filterBlockedClients, tags);

            await DownloadReport(remoteBook, downloadClient);
        }

        private async Task DownloadReport(RemoteBook remoteBook, IDownloadClient downloadClient)
        {
            Ensure.That(remoteBook.Author, () => remoteBook.Author).IsNotNull();
            Ensure.That(remoteBook.Books, () => remoteBook.Books).HasItems();

            var downloadTitle = remoteBook.Release.Title;

            if (downloadClient == null)
            {
                throw new DownloadClientUnavailableException($"{remoteBook.Release.DownloadProtocol} Download client isn't configured yet");
            }

            // Get the seed configuration for this release.
            remoteBook.SeedConfiguration = _seedConfigProvider.GetSeedConfiguration(remoteBook);

            // Limit grabs to 2 per second.
            if (remoteBook.Release.DownloadUrl.IsNotNullOrWhiteSpace() && !remoteBook.Release.DownloadUrl.StartsWith("magnet:"))
            {
                var url = new HttpUri(remoteBook.Release.DownloadUrl);
                await _rateLimitService.WaitAndPulseAsync(url.Host, TimeSpan.FromSeconds(2));
            }

            IIndexer indexer = null;

            if (remoteBook.Release.IndexerId > 0)
            {
                indexer = _indexerFactory.GetInstance(_indexerFactory.Get(remoteBook.Release.IndexerId));
            }

            string downloadClientId;
            try
            {
                downloadClientId = await downloadClient.Download(remoteBook, indexer);
                _downloadClientStatusService.RecordSuccess(downloadClient.Definition.Id);
                _indexerStatusService.RecordSuccess(remoteBook.Release.IndexerId, IndexerOperationType.Download);
            }
            catch (ReleaseUnavailableException)
            {
                _logger.Trace("Release {0} no longer available on indexer.", remoteBook);
                throw;
            }
            catch (ReleaseBlockedException)
            {
                _logger.Trace("Release {0} previously added to blocklist, not sending to download client again.", remoteBook);
                throw;
            }
            catch (DownloadClientRejectedReleaseException)
            {
                _logger.Trace("Release {0} rejected by download client, possible duplicate.", remoteBook);
                throw;
            }
            catch (ReleaseDownloadException ex)
            {
                if (ex.InnerException is RequestLimitReachedException http429)
                {
                    _indexerStatusService.RecordFailure(remoteBook.Release.IndexerId, http429.RetryAfter);
                    _indexerStatusService.RecordFailure(remoteBook.Release.IndexerId, IndexerOperationType.Search, IndexerErrorType.RateLimit, ex.Message ?? http429.Message);
                }
                else
                {
                    _indexerStatusService.RecordFailure(remoteBook.Release.IndexerId);
                    var errorType = DetermineErrorType(ex);
                    var httpStatusCode = GetHttpStatusCode(ex);
                    _indexerStatusService.RecordFailure(remoteBook.Release.IndexerId, IndexerOperationType.Search, errorType, ex.Message ?? ex.InnerException?.Message ?? "Download failed", httpStatusCode);
                }

                throw;
            }

            var bookGrabbedEvent = new BookGrabbedEvent(remoteBook);
            bookGrabbedEvent.DownloadClient = downloadClient.Name;
            bookGrabbedEvent.DownloadClientId = downloadClient.Definition.Id;
            bookGrabbedEvent.DownloadClientName = downloadClient.Definition.Name;

            if (downloadClientId.IsNotNullOrWhiteSpace())
            {
                bookGrabbedEvent.DownloadId = downloadClientId;
            }

            _logger.ProgressInfo("Report sent to {0} from indexer {1}. {2}", downloadClient.Definition.Name, remoteBook.Release.Indexer, downloadTitle);
            _eventAggregator.PublishEvent(bookGrabbedEvent);
        }

        private static IndexerErrorType DetermineErrorType(Exception ex)
        {
            var innerEx = ex.InnerException ?? ex;
            return innerEx switch
            {
                System.Net.Http.HttpRequestException => IndexerErrorType.ConnectionFailure,
                System.Net.WebException webEx when webEx.Status == System.Net.WebExceptionStatus.Timeout => IndexerErrorType.Timeout,
                System.Net.WebException => IndexerErrorType.ConnectionFailure,
                System.Threading.Tasks.TaskCanceledException => IndexerErrorType.Timeout,
                NzbDrone.Common.Http.HttpException httpEx when httpEx.Response?.StatusCode == System.Net.HttpStatusCode.Unauthorized || httpEx.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden => IndexerErrorType.AuthError,
                NzbDrone.Common.Http.HttpException => IndexerErrorType.HttpError,
                NzbDrone.Core.Indexers.Exceptions.RequestLimitReachedException => IndexerErrorType.RateLimit,
                NzbDrone.Core.Http.CloudFlare.CloudFlareCaptchaException => IndexerErrorType.CloudflareCaptcha,
                _ => IndexerErrorType.Unknown
            };
        }

        private static int? GetHttpStatusCode(Exception ex)
        {
            var innerEx = ex.InnerException ?? ex;
            if (innerEx is NzbDrone.Common.Http.HttpException httpEx)
            {
                return (int?)httpEx.Response?.StatusCode;
            }

            return null;
        }
    }
}
