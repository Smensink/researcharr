using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Biorxiv
{
    public class Biorxiv : HttpIndexerBase<BiorxivSettings>
    {
        public override string Name => "bioRxiv";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(1);

        public Biorxiv(IHttpClient httpClient,
                       IIndexerStatusService indexerStatusService,
                       IConfigService configService,
                       IParsingService parsingService,
                       Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new BiorxivRequestGenerator
            {
                Settings = Settings,
                Server = "biorxiv"
            };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new BiorxivParser
            {
                Settings = Settings,
                Server = "biorxiv",
                ContentBaseUrl = "https://www.biorxiv.org",
                SourceName = Name
            };
        }
    }
}
