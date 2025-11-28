// Quick Sci-Hub search tester (not part of the main build).
// Usage: dotnet run --project _temp/scihub_test.csproj -- 10.1016/j.jcrc.2021.09.023

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

var query = args.Length > 0 ? args[0] : "10.1016/j.jcrc.2021.09.023";
var mirrors = new[]
{
    "https://sci-hub.se",
    "https://sci-hub.ru",
    "https://sci-hub.st"
};

var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(15)
};
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

var variants = BuildQueryVariants(query);

foreach (var mirror in mirrors)
{
    foreach (var variant in variants)
    {
        var url = $"{mirror.TrimEnd('/')}/{variant}";
        Console.WriteLine($"[TRY] {url}");

        try
        {
            var html = await client.GetStringAsync(url);
            var pdf = ExtractPdfUrl(html, url);
            if (pdf != null)
            {
                Console.WriteLine($"[OK] PDF: {pdf}");
                return;
            }

            Console.WriteLine("[WARN] No PDF link found in response.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {ex.GetType().Name}: {ex.Message}");
        }
    }
}

Console.WriteLine("Done. No PDF found.");

static IEnumerable<string> BuildQueryVariants(string query)
{
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var norm = Normalize(query);

    if (!string.IsNullOrWhiteSpace(norm))
    {
        set.Add(norm);

        if (norm.StartsWith("10.", StringComparison.OrdinalIgnoreCase))
        {
            set.Add($"https://doi.org/{norm}");
        }
    }

    set.Add(query.Trim());
    return set;
}

static string Normalize(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var trimmed = value.Trim();

    if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        var idx = trimmed.IndexOf("doi.org/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            trimmed = trimmed[(idx + "doi.org/".Length)..];
        }
    }

    if (trimmed.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
    {
        trimmed = trimmed[4..];
    }

    return trimmed;
}

static string ExtractPdfUrl(string html, string requestUrl)
{
    var match = Regex.Match(html, @"src=""([^""]+)""\s*id=""pdf""", RegexOptions.IgnoreCase);
    if (!match.Success)
    {
        return null;
    }

    var pdf = match.Groups[1].Value;

    if (pdf.StartsWith("//"))
    {
        pdf = "https:" + pdf;
    }
    else if (pdf.StartsWith("/"))
    {
        var uri = new Uri(requestUrl);
        pdf = $"{uri.Scheme}://{uri.Host}{pdf}";
    }

    return pdf;
}
