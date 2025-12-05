using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.HttpDownload
{
    public class HttpDownload : DownloadClientBase<HttpDownloadSettings>
    {
        private readonly IHttpClient _httpClient;
        private readonly IHttpDownloadItemRepository _repository;
        private readonly IIndexerStatusService _indexerStatusService;

        public HttpDownload(IHttpClient httpClient,
                            IHttpDownloadItemRepository repository,
                            IDiskProvider diskProvider,
                            IConfigService configService,
                            IDownloadClientStatusService downloadClientStatusService,
                            IRemotePathMappingService remotePathMappingService,
                            IIndexerStatusService indexerStatusService,
                            Logger logger)
            : base(configService, diskProvider, remotePathMappingService, logger)
        {
            _httpClient = httpClient;
            _repository = repository;
            _indexerStatusService = indexerStatusService;
        }

        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override string Name => "HttpDownload";

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var items = _repository.GetByDownloadClientId(Definition.Id);
            var clientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false);

            foreach (var item in items)
            {
                // Check if file exists and update status accordingly
                if (item.Status == DownloadItemStatus.Completed && !File.Exists(item.OutputPath))
                {
                    // File was removed/imported, mark for cleanup
                    continue;
                }

                yield return new DownloadClientItem
                {
                    DownloadClientInfo = clientInfo,
                    DownloadId = item.DownloadId,
                    Title = item.Title,
                    TotalSize = item.TotalSize,
                    RemainingSize = item.Status == DownloadItemStatus.Completed ? 0 : item.TotalSize,
                    Status = item.Status,
                    Message = item.Message,
                    OutputPath = new OsPath(item.OutputPath)
                };
            }
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            var dbItem = _repository.FindByDownloadId(item.DownloadId);
            if (dbItem != null)
            {
                _repository.Delete(dbItem);

                if (deleteData)
                {
                    DeleteItemData(item);
                }
            }
        }

        public override DownloadClientInfo GetStatus()
        {
            var info = new DownloadClientInfo
            {
                IsLocalhost = true,
                RemovesCompletedDownloads = false
            };

            if (!string.IsNullOrWhiteSpace(Settings.DownloadFolder))
            {
                info.OutputRootFolders.Add(new OsPath(Settings.DownloadFolder));
            }

            return info;
        }

        public override Task<string> Download(RemoteBook remoteBook, IIndexer indexer)
        {
            var release = remoteBook.Release;

            if (release == null || release.DownloadUrl.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Release is missing a download URL.");
            }

            var title = release.Title.IsNullOrWhiteSpace() ? release.Guid : release.Title;
            var filename = FileNameBuilder.CleanFileName(title) + ".pdf";
            var path = Path.Combine(Settings.DownloadFolder, filename);
            var downloadClientId = "HTTP-" + Guid.NewGuid().ToString("N");

            // Persist download to database
            var dbItem = new HttpDownloadItem
            {
                DownloadId = downloadClientId,
                Title = title,
                OutputPath = path,
                TotalSize = release.Size,
                Status = DownloadItemStatus.Downloading,
                DateAdded = DateTime.UtcNow,
                DownloadClientId = Definition.Id
            };

            _repository.Insert(dbItem);

            _logger.Info("Downloading {0} to {1}", release.DownloadUrl, path);

            // Capture indexer ID for error attribution
            var indexerId = remoteBook?.Release?.IndexerId ?? 0;

            // Fire and forget the download task so we return the ID immediately
            Task.Run(() =>
            {
                try
                {
                    _httpClient.DownloadFile(release.DownloadUrl, path, Settings.UserAgent, Settings.CustomHeaders);

                    _logger.Info("Successfully downloaded {0}", title);

                    // Update status in database
                    var item = _repository.FindByDownloadId(downloadClientId);
                    if (item != null)
                    {
                        item.Status = DownloadItemStatus.Completed;
                        _repository.Update(item);
                    }

                    // Record success for the indexer
                    if (indexerId > 0)
                    {
                        _indexerStatusService.RecordSuccess(indexerId, IndexerOperationType.Download);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to download {0}", release.DownloadUrl);

                    // Update status in database
                    var item = _repository.FindByDownloadId(downloadClientId);
                    if (item != null)
                    {
                        item.Status = DownloadItemStatus.Failed;
                        item.Message = ex.Message;
                        _repository.Update(item);
                    }

                    // Record failure for the indexer with detailed information
                    if (indexerId > 0)
                    {
                        try
                        {
                            _indexerStatusService.RecordFailure(indexerId);
                            var errorType = DetermineErrorType(ex);
                            var httpStatusCode = GetHttpStatusCode(ex);
                            _indexerStatusService.RecordFailure(indexerId, IndexerOperationType.Search, errorType, ex.Message ?? "HTTP download failed", httpStatusCode);
                        }
                        catch (Exception recordEx)
                        {
                            _logger.Debug(recordEx, "Failed to record indexer failure for indexer {0}", indexerId);
                        }
                    }
                }
            });

            return Task.FromResult(downloadClientId);
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            var failure = TestFolder(Settings.DownloadFolder, nameof(Settings.DownloadFolder));
            if (failure != null)
            {
                failures.Add(failure);
            }
        }

        private static IndexerErrorType DetermineErrorType(Exception ex)
        {
            return ex switch
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
            if (ex is NzbDrone.Common.Http.HttpException httpEx)
            {
                return (int?)httpEx.Response?.StatusCode;
            }

            return null;
        }
    }
}
