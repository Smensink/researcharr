using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.BookImport.Specifications
{
    public class BookUpgradeSpecification : IImportDecisionEngineSpecification<LocalEdition>
    {
        private readonly Logger _logger;

        public BookUpgradeSpecification(Logger logger)
        {
            _logger = logger;
        }

        public Decision IsSatisfiedBy(LocalEdition item, DownloadClientItem downloadClientItem)
        {
            // Check for null/missing data - guard against new authors or authors without quality profiles
            var qualityProfile = item.Edition?.Book?.Value?.Author?.Value?.QualityProfile?.Value;
            if (qualityProfile == null || qualityProfile.Items == null || !qualityProfile.Items.Any())
            {
                _logger.Debug("Author quality profile is missing or invalid, accepting import: {0}", item);
                return Decision.Accept();
            }

            if (item.LocalBooks == null || !item.LocalBooks.Any())
            {
                _logger.Debug("No local books found, accepting import: {0}", item);
                return Decision.Accept();
            }

            var qualityComparer = new QualityModelComparer(qualityProfile);

            // min quality of all new tracks
            var newMinQuality = item.LocalBooks.Select(x => x.Quality).OrderBy(x => x, qualityComparer).First();
            _logger.Debug("Min quality of new files: {0}", newMinQuality);

            // get minimum quality of existing release
            // var existingQualities = currentRelease.Value.Where(x => x.TrackFileId != 0).Select(x => x.TrackFile.Value.Quality);
            // if (existingQualities.Any())
            // {
            //     var existingMinQuality = existingQualities.OrderBy(x => x, qualityComparer).First();
            //     _logger.Debug("Min quality of existing files: {0}", existingMinQuality);
            //     if (qualityComparer.Compare(existingMinQuality, newMinQuality) > 0)
            //     {
            //         _logger.Debug("This book isn't a quality upgrade for all tracks. Skipping {0}", item);
            //         return Decision.Reject("Not an upgrade for existing book file(s)");
            //     }
            // }
            return Decision.Accept();
        }
    }
}
