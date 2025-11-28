using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.PubMedCentral
{
    public class PubMedCentral : HttpIndexerBase<PubMedCentralSettings>
    {
        public override string Name => "PubMed Central";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => TimeSpan.FromMilliseconds(350); // ~3 requests/sec without API key

        public PubMedCentral(IHttpClient httpClient, IIndexerStatusService indexerStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new PubMedCentralRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new PubMedCentralParser { Settings = Settings };
        }
    }
}
