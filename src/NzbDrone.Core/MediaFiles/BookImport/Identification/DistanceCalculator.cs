using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Books;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.BookImport.Identification
{
    public static class DistanceCalculator
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(DistanceCalculator));

        public static readonly List<string> VariousAuthorIds = new List<string> { "89ad4ac3-39f7-470e-963a-56509c546377" };

        private static readonly RegexReplace StripSeriesRegex = new RegexReplace(@"\([^\)].+?\)$", string.Empty, RegexOptions.Compiled);

        private static readonly RegexReplace CleanTitleCruft = new RegexReplace(@"\((?:unabridged)\)", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly List<string> EbookFormats = new List<string> { "Kindle Edition", "Nook", "ebook" };

        private static readonly List<string> AudiobookFormats = new List<string> { "Audiobook", "Audio CD", "Audio Cassette", "Audible Audio", "CD-ROM", "MP3 CD" };

        public static Distance BookDistance(List<LocalBook> localTracks, Edition edition, List<AuthorMetadata> allAuthorMetadata = null)
        {
            var dist = new Distance();

            // DOI matching - highest priority identifier for academic papers
            // If DOIs match exactly, this is essentially a perfect identification
            var localDoi = localTracks.MostCommon(x => x.FileTrackInfo.Doi);
            var editionDoi = GetDoiFromEdition(edition);

            if (localDoi.IsNotNullOrWhiteSpace() && editionDoi.IsNotNullOrWhiteSpace())
            {
                var doiMatch = DoiUtility.Normalize(localDoi) == DoiUtility.Normalize(editionDoi);

                // DOI match/mismatch has very high weight - it's a unique identifier
                dist.AddBool("doi", !doiMatch);
                Logger.Trace("doi: '{0}' vs '{1}' match={2}; {3}", localDoi, editionDoi, doiMatch, dist.NormalizedDistance());

                // If DOIs match, we can be very confident - reduce importance of other factors
                if (doiMatch)
                {
                    Logger.Debug("DOI match found - high confidence identification");
                }
            }
            else if (localDoi.IsNotNullOrWhiteSpace() != editionDoi.IsNotNullOrWhiteSpace())
            {
                // One has DOI, the other doesn't - slight penalty
                dist.AddBool("doi_missing", true);
                Logger.Trace("doi: '{0}' vs '{1}' (one missing); {2}", localDoi ?? "null", editionDoi ?? "null", dist.NormalizedDistance());
            }

            // Get all authors from file
            var fileAuthors = localTracks.Select(x => x.FileTrackInfo.Authors.Where(a => a.IsNotNullOrWhiteSpace()).ToList())
                .GroupBy(x => x.ConcatToString())
                .OrderByDescending(x => x.Count())
                .First()
                .First();

            var fileAuthorVariants = GetAuthorVariants(fileAuthors);
            var editionAuthorMetadata = edition.Book.Value.AuthorMetadata.Value;
            var editionAuthorName = editionAuthorMetadata.Name;

            // Check if edition is linked to a journal
            var isJournal = editionAuthorMetadata.Type == AuthorMetadataType.Journal ||
                            string.Equals(editionAuthorMetadata.Disambiguation, "Journal", System.StringComparison.InvariantCultureIgnoreCase);

            // Match authors individually if we have the full author metadata list
            if (allAuthorMetadata != null && allAuthorMetadata.Any())
            {
                // Match each file author individually against each metadata author
                // Use the best match score (lowest distance)
                var bestAuthorMatch = 1.0; // 1.0 = no match, 0.0 = perfect match
                var matchedAuthorName = string.Empty;

                foreach (var fileAuthor in fileAuthorVariants)
                {
                    foreach (var metadataAuthor in allAuthorMetadata)
                    {
                        // Skip journal metadata when matching individual authors
                        var metadataIsJournal = metadataAuthor.Type == AuthorMetadataType.Journal ||
                                               string.Equals(metadataAuthor.Disambiguation, "Journal", System.StringComparison.InvariantCultureIgnoreCase);
                        if (metadataIsJournal)
                        {
                            continue; // Skip journal - we'll match it separately
                        }

                        // Calculate string similarity for this author pair
                        var authorDistance = Distance.StringScore(fileAuthor, metadataAuthor.Name);
                        if (authorDistance < bestAuthorMatch)
                        {
                            bestAuthorMatch = authorDistance;
                            matchedAuthorName = metadataAuthor.Name;
                        }

                        // Also check aliases
                        if (metadataAuthor.Aliases != null)
                        {
                            foreach (var alias in metadataAuthor.Aliases)
                            {
                                var aliasDistance = Distance.StringScore(fileAuthor, alias);
                                if (aliasDistance < bestAuthorMatch)
                                {
                                    bestAuthorMatch = aliasDistance;
                                    matchedAuthorName = alias;
                                }
                            }
                        }
                    }
                }

                // Add the best author match to distance calculation
                if (isJournal)
                {
                    // For journals, check if any file author matches the journal name
                    // Then also check individual author matches
                    // Use the minimum (best) of journal name match and individual author matches
                    var journalMatchDistance = fileAuthorVariants.Any() 
                        ? fileAuthorVariants.Min(fa => Distance.StringScore(fa, editionAuthorName))
                        : 1.0;
                    
                    // Use the best match between journal name and individual authors
                    var finalAuthorDistance = Math.Min(journalMatchDistance, bestAuthorMatch);
                    
                    dist.Add("author_secondary", finalAuthorDistance);
                    
                    if (bestAuthorMatch < 1.0 && bestAuthorMatch < journalMatchDistance)
                    {
                        Logger.Trace("journal: '{0}' vs file authors '{1}' (best individual author match: '{2}' with distance {3}, journal distance {4}); {5}", 
                            editionAuthorName, fileAuthorVariants.ConcatToString("' or '"), matchedAuthorName, bestAuthorMatch, journalMatchDistance, dist.NormalizedDistance());
                    }
                    else if (journalMatchDistance < 1.0)
                    {
                        Logger.Trace("journal: '{0}' vs file authors '{1}' (journal name match distance {2}, best individual author distance {3}); {4}", 
                            editionAuthorName, fileAuthorVariants.ConcatToString("' or '"), journalMatchDistance, bestAuthorMatch, dist.NormalizedDistance());
                    }
                    else
                    {
                        Logger.Trace("journal: '{0}' vs file authors '{1}' (no matches - journal distance {2}, individual author distance {3}); {4}", 
                            editionAuthorName, fileAuthorVariants.ConcatToString("' or '"), journalMatchDistance, bestAuthorMatch, dist.NormalizedDistance());
                    }
                }
                else
                {
                    // For person authors, use the best individual match
                    dist.Add("author", bestAuthorMatch);
                    Logger.Trace("author: file authors '{0}' vs metadata authors (best match: '{1}' with distance {2}); {3}", 
                        fileAuthorVariants.ConcatToString("' or '"), matchedAuthorName, bestAuthorMatch, dist.NormalizedDistance());
                }
            }
            else
            {
                // Fallback to original behavior if we don't have full author metadata list
                if (isJournal)
                {
                    // For journals, author name matching is secondary (papers can have multiple authors)
                    // Use "author_secondary" key which has lower weight (1.0 vs 3.0) to indicate secondary importance
                    // The primary matching is done by journal name/entity, author names are just a hint
                    dist.AddString("author_secondary", fileAuthorVariants, editionAuthorName);
                    Logger.Trace("journal: '{0}' vs file authors '{1}'; {2}", 
                        editionAuthorName, fileAuthorVariants.ConcatToString("' or '"), dist.NormalizedDistance());
                }
                else
                {
                    // For person authors, author name matching is primary
                    dist.AddString("author", fileAuthorVariants, editionAuthorName);
                    Logger.Trace("author: '{0}' vs '{1}'; {2}", fileAuthorVariants.ConcatToString("' or '"), editionAuthorName, dist.NormalizedDistance());
                }
            }

            var title = localTracks.MostCommon(x => x.FileTrackInfo.BookTitle) ?? "";
            var titleOptions = new List<string> { edition.Title };
            if (titleOptions[0].Contains("#"))
            {
                titleOptions.Add(StripSeriesRegex.Replace(titleOptions[0]));
            }

            var (maintitle, _) = edition.Title.SplitBookTitle(edition.Book.Value.AuthorMetadata.Value.Name);
            if (!titleOptions.Contains(maintitle))
            {
                titleOptions.Add(maintitle);
            }

            if (edition.Book.Value.SeriesLinks?.Value?.Any() ?? false)
            {
                foreach (var l in edition.Book.Value.SeriesLinks.Value)
                {
                    if (l.Series?.Value?.Title?.IsNotNullOrWhiteSpace() ?? false)
                    {
                        titleOptions.Add($"{l.Series.Value.Title} {l.Position} {edition.Title}");
                        titleOptions.Add($"{l.Series.Value.Title} Book {l.Position} {edition.Title}");
                        titleOptions.Add($"{edition.Title} {l.Series.Value.Title} {l.Position}");
                        titleOptions.Add($"{edition.Title} {l.Series.Value.Title} Book {l.Position}");
                    }
                }
            }

            // Normalize titles to handle dash vs colon differences (e.g., "Title - Subtitle" vs "Title: Subtitle")
            var normalizedTitle = Parser.Parser.NormalizeTitleSeparators(title);
            var fileTitles = new[] { title, normalizedTitle, CleanTitleCruft.Replace(title), CleanTitleCruft.Replace(normalizedTitle) }.Distinct().Where(t => !t.IsNullOrWhiteSpace()).ToList();

            // Normalize book titles as well
            var normalizedTitleOptions = titleOptions.Select(t => Parser.Parser.NormalizeTitleSeparators(t)).ToList();
            var allTitleOptions = titleOptions.Concat(normalizedTitleOptions).Distinct().Where(t => !t.IsNullOrWhiteSpace()).ToList();

            dist.AddString("book", fileTitles, allTitleOptions);
            Logger.Trace("book: '{0}' vs '{1}'; {2}", fileTitles.ConcatToString("' or '"), titleOptions.ConcatToString("' or '"), dist.NormalizedDistance());

            var isbn = localTracks.MostCommon(x => x.FileTrackInfo.Isbn);
            if (isbn.IsNotNullOrWhiteSpace() && edition.Isbn13.IsNotNullOrWhiteSpace())
            {
                dist.AddBool("isbn", isbn != edition.Isbn13);
                Logger.Trace("isbn: '{0}' vs '{1}'; {2}", isbn, edition.Isbn13, dist.NormalizedDistance());
            }
            else if (isbn.IsNullOrWhiteSpace() != edition.Isbn13.IsNullOrWhiteSpace())
            {
                dist.AddBool("isbn_missing", true);
                Logger.Trace("isbn: '{0}' vs '{1}'; {2}", isbn, edition.Isbn13, dist.NormalizedDistance());
            }

            var asin = localTracks.MostCommon(x => x.FileTrackInfo.Asin);
            if (asin.IsNotNullOrWhiteSpace() && edition.Asin.IsNotNullOrWhiteSpace())
            {
                dist.AddBool("asin", asin != edition.Asin);
                Logger.Trace("asin: '{0}' vs '{1}'; {2}", asin, edition.Asin, dist.NormalizedDistance());
            }
            else if (asin.IsNullOrWhiteSpace() != edition.Asin.IsNullOrWhiteSpace())
            {
                dist.AddBool("asin_missing", true);
                Logger.Trace("asin: '{0}' vs '{1}'; {2}", asin, edition.Asin, dist.NormalizedDistance());
            }

            // Year
            var localYear = localTracks.MostCommon(x => x.FileTrackInfo.Year);
            if (localYear > 0 && edition.ReleaseDate.HasValue)
            {
                var bookYear = edition.ReleaseDate?.Year ?? 0;
                if (localYear == bookYear)
                {
                    dist.Add("year", 0.0);
                }
                else
                {
                    var remoteYear = bookYear;
                    var diff = Math.Abs(localYear - remoteYear);
                    var diff_max = Math.Abs(DateTime.Now.Year - remoteYear);
                    dist.AddRatio("year", diff, diff_max);
                }

                Logger.Trace($"year: {localYear} vs {edition.ReleaseDate?.Year}; {dist.NormalizedDistance()}");
            }

            // Language - only if set for both the local book and remote edition
            var localLanguage = localTracks.MostCommon(x => x.FileTrackInfo.Language).CanonicalizeLanguage();
            var editionLanguage = edition.Language.CanonicalizeLanguage();
            if (localLanguage.IsNotNullOrWhiteSpace() && editionLanguage.IsNotNullOrWhiteSpace())
            {
                dist.AddBool("language", localLanguage != editionLanguage);
                Logger.Trace($"language: {localLanguage} vs {editionLanguage}; {dist.NormalizedDistance()}");
            }

            // Publisher - only if set for both the local book and remote edition
            var localPublisher = localTracks.MostCommon(x => x.FileTrackInfo.Publisher);
            var editionPublisher = edition.Publisher;
            if (localPublisher.IsNotNullOrWhiteSpace() && editionPublisher.IsNotNullOrWhiteSpace())
            {
                dist.AddString("publisher", localPublisher, editionPublisher);
                Logger.Trace($"publisher: {localPublisher} vs {editionPublisher}; {dist.NormalizedDistance()}");
            }

            // Journal/Source matching - match journal name from file metadata against edition journal name
            // This is important for papers where the journal is the primary entity
            var localSource = localTracks.MostCommon(x => x.FileTrackInfo.Source);
            var editionSource = isJournal ? editionAuthorName : edition.Disambiguation;
            
            // Only match if both have journal/source information
            if (localSource.IsNotNullOrWhiteSpace() && editionSource.IsNotNullOrWhiteSpace())
            {
                dist.AddString("source", localSource, editionSource);
                Logger.Trace($"source/journal: '{localSource}' vs '{editionSource}'; {dist.NormalizedDistance()}");
            }

            // try to tilt it towards the correct "type" of release
            var isAudio = MediaFileExtensions.AudioExtensions.Contains(localTracks.First().Path.GetPathExtension());

            if (edition.Format.IsNotNullOrWhiteSpace())
            {
                if (!isAudio)
                {
                    // text books should prefer ebook formats
                    dist.AddBool("ebook_format", !EbookFormats.Contains(edition.Format));

                    // text books should not match audio entries
                    dist.AddBool("wrong_format", AudiobookFormats.Contains(edition.Format));
                }
                else
                {
                    // audio books should prefer audio formats
                    dist.AddBool("audio_format", !AudiobookFormats.Contains(edition.Format));
                }
            }

            return dist;
        }

        public static List<string> GetAuthorVariants(List<string> fileAuthors)
        {
            var authors = new List<string>(fileAuthors);

            if (fileAuthors.Count == 1)
            {
                authors.AddRange(SplitAuthor(fileAuthors[0]));
            }

            foreach (var author in fileAuthors)
            {
                if (author.Contains(','))
                {
                    var split = author.Split(',', 2).Select(x => x.Trim());
                    if (!split.First().Contains(' '))
                    {
                        authors.Add(split.Reverse().ConcatToString(" "));
                    }
                }
            }

            return authors;
        }

        private static List<string> SplitAuthor(string input)
        {
            var seps = new[] { ';', '/' };
            foreach (var sep in seps)
            {
                if (input.Contains(sep))
                {
                    return input.Split(sep).Select(x => x.Trim()).ToList();
                }
            }

            var andSeps = new List<string> { " and ", " & " };
            foreach (var sep in andSeps)
            {
                if (input.Contains(sep))
                {
                    var result = new List<string>();
                    foreach (var s in input.Split(sep).Select(x => x.Trim()))
                    {
                        var s2 = SplitAuthor(s);
                        if (s2.Any())
                        {
                            result.AddRange(s2);
                        }
                        else
                        {
                            result.Add(s);
                        }
                    }

                    return result;
                }
            }

            if (input.Contains(','))
            {
                var split = input.Split(',').Select(x => x.Trim()).ToList();
                if (split[0].Contains(' '))
                {
                    return split;
                }
            }

            return new List<string>();
        }

        /// <summary>
        /// Extract DOI from an Edition's Links or its parent Book's Links
        /// </summary>
        private static string GetDoiFromEdition(Edition edition)
        {
            // First try edition links
            var editionDoi = ExtractDoiFromLinks(edition.Links);
            if (editionDoi.IsNotNullOrWhiteSpace())
            {
                return editionDoi;
            }

            // Fall back to book links
            if (edition.Book?.Value?.Links != null)
            {
                return ExtractDoiFromLinks(edition.Book.Value.Links);
            }

            return null;
        }

        private static string ExtractDoiFromLinks(List<Links> links)
        {
            if (links == null)
            {
                return null;
            }

            var doiLink = links.FirstOrDefault(l =>
                l?.Name != null && l.Name.Equals("doi", StringComparison.OrdinalIgnoreCase));

            if (doiLink?.Url != null)
            {
                return DoiUtility.Normalize(doiLink.Url);
            }

            return null;
        }
    }
}
