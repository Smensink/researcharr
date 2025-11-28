using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Parser
{
    public static class DoiUtility
    {
        // DOI regex pattern - matches standard DOI format: 10.XXXX/suffix
        // DOIs start with 10. followed by registrant code (4+ digits), then / and suffix
        // Suffix can contain alphanumeric chars, dots, dashes, underscores, parentheses, etc.
        private static readonly Regex DoiRegex = new Regex(
            @"(?:doi[:\s]*)?(?:https?://(?:dx\.)?doi\.org/)?(?<doi>10\.\d{4,}/[^\s""'<>\[\]]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Pattern for DOIs in filenames where / is replaced with _ or -
        private static readonly Regex DoiFilenameRegex = new Regex(
            @"(?<doi>10\.\d{4,}[_\-][^\s""'<>\[\].]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Normalize(string doi)
        {
            if (doi.IsNullOrWhiteSpace())
            {
                return null;
            }

            var trimmed = doi.Trim();
            var doiIndex = trimmed.IndexOf("doi.org/", StringComparison.OrdinalIgnoreCase);

            if (doiIndex >= 0)
            {
                trimmed = trimmed[(doiIndex + "doi.org/".Length) ..];
            }

            if (trimmed.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["doi:".Length..];
            }

            trimmed = trimmed.Trim().ToLowerInvariant();

            if (!trimmed.StartsWith("10.", StringComparison.OrdinalIgnoreCase) || !trimmed.Contains("/"))
            {
                return null;
            }

            return trimmed;
        }

        public static bool IsDoiMatch(ReleaseInfo report, SearchCriteriaBase searchCriteria)
        {
            var reportDoi = Normalize(report?.Doi);
            if (reportDoi == null || searchCriteria?.Books == null)
            {
                return false;
            }

            return GetDois(searchCriteria).Any(d => d == reportDoi);
        }

        public static IEnumerable<string> GetDois(SearchCriteriaBase searchCriteria)
        {
            if (searchCriteria?.Books == null)
            {
                yield break;
            }

            foreach (var doi in searchCriteria.Books
                         .SelectMany(b => b.Links ?? new List<Links>())
                         .Where(l => l != null && l.Name != null && l.Name.Equals("doi", StringComparison.OrdinalIgnoreCase))
                         .Select(l => Normalize(l.Url)))
            {
                if (doi.IsNotNullOrWhiteSpace())
                {
                    yield return doi;
                }
            }
        }

        /// <summary>
        /// Extract DOI from text content (PDF text, metadata fields, etc.)
        /// </summary>
        public static string ExtractFromText(string text)
        {
            if (text.IsNullOrWhiteSpace())
            {
                return null;
            }

            var match = DoiRegex.Match(text);
            if (match.Success)
            {
                var doi = match.Groups["doi"].Value;

                // Clean up trailing punctuation that might have been captured
                doi = doi.TrimEnd('.', ',', ';', ':', ')', ']');
                return Normalize(doi);
            }

            return null;
        }

        /// <summary>
        /// Extract DOI from filename where / is typically replaced with _ or -
        /// </summary>
        public static string ExtractFromFilename(string filename)
        {
            if (filename.IsNullOrWhiteSpace())
            {
                return null;
            }

            // First try standard DOI format (in case filename has actual /)
            var standardMatch = DoiRegex.Match(filename);
            if (standardMatch.Success)
            {
                var doi = standardMatch.Groups["doi"].Value;
                doi = doi.TrimEnd('.', ',', ';', ':', ')', ']');
                return Normalize(doi);
            }

            // Try filename format where / is replaced with _ or -
            var filenameMatch = DoiFilenameRegex.Match(filename);
            if (filenameMatch.Success)
            {
                var doi = filenameMatch.Groups["doi"].Value;

                // Convert filename separator back to /
                // Find the position after the registrant code (first _ or - after 10.XXXX)
                var registrantEnd = doi.IndexOfAny(new[] { '_', '-' });
                if (registrantEnd > 0)
                {
                    doi = doi.Substring(0, registrantEnd) + "/" + doi.Substring(registrantEnd + 1);
                }

                doi = doi.TrimEnd('.', ',', ';', ':', ')', ']');
                return Normalize(doi);
            }

            return null;
        }

        /// <summary>
        /// Extract all DOIs from text content
        /// </summary>
        public static IEnumerable<string> ExtractAllFromText(string text)
        {
            if (text.IsNullOrWhiteSpace())
            {
                yield break;
            }

            var seen = new HashSet<string>();
            var matches = DoiRegex.Matches(text);

            foreach (Match match in matches)
            {
                var doi = match.Groups["doi"].Value;
                doi = doi.TrimEnd('.', ',', ';', ':', ')', ']');
                var normalized = Normalize(doi);

                if (normalized.IsNotNullOrWhiteSpace() && !seen.Contains(normalized))
                {
                    seen.Add(normalized);
                    yield return normalized;
                }
            }
        }
    }
}
