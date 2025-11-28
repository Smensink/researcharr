using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public class ReleaseInfo
{
    public string Guid { get; set; }
    public string Title { get; set; }
    public string Book { get; set; }
    public string Author { get; set; }
    public string DownloadUrl { get; set; }
    public string InfoUrl { get; set; }
    public string Container { get; set; }
    public List<int> Categories { get; set; }
    public long Size { get; set; }
    public DateTime PublishDate { get; set; }
    public string DownloadProtocol { get; set; }
}

public class ArxivParser
{
    public List<ReleaseInfo> ParseResponse(string content)
    {
        var releases = new List<ReleaseInfo>();
        var xml = XDocument.Parse(content);
        var ns = xml.Root.GetDefaultNamespace();

        foreach (var entry in xml.Descendants(ns + "entry"))
        {
            try
            {
                var id = entry.Element(ns + "id")?.Value;
                var title = entry.Element(ns + "title")?.Value;
                var summary = entry.Element(ns + "summary")?.Value;
                var published = entry.Element(ns + "published")?.Value; // Atom uses 'updated' or 'published'

                // Atom feed from arxiv uses 'updated' usually, but let's check the content.
                // The curl output showed <updated>2025-11-23T05:00:04.556492+00:00</updated> at feed level.
                // Let's see what entries have.
                
                // ArxivParser.cs uses:
                // var published = entry.Element(ns + "published")?.Value;
                
                var authors = entry.Elements(ns + "author").Select(a => a.Element(ns + "name")?.Value).Where(n => n != null).ToList();
                var author = authors.FirstOrDefault() ?? "Unknown Author";

                // Find PDF link
                var pdfLink = entry.Elements(ns + "link")
                    .FirstOrDefault(l => (string)l.Attribute("title") == "pdf" || (string)l.Attribute("type") == "application/pdf")?
                    .Attribute("href")?.Value;

                if (string.IsNullOrEmpty(pdfLink))
                {
                    Console.WriteLine($"Skipping entry {id}: No PDF link");
                    continue;
                }

                var release = new ReleaseInfo();
                release.Guid = id ?? $"Arxiv-{Guid.NewGuid()}";
                release.Title = $"{author} - {title}";
                release.Book = title;
                release.Author = author;
                release.DownloadUrl = pdfLink;
                release.InfoUrl = id; 
                release.Container = "PDF";
                release.Categories = new List<int> { 8000 }; 
                release.Size = 0; 

                if (DateTime.TryParse(published, out var pubDate))
                {
                    release.PublishDate = pubDate;
                }
                else
                {
                     // Try 'updated' if published is missing
                     var updated = entry.Element(ns + "updated")?.Value;
                     if (DateTime.TryParse(updated, out var upDate))
                     {
                         release.PublishDate = upDate;
                     }
                     else
                     {
                        release.PublishDate = DateTime.UtcNow;
                     }
                }

                release.DownloadProtocol = "Http";
                releases.Add(release);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing entry: {ex.Message}");
            }
        }

        return releases;
    }
}

public class Program
{
    public static void Main()
    {
        string atomXml = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns:arxiv=""http://arxiv.org/schemas/atom"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns=""http://www.w3.org/2005/Atom"" xml:lang=""en-us"">
  <id>http://rss.arxiv.org/atom/cs</id>
  <title>cs updates on arXiv.org</title>
  <updated>2025-11-23T05:00:04.556492+00:00</updated>
  <link href=""http://rss.arxiv.org/atom/cs"" rel=""self"" type=""application/atom+xml""/>
  <subtitle>cs updates on the arXiv.org e-print archive.</subtitle>
  <entry>
    <id>http://arxiv.org/abs/2511.08685</id>
    <updated>2025-11-23T05:00:04+00:00</updated>
    <published>2025-11-23T05:00:04+00:00</published>
    <title>Test Paper Title</title>
    <summary>Test Summary</summary>
    <author>
      <name>Test Author</name>
    </author>
    <link title=""pdf"" href=""http://arxiv.org/pdf/2511.08685"" rel=""related"" type=""application/pdf""/>
  </entry>
</feed>";

        var parser = new ArxivParser();
        var releases = parser.ParseResponse(atomXml);

        Console.WriteLine($"Found {releases.Count} releases.");
        foreach (var r in releases)
        {
            Console.WriteLine($"Title: {r.Title}");
            Console.WriteLine($"URL: {r.DownloadUrl}");
            Console.WriteLine($"Category: {string.Join(",", r.Categories)}");
        }
    }
}
