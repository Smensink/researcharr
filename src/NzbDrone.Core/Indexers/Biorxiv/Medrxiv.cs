using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Biorxiv
{
    public class Medrxiv : HttpIndexerBase<MedrxivSettings>
    {
        public override string Name => "medRxiv";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(1);

        public Medrxiv(IHttpClient httpClient,
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
                Server = "medrxiv"
            };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new BiorxivParser
            {
                Settings = Settings,
                Server = "medrxiv",
                ContentBaseUrl = "https://www.medrxiv.org",
                SourceName = Name
            };
        }
    }
}
