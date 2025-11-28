using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.SciHub
{
    public class SciHub : HttpIndexerBase<SciHubSettings>
    {
        public override string Name => "Sci-Hub";
        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => false;
        public override bool SupportsSearch => true;
        public override int PageSize => 1;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(5);

        public SciHub(IHttpClient httpClient, IIndexerStatusService indexerStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new SciHubRequestGenerator { Settings = Settings, HttpClient = _httpClient };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new SciHubParser { Settings = Settings };
        }

        protected override async Task Test(List<ValidationFailure> failures)
        {
            var mirrors = Settings.Mirrors.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var allFailures = new List<ValidationFailure>();

            foreach (var mirror in mirrors)
            {
                var baseUrl = mirror.Trim();
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    continue;
                }

                var testRequest = new IndexerRequest($"{baseUrl}/", HttpAccept.Html);
                var failure = await TestUrl(testRequest, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);

                if (failure == null)
                {
                    // At least one mirror is working, test passes
                    _logger.Info($"SciHub mirror {baseUrl} is reachable");
                    return;
                }
                else
                {
                    _logger.Warn($"SciHub mirror {baseUrl} failed: {failure.ErrorMessage}");
                    allFailures.Add(failure);
                }
            }

            // All mirrors failed
            failures.Add(new ValidationFailure("Mirrors", $"All {allFailures.Count} SciHub mirrors are unreachable"));
        }
    }
}
