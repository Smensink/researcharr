using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource.OpenAlex;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class OpenAlexProxyFixture : CoreTest<OpenAlexProxy>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant(new OpenAlexSettings
            {
                BaseUrl = "https://api.openalex.org/"
            });
        }

        [Test]
        public void should_get_author_info_using_author_id()
        {
            var authorId = "A5003442464";
            var json = "{\"id\": \"https://openalex.org/A5003442464\", \"display_name\": \"Gary Hoffman\"}";

            Mocker.GetMock<ICachedHttpResponseService>()
                  .Setup(s => s.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains($"authors/{authorId}")), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                  .Returns(new HttpResponse(new HttpRequestBuilder("http://test").Build(), new HttpHeader(), json));

            var worksJson = "{\"meta\": {\"count\": 0}, \"results\": []}";
            Mocker.GetMock<ICachedHttpResponseService>()
                  .Setup(s => s.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("works")), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                  .Returns(new HttpResponse(new HttpRequestBuilder("http://test").Build(), new HttpHeader(), worksJson));

            var result = Subject.GetAuthorInfo(authorId);

            result.Should().NotBeNull();
            result.Metadata.Value.ForeignAuthorId.Should().Be(authorId);
            result.Metadata.Value.Name.Should().Be("Gary Hoffman");
        }

        [Test]
        public void should_get_source_info_using_source_id()
        {
            var sourceId = "S4210172589";
            var json = "{\"id\": \"https://openalex.org/S4210172589\", \"display_name\": \"Nature\"}";

            Mocker.GetMock<ICachedHttpResponseService>()
                  .Setup(s => s.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains($"sources/{sourceId}")), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                  .Returns(new HttpResponse(new HttpRequestBuilder("http://test").Build(), new HttpHeader(), json));

            // Mock the works call which is expected to happen if author/source is found
            var worksJson = "{\"meta\": {\"count\": 0}, \"results\": []}";
            Mocker.GetMock<ICachedHttpResponseService>()
                  .Setup(s => s.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("works")), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                  .Returns(new HttpResponse(new HttpRequestBuilder("http://test").Build(), new HttpHeader(), worksJson));

            var result = Subject.GetAuthorInfo(sourceId);

            result.Should().NotBeNull();
            result.Metadata.Value.ForeignAuthorId.Should().Be(sourceId);
            result.Metadata.Value.Name.Should().Be("Nature");
            result.Metadata.Value.Disambiguation.Should().Be("Journal");
        }
    }
}
