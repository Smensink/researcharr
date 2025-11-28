using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Arxiv
{
    public class Arxiv : HttpIndexerBase<ArxivSettings>
    {
        public override string Name => "arXiv";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(3);

        public Arxiv(IHttpClient httpClient, IIndexerStatusService indexerStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new ArxivRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new ArxivParser { Settings = Settings };
        }
    }
}
