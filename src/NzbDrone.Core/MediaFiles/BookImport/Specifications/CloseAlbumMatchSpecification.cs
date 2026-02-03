using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.BookImport.Specifications
{
    public class CloseBookMatchSpecification : IImportDecisionEngineSpecification<LocalEdition>
    {
        private const double _bookThreshold = 0.20;
        private readonly Logger _logger;

        public CloseBookMatchSpecification(Logger logger)
        {
            _logger = logger;
        }

        public Decision IsSatisfiedBy(LocalEdition item, DownloadClientItem downloadClientItem)
        {
            double dist;
            string reasons;
            double threshold = _bookThreshold;

            // Check if DOIs match - if so, accept immediately (DOI is a unique identifier)
            var doiPenalty = item.Distance.Penalties.ContainsKey("doi") ? item.Distance.Penalties["doi"].Max() : 1.0;
            if (doiPenalty == 0.0)
            {
                // DOIs match exactly - this is a perfect identification, accept regardless of other distance factors
                _logger.Debug($"DOI match found - accepting release {item} regardless of distance: {item.Distance.NormalizedDistance()}");
                // #region agent log
                try
                {
                    var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                    if (!System.IO.File.Exists(logPath))
                    {
                        logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                        logPath = System.IO.Path.GetFullPath(logPath);
                    }
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                    System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "doi-acceptance", hypothesisId = "G", location = "CloseAlbumMatchSpecification.cs:IsSatisfiedBy", message = "DOI match found - accepting", data = new { doiPenalty = doiPenalty, distance = item.Distance.NormalizedDistance(), reasons = item.Distance.Reasons }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                }
                catch { }
                // #endregion
                return Decision.Accept();
            }

            // strict when a new download
            if (item.NewDownload)
            {
                dist = item.Distance.NormalizedDistance();
                reasons = item.Distance.Reasons;
                
                // For academic papers, be more lenient when DOI is missing but other metadata matches
                // If the only issues are missing DOI/ISBN/author_secondary, use a more lenient threshold
                // Reasons format is like "[doi, author secondary, isbn missing]" or "[doi missing]"
                // Check if reasons contain ONLY missing metadata indicators (doi missing, isbn missing, author secondary missing)
                // and NO major mismatches (book, author, wrong format)
                var hasDoiMissing = reasons.Contains("doi", StringComparison.OrdinalIgnoreCase) && reasons.Contains("missing", StringComparison.OrdinalIgnoreCase);
                var hasIsbnMissing = reasons.Contains("isbn", StringComparison.OrdinalIgnoreCase) && reasons.Contains("missing", StringComparison.OrdinalIgnoreCase);
                var hasAuthorSecondaryMissing = reasons.Contains("author secondary", StringComparison.OrdinalIgnoreCase) && reasons.Contains("missing", StringComparison.OrdinalIgnoreCase);
                var hasOnlyMissingMetadata = (hasDoiMissing || hasIsbnMissing || hasAuthorSecondaryMissing);
                // Check if there are major mismatches by looking at the actual distance penalties
                // "book" penalty indicates title mismatch, "author" indicates author name mismatch
                // "wrong format" indicates format mismatch
                // We check the actual penalties to be thorough (reasons string only shows penalties > 0.0)
                var hasBookPenalty = item.Distance.Penalties.ContainsKey("book") && item.Distance.Penalties["book"].Max() > 0.0;
                var hasAuthorPenalty = item.Distance.Penalties.ContainsKey("author") && item.Distance.Penalties["author"].Max() > 0.0;
                var hasWrongFormatPenalty = item.Distance.Penalties.ContainsKey("wrong_format") && item.Distance.Penalties["wrong_format"].Max() > 0.0;
                var hasNoMajorMismatches = !hasBookPenalty && !hasAuthorPenalty && !hasWrongFormatPenalty;
                
                // #region agent log
                try
                {
                    var logPath = System.IO.Path.Combine("/workspace", ".cursor", "debug.log");
                    if (!System.IO.File.Exists(logPath))
                    {
                        logPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".", "..", "..", "..", ".cursor", "debug.log");
                        logPath = System.IO.Path.GetFullPath(logPath);
                    }
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath) ?? ".");
                    System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "lenient-threshold", hypothesisId = "I", location = "CloseAlbumMatchSpecification.cs:IsSatisfiedBy", message = "Checking lenient threshold", data = new { dist = dist, reasons = reasons, hasDoiMissing = hasDoiMissing, hasIsbnMissing = hasIsbnMissing, hasAuthorSecondaryMissing = hasAuthorSecondaryMissing, hasOnlyMissingMetadata = hasOnlyMissingMetadata, hasBookPenalty = hasBookPenalty, hasAuthorPenalty = hasAuthorPenalty, hasWrongFormatPenalty = hasWrongFormatPenalty, hasNoMajorMismatches = hasNoMajorMismatches, willApplyLenient = hasOnlyMissingMetadata && hasNoMajorMismatches && dist < 0.90 }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n");
                }
                catch { }
                // #endregion
                
                if (hasOnlyMissingMetadata && hasNoMajorMismatches && dist < 0.90)
                {
                    // Use a more lenient threshold (0.90 = 10% match) when only metadata is missing
                    threshold = 0.90;
                    _logger.Debug($"Using lenient threshold for academic paper with missing metadata: {dist} vs {threshold} {reasons}");
                }
                
                if (dist > threshold)
                {
                    _logger.Debug($"Book match is not close enough: {dist} vs {threshold} {reasons}. Skipping {item}");
                    return Decision.Reject($"Book match is not close enough: {1 - dist:P1} vs {1 - threshold:P0} {reasons}");
                }
            }

            // otherwise importing existing files in library
            else
            {
                // get book distance ignoring whether tracks are missing
                dist = item.Distance.NormalizedDistanceExcluding(new List<string> { "missing_tracks", "unmatched_tracks" });
                reasons = item.Distance.Reasons;
                
                // Apply same lenient threshold logic for existing files
                var hasOnlyMissingMetadata = (reasons.Contains("doi", StringComparison.OrdinalIgnoreCase) && 
                                             (reasons.Contains("missing", StringComparison.OrdinalIgnoreCase) || reasons.Contains("doi missing", StringComparison.OrdinalIgnoreCase))) ||
                                           (reasons.Contains("isbn", StringComparison.OrdinalIgnoreCase) && reasons.Contains("missing", StringComparison.OrdinalIgnoreCase)) ||
                                           (reasons.Contains("author secondary", StringComparison.OrdinalIgnoreCase) && reasons.Contains("missing", StringComparison.OrdinalIgnoreCase));
                var hasNoMajorMismatches = !reasons.Contains("book", StringComparison.OrdinalIgnoreCase) &&
                                          !reasons.Contains("author", StringComparison.OrdinalIgnoreCase) &&
                                          !reasons.Contains("wrong format", StringComparison.OrdinalIgnoreCase);
                
                if (hasOnlyMissingMetadata && hasNoMajorMismatches && dist < 0.90)
                {
                    threshold = 0.90;
                    _logger.Debug($"Using lenient threshold for academic paper with missing metadata: {dist} vs {threshold} {reasons}");
                }
                
                if (dist > threshold)
                {
                    _logger.Debug($"Book match is not close enough: {dist} vs {threshold} {reasons}. Skipping {item}");
                    return Decision.Reject($"Book match is not close enough: {1 - dist:P1} vs {1 - threshold:P0} {reasons}");
                }
            }

            _logger.Debug($"Accepting release {item}: dist {dist} vs {_bookThreshold} {reasons}");
            return Decision.Accept();
        }
    }
}
