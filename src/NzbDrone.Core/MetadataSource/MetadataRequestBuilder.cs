using System;
using System.Reflection;
using System.Text.Json;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataRequestBuilder
    {
        IHttpRequestBuilderFactory GetRequestBuilder();
    }

    public class MetadataRequestBuilder : IMetadataRequestBuilder
    {
        private readonly IConfigService _configService;

        private readonly IReadarrCloudRequestBuilder _defaultRequestFactory;

        public MetadataRequestBuilder(IConfigService configService, IReadarrCloudRequestBuilder defaultRequestBuilder)
        {
            _configService = configService;
            _defaultRequestFactory = defaultRequestBuilder;
        }

        public IHttpRequestBuilderFactory GetRequestBuilder()
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
                System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "MetadataRequestBuilder.cs:25", message = "GetRequestBuilder entry", data = new { metadataSource = _configService.MetadataSource ?? "null" }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
            }
            catch { }

            // #endregion
            if (_configService.MetadataSource.IsNotNullOrWhiteSpace())
            {
                var customUrl = _configService.MetadataSource.TrimEnd("/") + "/{route}";
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
                    System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "MetadataRequestBuilder.cs:29", message = "Using custom metadata source", data = new { url = customUrl }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                }
                catch { }

                // #endregion
                return new HttpRequestBuilder(customUrl).KeepAlive().CreateFactory();
            }
            else
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
                    System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "MetadataRequestBuilder.cs:33", message = "Using default metadata source", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                }
                catch { }

                // #endregion
                return _defaultRequestFactory.Metadata;
            }
        }
    }
}
