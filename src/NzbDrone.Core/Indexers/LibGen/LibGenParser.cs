using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.LibGen
{
    public class LibGenParser : IParseIndexerResponse
    {
        public LibGenSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // LibGen mirrors may return various errors - return empty rather than throwing
                if (indexerResponse.HttpResponse.StatusCode is System.Net.HttpStatusCode.NotFound
                    or System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.ServiceUnavailable
                    or System.Net.HttpStatusCode.BadGateway
                    or System.Net.HttpStatusCode.GatewayTimeout
                    or System.Net.HttpStatusCode.Moved
                    or System.Net.HttpStatusCode.MovedPermanently
                    or System.Net.HttpStatusCode.Redirect
                    or System.Net.HttpStatusCode.RedirectKeepVerb
                    or System.Net.HttpStatusCode.RedirectMethod)
                {
                    return releases;
                }

                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            var (html, baseUrl) = ExtractContent(indexerResponse);

            var tables = Regex.Matches(html, @"<table.*?>.*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match table in tables)
            {
                ParseTable(table.Value, baseUrl, releases);
            }

            return releases;
        }

        private void ParseTable(string tableHtml, string baseUrl, List<ReleaseInfo> releases)
        {
            // Split rows
            var rowRegex = new Regex(@"<tr.*?>.*?</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var rows = rowRegex.Matches(tableHtml).Cast<Match>().Select(m => m.Value).ToList();

            if (rows.Count == 0)
            {
                return;
            }

            var headerIndex = rows.FindIndex(r => r.IndexOf("author", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                  r.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0);

            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (headerIndex >= 0)
            {
                var headerCells = GetCells(rows[headerIndex]);
                for (var i = 0; i < headerCells.Count; i++)
                {
                    var name = StripTags(headerCells[i]).ToLowerInvariant();
                    if (!headerMap.ContainsKey(name))
                    {
                        headerMap[name] = i;
                    }
                }
            }

            for (var i = headerIndex >= 0 ? headerIndex + 1 : 0; i < rows.Count; i++)
            {
                var rowHtml = rows[i];

                try
                {
                    var release = ParseRow(rowHtml, baseUrl, headerMap);
                    if (release != null)
                    {
                        releases.Add(release);
                    }
                }
                catch
                {
                    // ignore bad rows
                }
            }
        }

        private ReleaseInfo ParseRow(string rowHtml, string baseUrl, Dictionary<string, int> headerMap)
        {
            var cells = GetCells(rowHtml);

            var rowText = string.Join(" ", cells);

            // Try to find md5 hash (for Files view)
            var md5Match = Regex.Match(rowText, @"md5=([0-9a-fA-F]{32})", RegexOptions.IgnoreCase);
            
            // Try to find edition ID (for Editions view)
            var editionMatch = Regex.Match(rowHtml, @"edition\.php\?id=(\d+)", RegexOptions.IgnoreCase);
            
            // Need at least one identifier
            if (!md5Match.Success && !editionMatch.Success)
            {
                return null;
            }

            var md5 = md5Match.Success ? md5Match.Groups[1].Value : null;
            var editionId = editionMatch.Success ? editionMatch.Groups[1].Value : null;

            var title = "Unknown Title";
            string author = null;
            string doi = null;
            string downloadUrl = null;
            long size = 0;
            var extension = "PDF";

            // Try title/author/doi from mapped cells if available
            if (headerMap.TryGetValue("title", out var titleIndex) && titleIndex < cells.Count)
            {
                title = StripTags(cells[titleIndex]);
            }

            if (headerMap.TryGetValue("author", out var authorIndex) && authorIndex < cells.Count)
            {
                author = StripTags(cells[authorIndex]);
            }

            if (headerMap.TryGetValue("doi", out var doiIndex) && doiIndex < cells.Count)
            {
                doi = StripTags(cells[doiIndex]);
            }

            // Fallback: regex in row for title anchor (md5-based)
            if (title == "Unknown Title" && md5 != null)
            {
                var titleMatch = Regex.Match(rowHtml, @"href=""[^""]*md5=[0-9a-fA-F]{32}[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    title = StripTags(titleMatch.Groups[1].Value);
                }
            }

            // Fallback: regex for edition-based title
            if (title == "Unknown Title" && editionId != null)
            {
                var editionTitleMatch = Regex.Match(rowHtml, @"href=""edition\.php\?id=\d+""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (editionTitleMatch.Success)
                {
                    var titleText = editionTitleMatch.Groups[1].Value;
                    // Clean up the title - remove <i> tags and extra whitespace
                    title = StripTags(titleText).Trim();
                }
            }

            // Extract DOI from green font text if not already found
            if (string.IsNullOrWhiteSpace(doi))
            {
                var doiMatch = Regex.Match(rowHtml, @"DOI:\s*([0-9.]+/[^\s<]+)", RegexOptions.IgnoreCase);
                if (doiMatch.Success)
                {
                    doi = doiMatch.Groups[1].Value.Trim();
                }
            }

            // If author still unknown, try author column text even if not mapped
            if (author == null)
            {
                // Heuristic: the second cell often holds author(s)
                if (cells.Count > 1)
                {
                    author = StripTags(cells[1]);
                }
            }

            // Download link - try multiple patterns to support various LibGen mirrors
            if (md5 != null)
            {
                downloadUrl = FindDownloadUrl(rowHtml, baseUrl, md5);
            }
            else if (editionId != null)
            {
                // For edition-based results, construct download URL
                downloadUrl = $"{baseUrl}/ads.php?id={editionId}";
            }

            // Size - try multiple patterns
            size = FindSize(rowHtml);

            // Extension - try multiple patterns
            extension = FindExtension(rowHtml);

            if (downloadUrl == null)
            {
                return null;
            }

            var identifier = md5 ?? $"ed{editionId}";
            var release = new ReleaseInfo();
            release.Guid = $"LibGen-{identifier}";

            // Truncate author for display/filename to avoid PathTooLongException
            var displayAuthor = TruncateAuthors(author, 80);
            release.Title = $"{displayAuthor} - {title} ({extension})";

            // Keep full author list in Author field for matching
            release.Author = author;
            release.Book = title;
            release.Doi = DoiUtility.Normalize(doi);
            release.DownloadUrl = downloadUrl;
            release.InfoUrl = md5 != null ? $"{baseUrl}/book/index.php?md5={md5}" : $"{baseUrl}/edition.php?id={editionId}";
            release.Size = size;
            release.PublishDate = DateTime.UtcNow;
            release.DownloadProtocol = DownloadProtocol.Http;

            return release;
        }

        private List<string> GetCells(string rowHtml)
        {
            var cells = new List<string>();
            var cellRegex = new Regex(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var matches = cellRegex.Matches(rowHtml);
            foreach (Match match in matches)
            {
                cells.Add(WebUtility.HtmlDecode(match.Groups[1].Value));
            }

            return cells;
        }

        private string StripTags(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty).Trim();
        }

        private string TruncateAuthors(string author, int maxLength = 100)
        {
            if (string.IsNullOrWhiteSpace(author) || author.Length <= maxLength)
            {
                return author;
            }

            // Split by common author separators
            var separators = new[] { ";", " and ", ", " };
            var authors = new List<string>();

            foreach (var sep in separators)
            {
                if (author.Contains(sep))
                {
                    authors = author.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(a => a.Trim())
                                    .ToList();
                    break;
                }
            }

            if (authors.Count == 0)
            {
                // No separator found, truncate the single author string
                return author.Length > maxLength ? author.Substring(0, maxLength - 10) + " et al." : author;
            }

            // Take first author and add "et al."
            var firstAuthor = authors[0];

            // Remove "(author)" suffix if present
            firstAuthor = Regex.Replace(firstAuthor, @"\s*\(author\)\s*", " ").Trim();

            // If first author alone is too long, truncate it
            if (firstAuthor.Length > maxLength - 10)
            {
                firstAuthor = firstAuthor.Substring(0, maxLength - 10);
            }

            return $"{firstAuthor} et al.";
        }

        private long ParseSize(string sizeString)
        {
            var parts = sizeString.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1)
            {
                return 0;
            }

            if (double.TryParse(parts[0], out var number))
            {
                var multiplier = 1L;
                var unit = parts.Length > 1 ? parts[1] : sizeString;

                if (unit.StartsWith("G", StringComparison.OrdinalIgnoreCase) || unit.Contains("GB", StringComparison.OrdinalIgnoreCase))
                {
                    multiplier = 1024L * 1024 * 1024;
                }
                else if (unit.StartsWith("M", StringComparison.OrdinalIgnoreCase) || unit.Contains("MB", StringComparison.OrdinalIgnoreCase))
                {
                    multiplier = 1024 * 1024;
                }
                else if (unit.StartsWith("K", StringComparison.OrdinalIgnoreCase) || unit.Contains("KB", StringComparison.OrdinalIgnoreCase))
                {
                    multiplier = 1024;
                }

                return (long)(number * multiplier);
            }

            return 0;
        }

        private static (string Html, string BaseUrl) ExtractContent(IndexerResponse indexerResponse)
        {
            var content = indexerResponse.Content;
            var requestUri = new Uri(indexerResponse.HttpRequest.Url.FullUri);
            var baseUrl = $"{requestUri.Scheme}://{requestUri.Host}";

            // Unwrap FlareSolverr JSON responses if present
            if (content.TrimStart().StartsWith("{"))
            {
                try
                {
                    var json = JObject.Parse(content);
                    var solution = json["solution"];
                    var responseHtml = solution?["response"]?.ToString();
                    var solvedUrl = solution?["url"]?.ToString();

                    if (!responseHtml.IsNullOrWhiteSpace())
                    {
                        if (!solvedUrl.IsNullOrWhiteSpace())
                        {
                            var solvedUri = new Uri(solvedUrl);
                            baseUrl = $"{solvedUri.Scheme}://{solvedUri.Host}";
                        }

                        return (responseHtml, baseUrl);
                    }
                }
                catch
                {
                    // fall back to raw content
                }
            }

            return (content, baseUrl);
        }

        private static string FindDownloadUrl(string rowHtml, string baseUrl, string md5)
        {
            // Try multiple URL patterns to support various LibGen mirrors
            var patterns = new[]
            {
                // Relative URL patterns (libgen.li uses these) - MUST resolve against baseUrl
                @"href=['""]?(/ads\.php\?md5=[A-Fa-f0-9]{32})['""]?",
                @"href=['""]?(/get\.php\?md5=[A-Fa-f0-9]{32})['""]?",

                // Absolute URL patterns for external mirrors
                @"href=['""]?(https?://library\.lol/main/[^'"">\s]+)",
                @"href=['""]?(https?://randombook\.org/book/[A-Fa-f0-9]{32})",
                @"href=['""]?(https?://[^'"">\s]*annas-archive[^'"">\s]*md5/[A-Fa-f0-9]{32}[^'"">\s]*)",
                @"href=['""]?(https?://bookfi\.net/md5/[A-Fa-f0-9]{32})",

                // Generic absolute patterns
                @"href=['""]?(https?://[^'"">\s]+/ads\.php\?md5=[^'"">\s]+)",
                @"href=['""]?(https?://[^'"">\s]+/get\.php\?md5=[^'"">\s]+)",

                // Any link containing the specific md5 hash
                @"href=['""]?(https?://[^'"">\s]*" + Regex.Escape(md5) + @"[^'"">\s]*)",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(rowHtml, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var url = match.Groups[1].Value;

                    // Clean up any trailing quotes or brackets
                    url = Regex.Replace(url, @"['"">].*$", "");

                    // Resolve relative URLs against baseUrl
                    if (url.StartsWith("/"))
                    {
                        url = baseUrl.TrimEnd('/') + url;
                    }

                    return url;
                }
            }

            // Fallback: construct URL using base URL and md5
            if (md5.IsNotNullOrWhiteSpace())
            {
                return $"{baseUrl}/ads.php?md5={md5}";
            }

            return null;
        }

        private static long FindSize(string rowHtml)
        {
            // Try multiple size patterns
            var patterns = new[]
            {
                // libgen.li format: size inside anchor tag like <a href="/file.php?id=...">2 MB</a>
                @"<a[^>]*href=['""][^'""]*file\.php[^'""]*['""][^>]*>([\d.,]+)\s*(kB|KB|MB|GB|Mb|Gb|KiB|MiB|GiB)</a>",

                // Standard format with nowrap
                @"nowrap[^>]*>([\d.,]+)\s*([KMGT]?B)</td>",

                // Size in td cell with anchor
                @"<td[^>]*><[^>]*>([\d.,]+)\s*(kB|KB|MB|GB|Mb|Gb)</[^>]*></td>",

                // Without nowrap - plain td
                @"<td[^>]*>([\d.,]+)\s*(kB|KB|MB|GB|Mb|Gb|KiB|MiB|GiB)</td>",

                // Size anywhere in row with unit
                @"([\d.,]+)\s*(kB|KB|MB|GB|Mb|Gb|KiB|MiB|GiB)",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(rowHtml, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var numberStr = match.Groups[1].Value.Replace(",", ".");
                    var unit = match.Groups[2].Value;

                    if (double.TryParse(numberStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number))
                    {
                        var multiplier = 1L;
                        if (unit.StartsWith("G", StringComparison.OrdinalIgnoreCase))
                        {
                            multiplier = 1024L * 1024 * 1024;
                        }
                        else if (unit.StartsWith("M", StringComparison.OrdinalIgnoreCase))
                        {
                            multiplier = 1024 * 1024;
                        }
                        else if (unit.StartsWith("K", StringComparison.OrdinalIgnoreCase) || unit.StartsWith("k", StringComparison.OrdinalIgnoreCase))
                        {
                            multiplier = 1024;
                        }

                        return (long)(number * multiplier);
                    }
                }
            }

            return 0;
        }

        private static string FindExtension(string rowHtml)
        {
            // Try multiple extension patterns
            var patterns = new[]
            {
                @"<td[^>]*>\s*(pdf|epub|mobi|azw3|djvu|doc|docx|txt|rtf|chm|fb2)\s*</td>",
                @"\.(pdf|epub|mobi|azw3|djvu|doc|docx|txt|rtf|chm|fb2)(?:['""\s<>]|$)",
                @"(?:^|[\s,;:\[\(])(pdf|epub|mobi|azw3|djvu)(?:[\s,;:\]\)]|$)",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(rowHtml, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.ToUpperInvariant();
                }
            }

            return "PDF"; // Default assumption for academic content
        }
    }
}
