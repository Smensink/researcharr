using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.DecisionEngine;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Indexers
{
    public abstract class ReleaseControllerBase : RestController<ReleaseResource>
    {
        [NonAction]
        public override ActionResult<ReleaseResource> GetResourceByIdWithErrorHandler(int id)
        {
            return base.GetResourceByIdWithErrorHandler(id);
        }

        protected override ReleaseResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        protected virtual List<ReleaseResource> MapDecisions(IEnumerable<DownloadDecision> decisions)
        {
            var result = new List<ReleaseResource>();

            foreach (var downloadDecision in decisions)
            {
                var release = MapDecision(downloadDecision, result.Count);

                result.Add(release);
            }

            return result;
        }

        protected virtual ReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var release = decision.ToResource();

            release.ReleaseWeight = initialWeight;

            if (decision.RemoteBook?.Author?.QualityProfile?.Value != null && release.Quality?.Quality != null)
            {
                var qualityIndex = decision.RemoteBook.Author.QualityProfile.Value.GetIndex(release.Quality.Quality);
                release.QualityWeight = qualityIndex.Index * 100;
            }
            else
            {
                release.QualityWeight = 0;
            }

            if (release.Quality?.Revision != null)
            {
                release.QualityWeight += release.Quality.Revision.Real * 10;
                release.QualityWeight += release.Quality.Revision.Version;
            }

            return release;
        }
    }
}
