using System;
using System.Reflection;
using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface IReadarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory Metadata { get; }
    }

    public class ReadarrCloudRequestBuilder : IReadarrCloudRequestBuilder
    {
        public ReadarrCloudRequestBuilder()
        {
            //TODO: Create Update Endpoint
            Services = new HttpRequestBuilder("https://readarr.servarr.com/v1/")
                .CreateFactory();

            var metadataUrl = "https://api.bookinfo.club/v1/{route}";

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
                System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "ReadarrCloudRequestBuilder.cs:13", message = "ReadarrCloudRequestBuilder constructor", data = new { metadataUrl = metadataUrl }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
            }
            catch { }

            // #endregion
            Metadata = new HttpRequestBuilder(metadataUrl)
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; }

        public IHttpRequestBuilderFactory Metadata { get; }
    }
}
