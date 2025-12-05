using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Common.Http
{
    public interface IFlareSolverrService
    {
        bool IsAvailable();
        Task<FlareSolverrResponse> SolveAsync(string url, int maxTimeout = 90000);
        Task<byte[]> DownloadFileAsync(string url, int maxTimeout = 90000);
    }

    public class FlareSolverrResponse
    {
        public string Url { get; set; }
        public string Response { get; set; }
        public string UserAgent { get; set; }
        public CookieContainer Cookies { get; set; }
    }

    public class FlareSolverrService : IFlareSolverrService
    {
        private readonly Logger _logger;
        private readonly string _flareSolverrUrl;
        private readonly System.Net.Http.HttpClient _httpClient;

        public FlareSolverrService(Logger logger)
        {
            _logger = logger;
            _flareSolverrUrl = Environment.GetEnvironmentVariable("FLARESOLVERR_URL") ?? "http://localhost:8191";
            _httpClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
        }

        public bool IsAvailable()
        {
            if (string.IsNullOrWhiteSpace(_flareSolverrUrl))
            {
                return false;
            }

            try
            {
                var testUrl = $"{_flareSolverrUrl.TrimEnd('/')}/v1";
                var payload = new { cmd = "request.get", url = "https://www.google.com", maxTimeout = 5000 };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = _httpClient.PostAsync(testUrl, content).GetAwaiter().GetResult();
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "FlareSolverr availability check failed");
                return false;
            }
        }

        public async Task<FlareSolverrResponse> SolveAsync(string url, int maxTimeout = 90000)
        {
            if (string.IsNullOrWhiteSpace(_flareSolverrUrl))
            {
                throw new InvalidOperationException("FlareSolverr URL is not configured");
            }

            var solverUrl = $"{_flareSolverrUrl.TrimEnd('/')}/v1";
            var payload = new
            {
                cmd = "request.get",
                url,
                maxTimeout
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            _logger.Debug("Requesting FlareSolverr to solve: {0}", url);

            var response = await _httpClient.PostAsync(solverUrl, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"FlareSolverr request failed with status {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            try
            {
                var json = JObject.Parse(responseContent);
                var status = json["status"]?.ToString();

                if (status != "ok")
                {
                    var message = json["message"]?.ToString() ?? "Unknown error";
                    throw new InvalidOperationException($"FlareSolverr error: {message}");
                }

                var solution = json["solution"];
                if (solution == null)
                {
                    throw new InvalidOperationException("FlareSolverr response missing solution");
                }

                var result = new FlareSolverrResponse
                {
                    Url = solution["url"]?.ToString(),
                    Response = solution["response"]?.ToString(),
                    UserAgent = solution["userAgent"]?.ToString()
                };

                // Parse cookies if present
                var cookies = solution["cookies"];
                if (cookies != null && cookies.Type == JTokenType.Array)
                {
                    result.Cookies = new CookieContainer();
                    foreach (var cookie in cookies)
                    {
                        var name = cookie["name"]?.ToString();
                        var value = cookie["value"]?.ToString();
                        var domain = cookie["domain"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(domain))
                        {
                            try
                            {
                                result.Cookies.Add(new Cookie(name, value, "/", domain));
                            }
                            catch (Exception ex)
                            {
                                _logger.Debug(ex, "Failed to add cookie from FlareSolverr: {0}", name);
                            }
                        }
                    }
                }

                _logger.Debug("FlareSolverr successfully solved challenge for: {0}", url);
                return result;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to parse FlareSolverr response: {0}", responseContent?.Substring(0, Math.Min(500, responseContent?.Length ?? 0)));
                throw new InvalidOperationException("Failed to parse FlareSolverr response", ex);
            }
        }

        public async Task<byte[]> DownloadFileAsync(string url, int maxTimeout = 90000)
        {
            if (string.IsNullOrWhiteSpace(_flareSolverrUrl))
            {
                throw new InvalidOperationException("FlareSolverr URL is not configured");
            }

            var solverUrl = $"{_flareSolverrUrl.TrimEnd('/')}/v1";
            var payload = new
            {
                cmd = "request.get",
                url,
                maxTimeout,
                returnOnlyCookies = false
            };

            var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            _logger.Debug("Requesting FlareSolverr to download: {0}", url);

            var response = await _httpClient.PostAsync(solverUrl, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"FlareSolverr request failed with status {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            try
            {
                var json = JObject.Parse(responseContent);
                var status = json["status"]?.ToString();

                if (status != "ok")
                {
                    var message = json["message"]?.ToString() ?? "Unknown error";
                    throw new InvalidOperationException($"FlareSolverr error: {message}");
                }

                var solution = json["solution"];
                if (solution == null)
                {
                    throw new InvalidOperationException("FlareSolverr response missing solution");
                }

                // For file downloads, we need the response body as bytes
                // FlareSolverr returns the response as a base64 string in some cases, or as text
                var responseText = solution["response"]?.ToString();
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    throw new InvalidOperationException("FlareSolverr response missing response body");
                }

                // Try to decode as base64 first (for binary content)
                try
                {
                    return Convert.FromBase64String(responseText);
                }
                catch
                {
                    // If not base64, treat as UTF-8 text
                    return Encoding.UTF8.GetBytes(responseText);
                }
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to parse FlareSolverr response: {0}", responseContent?.Substring(0, Math.Min(500, responseContent?.Length ?? 0)));
                throw new InvalidOperationException("Failed to parse FlareSolverr response", ex);
            }
        }
    }
}

