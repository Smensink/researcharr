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

namespace NzbDrone.Core.Indexers.Core
{
    public class Core : HttpIndexerBase<CoreSettings>
    {
        public override string Name => "CORE";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(2);

        public Core(IHttpClient httpClient, IIndexerStatusService indexerStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new CoreRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new CoreParser { Settings = Settings };
        }

        protected override async Task Test(List<ValidationFailure> failures)
        {
            var testRequest = GetRequestGenerator().GetRecentRequests().GetAllTiers().FirstOrDefault()?.FirstOrDefault();
            var failure = await TestUrl(testRequest, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.MovedPermanently, HttpStatusCode.Redirect, HttpStatusCode.RedirectKeepVerb, HttpStatusCode.RedirectMethod);
            if (failure != null)
            {
                failures.Add(failure);
            }
        }
    }
}
