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

        public HttpDownload(IHttpClient httpClient,
                            IHttpDownloadItemRepository repository,
                            IDiskProvider diskProvider,
                            IConfigService configService,
                            IDownloadClientStatusService downloadClientStatusService,
                            IRemotePathMappingService remotePathMappingService,
                            Logger logger)
            : base(configService, diskProvider, remotePathMappingService, logger)
        {
            _httpClient = httpClient;
            _repository = repository;
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

            // Fire and forget the download task so we return the ID immediately
            Task.Run(() =>
            {
                try
                {
                    _httpClient.DownloadFile(release.DownloadUrl, path, Settings.UserAgent);

                    _logger.Info("Successfully downloaded {0}", title);

                    // Update status in database
                    var item = _repository.FindByDownloadId(downloadClientId);
                    if (item != null)
                    {
                        item.Status = DownloadItemStatus.Completed;
                        _repository.Update(item);
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
    }
}
