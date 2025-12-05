using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.TPL;

namespace NzbDrone.Common.Http
{
    public interface IHttpClient
    {
        HttpResponse Execute(HttpRequest request);
        void DownloadFile(string url, string fileName, string userAgent = null, string customHeaders = null);
        HttpResponse Get(HttpRequest request);
        HttpResponse<T> Get<T>(HttpRequest request)
            where T : new();
        HttpResponse Head(HttpRequest request);
        HttpResponse Post(HttpRequest request);
        HttpResponse<T> Post<T>(HttpRequest request)
            where T : new();

        Task<HttpResponse> ExecuteAsync(HttpRequest request);
        Task DownloadFileAsync(string url, string fileName, string userAgent = null, string customHeaders = null);
        Task<HttpResponse> GetAsync(HttpRequest request);
        Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new();
        Task<HttpResponse> HeadAsync(HttpRequest request);
        Task<HttpResponse> PostAsync(HttpRequest request);
        Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new();
    }

    public class HttpClient : IHttpClient
    {
        private const int MaxRedirects = 5;

        private readonly Logger _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly ICached<CookieContainer> _cookieContainerCache;
        private readonly List<IHttpRequestInterceptor> _requestInterceptors;
        private readonly IHttpDispatcher _httpDispatcher;
        private readonly IFlareSolverrService _flareSolverrService;

        public HttpClient(IEnumerable<IHttpRequestInterceptor> requestInterceptors,
            ICacheManager cacheManager,
            IRateLimitService rateLimitService,
            IHttpDispatcher httpDispatcher,
            Logger logger,
            IFlareSolverrService flareSolverrService = null)
        {
            _requestInterceptors = requestInterceptors.ToList();
            _rateLimitService = rateLimitService;
            _httpDispatcher = httpDispatcher;
            _logger = logger;
            _flareSolverrService = flareSolverrService;

            ServicePointManager.DefaultConnectionLimit = 12;
            _cookieContainerCache = cacheManager.GetCache<CookieContainer>(typeof(HttpClient));
        }

        public virtual async Task<HttpResponse> ExecuteAsync(HttpRequest request)
        {
            var cookieContainer = InitializeRequestCookies(request);

            var response = await ExecuteRequestAsync(request, cookieContainer);

            if (request.AllowAutoRedirect && response.HasHttpRedirect)
            {
                var autoRedirectChain = new List<string> { request.Url.ToString() };
                var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { request.Url.ToString() };

                do
                {
                    var location = response.Headers.GetSingleValue("Location");
                    if (location == null)
                    {
                        break;
                    }

                    request.Url += new HttpUri(location);
                    var currentUrl = request.Url.ToString();
                    autoRedirectChain.Add(currentUrl);

                    _logger.Trace("Redirected to {0}", request.Url);

                    // Detect redirect loops by checking if we've visited this URL before
                    if (visitedUrls.Contains(currentUrl))
                    {
                        throw new WebException($"Redirect loop detected. Visited URL twice: {currentUrl}. Redirect chain: {autoRedirectChain.Join(" -> ")}", WebExceptionStatus.ProtocolError);
                    }

                    visitedUrls.Add(currentUrl);

                    if (autoRedirectChain.Count > MaxRedirects)
                    {
                        throw new WebException($"Too many automatic redirections were attempted for {autoRedirectChain.Join(" -> ")}", WebExceptionStatus.ProtocolError);
                    }

                    // 302 or 303 should default to GET on redirect even if POST on original
                    if (RequestRequiresForceGet(response.StatusCode, response.Request.Method))
                    {
                        request.Method = HttpMethod.Get;
                        request.ContentData = null;
                        request.ContentSummary = null;
                    }

                    response = await ExecuteRequestAsync(request, cookieContainer);
                }
                while (response.HasHttpRedirect);
            }

            if (response.HasHttpRedirect && !RuntimeInfo.IsProduction)
            {
                _logger.Error("Server requested a redirect to [{0}] while in developer mode. Update the request URL to avoid this redirect.", response.Headers["Location"]);
            }

            if (!request.SuppressHttpError && response.HasHttpError && (request.SuppressHttpErrorStatusCodes == null || !request.SuppressHttpErrorStatusCodes.Contains(response.StatusCode)))
            {
                if (request.LogHttpError)
                {
                    _logger.Warn("HTTP Error - {0}", response);
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new TooManyRequestsException(request, response);
                }
                else
                {
                    throw new HttpException(request, response);
                }
            }

            return response;
        }

        public HttpResponse Execute(HttpRequest request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        private static bool RequestRequiresForceGet(HttpStatusCode statusCode, HttpMethod requestMethod)
        {
            return statusCode switch
            {
                HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.MultipleChoices => requestMethod == HttpMethod.Post,
                HttpStatusCode.SeeOther => requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head,
                _ => false,
            };
        }

        private async Task<HttpResponse> ExecuteRequestAsync(HttpRequest request, CookieContainer cookieContainer)
        {
            foreach (var interceptor in _requestInterceptors)
            {
                request = interceptor.PreRequest(request);
            }

            if (request.RateLimit != TimeSpan.Zero)
            {
                await _rateLimitService.WaitAndPulseAsync(request.Url.Host, request.RateLimitKey, request.RateLimit);
            }

            _logger.Trace(request);

            var stopWatch = Stopwatch.StartNew();

            var response = await _httpDispatcher.GetResponseAsync(request, cookieContainer);

            HandleResponseCookies(response, cookieContainer);

            stopWatch.Stop();

            _logger.Trace("{0} ({1} ms)", response, stopWatch.ElapsedMilliseconds);

            foreach (var interceptor in _requestInterceptors)
            {
                response = interceptor.PostResponse(response);
            }

            if (request.LogResponseContent && response.ResponseData != null)
            {
                _logger.Trace("Response content ({0} bytes): {1}", response.ResponseData.Length, response.Content);
            }

            return response;
        }

        private CookieContainer InitializeRequestCookies(HttpRequest request)
        {
            lock (_cookieContainerCache)
            {
                var sourceContainer = new CookieContainer();

                var presistentContainer = _cookieContainerCache.Get("container", () => new CookieContainer());
                var persistentCookies = presistentContainer.GetCookies((Uri)request.Url);
                sourceContainer.Add(persistentCookies);

                if (request.Cookies.Count != 0)
                {
                    foreach (var pair in request.Cookies)
                    {
                        Cookie cookie;
                        if (pair.Value == null)
                        {
                            cookie = new Cookie(pair.Key, "", "/")
                            {
                                Expires = DateTime.Now.AddDays(-1)
                            };
                        }
                        else
                        {
                            cookie = new Cookie(pair.Key, pair.Value, "/")
                            {
                                // Use Now rather than UtcNow to work around Mono cookie expiry bug.
                                // See https://gist.github.com/ta264/7822b1424f72e5b4c961
                                Expires = DateTime.Now.AddHours(1)
                            };
                        }

                        sourceContainer.Add((Uri)request.Url, cookie);

                        if (request.StoreRequestCookie)
                        {
                            presistentContainer.Add((Uri)request.Url, cookie);
                        }
                    }
                }

                return sourceContainer;
            }
        }

        private void HandleResponseCookies(HttpResponse response, CookieContainer container)
        {
            foreach (Cookie cookie in container.GetCookies((Uri)response.Request.Url))
            {
                cookie.Expired = true;
            }

            var cookieHeaders = response.GetCookieHeaders();

            if (cookieHeaders.Empty())
            {
                return;
            }

            AddCookiesToContainer(response.Request.Url, cookieHeaders, container);

            if (response.Request.StoreResponseCookie)
            {
                lock (_cookieContainerCache)
                {
                    var persistentCookieContainer = _cookieContainerCache.Get("container", () => new CookieContainer());

                    AddCookiesToContainer(response.Request.Url, cookieHeaders, persistentCookieContainer);
                }
            }
        }

        private void AddCookiesToContainer(HttpUri url, string[] cookieHeaders, CookieContainer container)
        {
            foreach (var cookieHeader in cookieHeaders)
            {
                try
                {
                    container.SetCookies((Uri)url, cookieHeader);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Invalid cookie in {0}", url);
                }
            }
        }

        public async Task DownloadFileAsync(string url, string fileName, string userAgent = null, string customHeaders = null)
        {
            var fileNamePart = fileName + ".part";

            try
            {
                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }

                _logger.Debug("Downloading [{0}] to [{1}]", url, fileName);

                var stopWatch = Stopwatch.StartNew();
                await using (var fileStream = new FileStream(fileNamePart, FileMode.Create, FileAccess.ReadWrite))
                {
                    var request = new HttpRequest(url);
                    request.AllowAutoRedirect = true;
                    request.ResponseStream = fileStream;
                    request.RequestTimeout = TimeSpan.FromSeconds(300);

                    // Set User-Agent
                    if (userAgent.IsNotNullOrWhiteSpace())
                    {
                        request.Headers.Set("User-Agent", userAgent);
                    }

                    // Add browser-like default headers to avoid bot detection
                    AddBrowserLikeHeaders(request, url);

                    // Parse and add custom headers
                    if (customHeaders.IsNotNullOrWhiteSpace())
                    {
                        ParseAndAddCustomHeaders(request, customHeaders);
                    }

                    var response = await GetAsync(request);
                    var cloudflareSolved = false;

                    // Check for Cloudflare JavaScript challenge page or other HTML responses
                    if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
                    {
                        // Read a portion of the response from the file stream to check for Cloudflare challenge
                        var currentPosition = fileStream.Position;
                        fileStream.Position = 0;
                        var fileLength = fileStream.Length;
                        var buffer = new byte[Math.Min(4096, (int)fileLength)];
                        var bytesRead = 0;

                        if (fileLength > 0)
                        {
                            bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                        }

                        fileStream.Position = currentPosition;

                        if (bytesRead > 0)
                        {
                            var contentPreview = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                            // Check for Cloudflare challenge indicators
                            if (contentPreview.Contains("Just a moment") ||
                                contentPreview.Contains("challenge-platform") ||
                                contentPreview.Contains("cf-challenge") ||
                                contentPreview.Contains("__cf_chl_opt") ||
                                contentPreview.Contains("Enable JavaScript and cookies to continue"))
                            {
                                // Try FlareSolverr if available
                                if (_flareSolverrService != null && _flareSolverrService.IsAvailable())
                                {
                                    _logger.Info("Cloudflare challenge detected, attempting to solve with FlareSolverr: {0}", url);
                                    try
                                    {
                                        // Use FlareSolverr to solve the challenge and get the file
                                        var flareResponse = await _flareSolverrService.SolveAsync(url, 90000);

                                        // FlareSolverr solved the challenge - now download the file using cookies/headers from FlareSolverr
                                        // Use the final URL from FlareSolverr (after redirects) or the original URL
                                        var downloadUrl = !string.IsNullOrWhiteSpace(flareResponse.Url) ? flareResponse.Url : url;
                                        _logger.Debug("Downloading file from FlareSolverr-solved URL: {0}", downloadUrl);

                                        // Retry download from the solved URL with cookies
                                        fileStream.SetLength(0);
                                        fileStream.Position = 0;

                                        var retryRequest = new HttpRequest(downloadUrl);
                                        retryRequest.AllowAutoRedirect = true;
                                        retryRequest.ResponseStream = fileStream;
                                        retryRequest.RequestTimeout = TimeSpan.FromSeconds(300);

                                        // Use the User-Agent from FlareSolverr if available
                                        if (!string.IsNullOrWhiteSpace(flareResponse.UserAgent))
                                        {
                                            retryRequest.Headers.Set("User-Agent", flareResponse.UserAgent);
                                        }
                                        else if (userAgent.IsNotNullOrWhiteSpace())
                                        {
                                            retryRequest.Headers.Set("User-Agent", userAgent);
                                        }

                                        // Add cookies from FlareSolverr
                                        if (flareResponse.Cookies != null)
                                        {
                                            try
                                            {
                                                var downloadUri = new Uri(downloadUrl);
                                                var cookieHeader = flareResponse.Cookies.GetCookieHeader(downloadUri);
                                                if (!string.IsNullOrWhiteSpace(cookieHeader))
                                                {
                                                    retryRequest.Headers.Set("Cookie", cookieHeader);
                                                    _logger.Debug("Added {0} cookies from FlareSolverr", flareResponse.Cookies.Count);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.Debug(ex, "Failed to add cookies from FlareSolverr");
                                            }
                                        }

                                        AddBrowserLikeHeaders(retryRequest, downloadUrl);

                                        if (customHeaders.IsNotNullOrWhiteSpace())
                                        {
                                            ParseAndAddCustomHeaders(retryRequest, customHeaders);
                                        }

                                        var retryResponse = await GetAsync(retryRequest);

                                        // Verify the downloaded content is actually a file (not HTML)
                                        fileStream.Position = 0;
                                        var retryFileLength = fileStream.Length;
                                        var retryBuffer = new byte[Math.Min(4096, (int)retryFileLength)];
                                        var retryBytesRead = 0;

                                        if (retryFileLength > 0)
                                        {
                                            retryBytesRead = await fileStream.ReadAsync(retryBuffer, 0, retryBuffer.Length);
                                        }

                                        if (retryBytesRead > 0)
                                        {
                                            // Check if it's a PDF file (starts with %PDF)
                                            var isPdf = retryBytesRead >= 4 && 
                                                       retryBuffer[0] == 0x25 && // %
                                                       retryBuffer[1] == 0x50 && // P
                                                       retryBuffer[2] == 0x44 && // D
                                                       retryBuffer[3] == 0x46;   // F

                                            if (isPdf)
                                            {
                                                // Success - it's a PDF file
                                                _logger.Info("Successfully downloaded PDF file using FlareSolverr: {0}", downloadUrl);
                                                cloudflareSolved = true;
                                            }
                                            else
                                            {
                                                // Check if it's still HTML
                                                var retryContentPreview = System.Text.Encoding.UTF8.GetString(retryBuffer, 0, retryBytesRead);
                                                if (retryContentPreview.Contains("Just a moment") ||
                                                    retryContentPreview.Contains("challenge-platform") ||
                                                    retryContentPreview.Contains("<!DOCTYPE html") ||
                                                    retryContentPreview.Contains("<html"))
                                                {
                                                    throw new HttpException(retryRequest, retryResponse, "Cloudflare challenge persists even after FlareSolverr attempt. The site may require additional authentication or manual intervention.");
                                                }
                                                else
                                                {
                                                    // Might be a different file type or valid content
                                                    _logger.Info("Downloaded file using FlareSolverr (content type may differ): {0}", downloadUrl);
                                                    cloudflareSolved = true;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            throw new HttpException(retryRequest, retryResponse, "FlareSolverr solved challenge but no content was downloaded.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.Warn(ex, "FlareSolverr failed to solve Cloudflare challenge for: {0}", url);
                                        throw new HttpException(request, response, $"Cloudflare JavaScript challenge detected and FlareSolverr failed to solve it: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    throw new HttpException(request, response, "Cloudflare JavaScript challenge detected. FlareSolverr is not available or not configured. Set FLARESOLVERR_URL environment variable to enable automatic challenge solving.");
                                }
                            }
                        }

                        if (!cloudflareSolved)
                        {
                            var errorMsg = "Site responded with html content instead of expected file.";
                            if (_flareSolverrService == null || !_flareSolverrService.IsAvailable())
                            {
                                errorMsg += " FlareSolverr is not available. Set FLARESOLVERR_URL environment variable to enable automatic Cloudflare challenge solving.";
                            }
                            else
                            {
                                errorMsg += " This may be a Cloudflare challenge that FlareSolverr could not solve, or the server is returning an error page.";
                            }
                            throw new HttpException(request, response, errorMsg);
                        }
                    }
                }

                stopWatch.Stop();

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                File.Move(fileNamePart, fileName);
                _logger.Debug("Downloading Completed. took {0:0}s", stopWatch.Elapsed.Seconds);
            }
            finally
            {
                if (File.Exists(fileNamePart))
                {
                    File.Delete(fileNamePart);
                }
            }
        }

        public void DownloadFile(string url, string fileName, string userAgent = null, string customHeaders = null)
        {
            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development#the-thread-pool-hack
            Task.Run(() => DownloadFileAsync(url, fileName, userAgent, customHeaders)).GetAwaiter().GetResult();
        }

        private void AddBrowserLikeHeaders(HttpRequest request, string url)
        {
            try
            {
                var uri = new Uri(url);

                // Set Accept header for PDF downloads
                if (!request.Headers.ContainsKey("Accept"))
                {
                    request.Headers.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,application/pdf,*/*;q=0.8";
                }

                // Set Accept-Language
                if (!request.Headers.ContainsKey("Accept-Language"))
                {
                    request.Headers.Set("Accept-Language", "en-US,en;q=0.9");
                }

                // Set Accept-Encoding
                if (!request.Headers.ContainsKey("Accept-Encoding"))
                {
                    request.Headers.Set("Accept-Encoding", "gzip, deflate, br");
                }

                // Set Referer to the same domain to appear as if navigating from the site
                if (!request.Headers.ContainsKey("Referer"))
                {
                    var referer = $"{uri.Scheme}://{uri.Host}/";
                    request.Headers.Set("Referer", referer);
                }

                // Set DNT (Do Not Track) header
                if (!request.Headers.ContainsKey("DNT"))
                {
                    request.Headers.Set("DNT", "1");
                }

                // Set Sec-Fetch headers for modern browsers
                if (!request.Headers.ContainsKey("Sec-Fetch-Dest"))
                {
                    request.Headers.Set("Sec-Fetch-Dest", "document");
                }

                if (!request.Headers.ContainsKey("Sec-Fetch-Mode"))
                {
                    request.Headers.Set("Sec-Fetch-Mode", "navigate");
                }

                if (!request.Headers.ContainsKey("Sec-Fetch-Site"))
                {
                    request.Headers.Set("Sec-Fetch-Site", "none");
                }

                if (!request.Headers.ContainsKey("Sec-Fetch-User"))
                {
                    request.Headers.Set("Sec-Fetch-User", "?1");
                }

                // Set Upgrade-Insecure-Requests
                if (!request.Headers.ContainsKey("Upgrade-Insecure-Requests"))
                {
                    request.Headers.Set("Upgrade-Insecure-Requests", "1");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to add browser-like headers for URL: {0}", url);
            }
        }

        private void ParseAndAddCustomHeaders(HttpRequest request, string customHeaders)
        {
            try
            {
                if (customHeaders.IsNullOrWhiteSpace())
                {
                    return;
                }

                // Parse format: Header1:Value1,Header2:Value2
                var headerPairs = customHeaders.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in headerPairs)
                {
                    var parts = pair.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var headerName = parts[0].Trim();
                        var headerValue = parts[1].Trim();

                        if (headerName.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                        {
                            // User-Agent is handled separately, skip it here
                            continue;
                        }

                        request.Headers.Set(headerName, headerValue);
                        _logger.Debug("Added custom header: {0} = {1}", headerName, headerValue);
                    }
                    else
                    {
                        _logger.Warn("Invalid custom header format (expected 'Header:Value'): {0}", pair);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to parse custom headers: {0}", customHeaders);
            }
        }

        public Task<HttpResponse> GetAsync(HttpRequest request)
        {
            request.Method = HttpMethod.Get;
            return ExecuteAsync(request);
        }

        public HttpResponse Get(HttpRequest request)
        {
            return Task.Run(() => GetAsync(request)).GetAwaiter().GetResult();
        }

        public async Task<HttpResponse<T>> GetAsync<T>(HttpRequest request)
            where T : new()
        {
            var response = await GetAsync(request);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Get<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => GetAsync<T>(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> HeadAsync(HttpRequest request)
        {
            request.Method = HttpMethod.Head;
            return ExecuteAsync(request);
        }

        public HttpResponse Head(HttpRequest request)
        {
            return Task.Run(() => HeadAsync(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> PostAsync(HttpRequest request)
        {
            request.Method = HttpMethod.Post;
            return ExecuteAsync(request);
        }

        public HttpResponse Post(HttpRequest request)
        {
            return Task.Run(() => PostAsync(request)).GetAwaiter().GetResult();
        }

        public async Task<HttpResponse<T>> PostAsync<T>(HttpRequest request)
            where T : new()
        {
            var response = await PostAsync(request);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Post<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => PostAsync<T>(request)).GetAwaiter().GetResult();
        }

        private void CheckResponseContentType(HttpResponse response)
        {
            if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
            {
                throw new UnexpectedHtmlContentException(response);
            }
        }
    }
}
