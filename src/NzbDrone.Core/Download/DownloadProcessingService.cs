using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download
{
    public class DownloadProcessingService : IExecute<ProcessMonitoredDownloadsCommand>
    {
        private readonly IConfigService _configService;
        private readonly ICompletedDownloadService _completedDownloadService;
        private readonly IFailedDownloadService _failedDownloadService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public DownloadProcessingService(IConfigService configService,
                                         ICompletedDownloadService completedDownloadService,
                                         IFailedDownloadService failedDownloadService,
                                         ITrackedDownloadService trackedDownloadService,
                                         IEventAggregator eventAggregator,
                                         Logger logger)
        {
            _configService = configService;
            _completedDownloadService = completedDownloadService;
            _failedDownloadService = failedDownloadService;
            _trackedDownloadService = trackedDownloadService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        private void RemoveCompletedDownloads()
        {
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                                                          .Where(t => !t.DownloadItem.Removed && t.DownloadItem.CanBeRemoved && t.State == TrackedDownloadState.Imported)
                                                          .ToList();

            foreach (var trackedDownload in trackedDownloads)
            {
                _eventAggregator.PublishEvent(new DownloadCanBeRemovedEvent(trackedDownload));
            }
        }

        public void Execute(ProcessMonitoredDownloadsCommand message)
        {
            // #region agent log
            try
            {
                var logPath = "/workspace/.cursor/debug.log";
                System.IO.Directory.CreateDirectory("/workspace/.cursor");
                System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "import-debug", hypothesisId = "A", location = "DownloadProcessingService.cs:47", message = "ProcessMonitoredDownloadsCommand executed", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
            }
            catch (System.Exception ex)
            {
                _logger.Debug("Failed to write debug log: {0}", ex.Message);
            }

            // #endregion
            var enableCompletedDownloadHandling = _configService.EnableCompletedDownloadHandling;
            // #region agent log
            try
            {
                var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                if (!System.IO.File.Exists(logPath))
                {
                    logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                    logPath = System.IO.Path.GetFullPath(logPath);
                }
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "import-debug", hypothesisId = "A", location = "DownloadProcessingService.cs:49", message = "EnableCompletedDownloadHandling check", data = new { enabled = enableCompletedDownloadHandling }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
            }
            catch { }

            // #endregion
            var trackedDownloads = _trackedDownloadService.GetTrackedDownloads()
                                                          .Where(t => t.IsTrackable)
                                                          .ToList();
            // #region agent log
            try
            {
                var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                if (!System.IO.File.Exists(logPath))
                {
                    logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                    logPath = System.IO.Path.GetFullPath(logPath);
                }
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "import-debug", hypothesisId = "A", location = "DownloadProcessingService.cs:51", message = "Tracked downloads retrieved", data = new { count = trackedDownloads.Count, states = trackedDownloads.Select(t => new { title = t.DownloadItem.Title, state = t.State.ToString(), status = t.DownloadItem.Status.ToString(), canMove = t.DownloadItem.CanMoveFiles, canRemove = t.DownloadItem.CanBeRemoved }).ToList() }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
            }
            catch { }

            // #endregion
            foreach (var trackedDownload in trackedDownloads)
            {
                try
                {
                    // #region agent log
                    try
                    {
                        var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                        if (!System.IO.File.Exists(logPath))
                        {
                            logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                            logPath = System.IO.Path.GetFullPath(logPath);
                        }
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                        System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "import-debug", hypothesisId = "B", location = "DownloadProcessingService.cs:54", message = "Processing tracked download", data = new { title = trackedDownload.DownloadItem.Title, state = trackedDownload.State.ToString(), status = trackedDownload.DownloadItem.Status.ToString() }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                    }
                    catch { }

                    // #endregion
                    if (trackedDownload.State == TrackedDownloadState.DownloadFailedPending)
                    {
                        _failedDownloadService.ProcessFailed(trackedDownload);
                    }
                    else if (enableCompletedDownloadHandling && (trackedDownload.State == TrackedDownloadState.ImportPending || trackedDownload.State == TrackedDownloadState.ImportFailed))
                    {
                        // #region agent log
                        try
                        {
                            var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                            if (!System.IO.File.Exists(logPath))
                            {
                                logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                                logPath = System.IO.Path.GetFullPath(logPath);
                            }
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                            System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "import-debug", hypothesisId = "B", location = "DownloadProcessingService.cs:62", message = "Calling Import for ImportPending/ImportFailed download", data = new { title = trackedDownload.DownloadItem.Title, state = trackedDownload.State.ToString(), outputPath = trackedDownload.ImportItem != null ? trackedDownload.ImportItem.OutputPath.FullPath : "null" }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                        }
                        catch { }

                        // #endregion
                        // Reset state to ImportPending to allow retry
                        if (trackedDownload.State == TrackedDownloadState.ImportFailed)
                        {
                            trackedDownload.State = TrackedDownloadState.ImportPending;
                        }
                        _completedDownloadService.Import(trackedDownload);
                    }
                }
                catch (Exception e)
                {
                    // #region agent log
                    try
                    {
                        var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                        if (!System.IO.File.Exists(logPath))
                        {
                            logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                            logPath = System.IO.Path.GetFullPath(logPath);
                        }
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                        System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "import-debug", hypothesisId = "C", location = "DownloadProcessingService.cs:67", message = "Exception processing download", data = new { title = trackedDownload.DownloadItem.Title, exception = e.Message, stackTrace = e.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                    }
                    catch { }

                    // #endregion
                    _logger.Debug(e, "Failed to process download: {0}", trackedDownload.DownloadItem.Title);
                }
            }

            // Imported downloads are no longer trackable so process them after processing trackable downloads
            RemoveCompletedDownloads();

            _eventAggregator.PublishEvent(new DownloadsProcessedEvent());
        }
    }
}
