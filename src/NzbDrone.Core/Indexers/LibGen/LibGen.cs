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

namespace NzbDrone.Core.Indexers.LibGen
{
    public class LibGen : HttpIndexerBase<LibGenSettings>
    {
        public override string Name => "LibGen";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(2);

        public LibGen(IHttpClient httpClient, IIndexerStatusService indexerStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new LibGenRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new LibGenParser { Settings = Settings };
        }

        protected override async Task Test(List<ValidationFailure> failures)
        {
            var requests = GetRequestGenerator().GetRecentRequests().GetAllTiers().SelectMany(r => r).ToList();

            if (!requests.Any())
            {
                failures.Add(new ValidationFailure(string.Empty, "No test request available for LibGen."));
                return;
            }

            var tasks = requests.Select(r => TestUrl(r, HttpStatusCode.OK, HttpStatusCode.MovedPermanently, HttpStatusCode.Redirect, HttpStatusCode.RedirectKeepVerb, HttpStatusCode.RedirectMethod));
            var results = await Task.WhenAll(tasks);

            if (results.All(r => r != null))
            {
                failures.Add(results.First(r => r != null));
            }
        }
    }
}
