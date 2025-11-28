using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.LibGen;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.LibGenTests
{
    [TestFixture]
    public class LibGenParserFixture : CoreTest<LibGenParser>
    {
        private LibGenSettings _settings;

        [SetUp]
        public void Setup()
        {
            _settings = new LibGenSettings
            {
                Mirrors = "https://libgen.li"
            };

            Subject.Settings = _settings;
        }

        private IndexerResponse CreateResponse(string htmlContent, string url = "https://libgen.li/search.php?req=test")
        {
            var httpRequest = new HttpRequest(url);
            var httpResponse = new HttpResponse(httpRequest, new HttpHeader(), Encoding.UTF8.GetBytes(htmlContent));
            return new IndexerResponse(new IndexerRequest(httpRequest), httpResponse);
        }

        [Test]
        public void should_parse_search_results_from_libgen_html()
        {
            var html = ReadAllText("Files/Indexers/LibGen/LibGenSearch.html");
            var response = CreateResponse(html);

            var releases = Subject.ParseResponse(response);

            releases.Should().HaveCount(3);

            var firstRelease = releases.First();
            firstRelease.Author.Should().Be("John Smith");
            firstRelease.Book.Should().Be("Machine Learning Basics");

            // Relative URL should be resolved against baseUrl
            firstRelease.DownloadUrl.Should().Contain("libgen.li/ads.php?md5=ABC123DEF456789012345678901234AB");
            firstRelease.Size.Should().BeGreaterThan(0);

            var secondRelease = releases.Skip(1).First();
            secondRelease.Author.Should().Be("Jane Doe");
            secondRelease.Book.Should().Be("Deep Learning Advanced");
            secondRelease.DownloadUrl.Should().Contain("libgen.li/ads.php?md5=XYZ789GHI012345678901234567890123");
        }

        [Test]
        public void should_extract_size_from_various_formats()
        {
            var html = ReadAllText("Files/Indexers/LibGen/LibGenSearch.html");
            var response = CreateResponse(html);

            var releases = Subject.ParseResponse(response);

            // First release: "5 MB" - should be around 5MB
            releases.First().Size.Should().BeGreaterThan(4 * 1024 * 1024);

            // Second release: "12 MB" - should be around 12MB
            releases.Skip(1).First().Size.Should().BeGreaterThan(11 * 1024 * 1024);

            // Third release: "549 kB" - should be around 500KB
            releases.Skip(2).First().Size.Should().BeGreaterThan(500 * 1024);
            releases.Skip(2).First().Size.Should().BeLessThan(600 * 1024);
        }

        [Test]
        public void should_handle_empty_response()
        {
            var html = "<html><body><table></table></body></html>";
            var response = CreateResponse(html);

            var releases = Subject.ParseResponse(response);

            releases.Should().BeEmpty();
        }

        [Test]
        public void should_handle_missing_download_url_gracefully()
        {
            var html = @"
<html><body>
<table>
<tr><th>Author</th><th>Title</th></tr>
<tr><td>Test Author</td><td><a href='book/index.php?md5=ABCD1234'>No Download Link Book</a></td></tr>
</table>
</body></html>";
            var response = CreateResponse(html);

            var releases = Subject.ParseResponse(response);

            // Should still find the release using md5 fallback URL construction
            releases.Should().HaveCount(1);
            releases.First().DownloadUrl.Should().Contain("ABCD1234");
        }

        [Test]
        public void should_extract_extension_correctly()
        {
            var html = ReadAllText("Files/Indexers/LibGen/LibGenSearch.html");
            var response = CreateResponse(html);

            var releases = Subject.ParseResponse(response);

            // First release should be PDF
            releases.First().Title.Should().Contain("PDF");

            // Second release should be EPUB
            releases.Skip(1).First().Title.Should().Contain("EPUB");
        }

        [Test]
        public void should_return_empty_for_404_response()
        {
            var httpRequest = new HttpRequest("https://libgen.li/search.php");
            var httpResponse = new HttpResponse(httpRequest, new HttpHeader(), Encoding.UTF8.GetBytes("Not Found"), System.Net.HttpStatusCode.NotFound);
            var response = new IndexerResponse(new IndexerRequest(httpRequest), httpResponse);

            var releases = Subject.ParseResponse(response);

            releases.Should().BeEmpty();
        }

        [Test]
        public void should_unwrap_flaresolverr_response()
        {
            var flareSolverrJson = @"{
                ""solution"": {
                    ""url"": ""https://libgen.li/search.php?req=test"",
                    ""response"": ""<html><body><table><tr><th>Author</th><th>Title</th></tr><tr><td>FlareSolverr Author</td><td><a href='book/index.php?md5=FLARE123456789012345678901234567'>FlareSolverr Book</a></td><td><a href='http://library.lol/main/FLARE123456789012345678901234567'>GET</a></td></tr></table></body></html>""
                }
            }";

            var response = CreateResponse(flareSolverrJson);

            var releases = Subject.ParseResponse(response);

            releases.Should().HaveCount(1);
            releases.First().Author.Should().Be("FlareSolverr Author");
        }
    }
}
