using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Unpaywall
{
    public class Unpaywall : HttpIndexerBase<UnpaywallSettings>
    {
        public override string Name => "Unpaywall";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 1;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(1);

        public Unpaywall(IHttpClient httpClient, IIndexerStatusService indexerStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new UnpaywallRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new UnpaywallParser { Settings = Settings };
        }

        protected override async Task Test(List<ValidationFailure> failures)
        {
            var testRequest = GetRequestGenerator().GetRecentRequests().GetAllTiers().FirstOrDefault()?.FirstOrDefault();
            var failure = await TestUrl(testRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
            if (failure != null)
            {
                failures.Add(failure);
            }
        }
    }
}
