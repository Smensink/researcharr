using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.CrossRef;
using NzbDrone.Core.MetadataSource.OpenAlex;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataAggregator
    {
        /// <summary>
        /// Get book metadata from multiple sources and merge intelligently
        /// </summary>
        Book GetEnhancedBookMetadata(string identifier, bool useCache = true);

        /// <summary>
        /// Merge book metadata from multiple sources
        /// </summary>
        Book MergeBookMetadata(List<Book> books);
    }

    public class MetadataAggregator : IMetadataAggregator
    {
        private readonly ICrossRefProxy _crossRefProxy;
        private readonly IOpenAlexProxy _openAlexProxy;
        private readonly Logger _logger;

        public MetadataAggregator(
            ICrossRefProxy crossRefProxy,
            IOpenAlexProxy openAlexProxy,
            Logger logger)
        {
            _crossRefProxy = crossRefProxy;
            _openAlexProxy = openAlexProxy;
            _logger = logger;
        }

        public Book GetEnhancedBookMetadata(string identifier, bool useCache = true)
        {
            var sources = new List<Book>();

            // Try DOI-based lookup first (most reliable)
            var doi = DoiUtility.Normalize(identifier);
            if (!string.IsNullOrWhiteSpace(doi))
            {
                _logger.Debug("Fetching metadata for DOI: {0}", doi);

                // CrossRef is authoritative for DOI metadata
                try
                {
                    var crossRefBook = _crossRefProxy.GetBookByDoi(doi, useCache);
                    if (crossRefBook != null)
                    {
                        sources.Add(crossRefBook);
                        _logger.Debug("Found metadata in CrossRef");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error fetching from CrossRef");
                }

                // OpenAlex provides complementary data
                try
                {
                    var openAlexBook = _openAlexProxy.GetBookByDoi(doi, useCache);
                    if (openAlexBook != null)
                    {
                        sources.Add(openAlexBook);
                        _logger.Debug("Found metadata in OpenAlex");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error fetching from OpenAlex");
                }
            }
            else
            {
                // Fall back to OpenAlex ID or other identifiers
                try
                {
                    var openAlexBook = _openAlexProxy.GetBookInfo(identifier);
                    if (openAlexBook != null)
                    {
                        sources.Add(openAlexBook.Item2);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error fetching from OpenAlex by ID");
                }
            }

            if (!sources.Any())
            {
                _logger.Debug("No metadata found for identifier: {0}", identifier);
                return null;
            }

            // Merge the metadata from all sources
            return MergeBookMetadata(sources);
        }

        public Book MergeBookMetadata(List<Book> books)
        {
            if (books == null || !books.Any())
            {
                return null;
            }

            if (books.Count == 1)
            {
                return books.First();
            }

            _logger.Debug("Merging metadata from {0} sources", books.Count);

            // Start with the most complete book as base
            var merged = books.OrderByDescending(b => GetCompletenessScore(b)).First();

            // Merge fields from other sources
            foreach (var book in books.Where(b => b != merged))
            {
                merged = MergeTwoBooks(merged, book);
            }

            // Ensure identifiers are preserved from any source
            var identitySource = books.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.ForeignBookId)) ?? books.First();

            merged.ForeignBookId = identitySource.ForeignBookId ?? merged.ForeignBookId;
            merged.ForeignEditionId = identitySource.ForeignEditionId ?? merged.ForeignEditionId;
            merged.TitleSlug = identitySource.TitleSlug ?? merged.TitleSlug;
            merged.CleanTitle = identitySource.CleanTitle ?? merged.CleanTitle;

            if ((merged.Editions == null || (merged.Editions.IsLoaded && (merged.Editions.Value == null || !merged.Editions.Value.Any())))
                && identitySource.Editions != null
                && (!identitySource.Editions.IsLoaded || (identitySource.Editions.Value != null && identitySource.Editions.Value.Any())))
            {
                merged.Editions = identitySource.Editions;
            }

            return merged;
        }

        private Book MergeTwoBooks(Book primary, Book secondary)
        {
            // Title: Prefer non-null, longer, more specific
            if (string.IsNullOrWhiteSpace(primary.Title) && !string.IsNullOrWhiteSpace(secondary.Title))
            {
                primary.Title = secondary.Title;
            }

            // Release Date: Prefer earlier, more specific
            if (!primary.ReleaseDate.HasValue && secondary.ReleaseDate.HasValue)
            {
                primary.ReleaseDate = secondary.ReleaseDate;
            }

            // Identifiers: keep the first available
            if (string.IsNullOrWhiteSpace(primary.ForeignBookId) && !string.IsNullOrWhiteSpace(secondary.ForeignBookId))
            {
                primary.ForeignBookId = secondary.ForeignBookId;
            }

            if (string.IsNullOrWhiteSpace(primary.ForeignEditionId) && !string.IsNullOrWhiteSpace(secondary.ForeignEditionId))
            {
                primary.ForeignEditionId = secondary.ForeignEditionId;
            }

            if (string.IsNullOrWhiteSpace(primary.TitleSlug) && !string.IsNullOrWhiteSpace(secondary.TitleSlug))
            {
                primary.TitleSlug = secondary.TitleSlug;
            }

            if (string.IsNullOrWhiteSpace(primary.CleanTitle) && !string.IsNullOrWhiteSpace(secondary.CleanTitle))
            {
                primary.CleanTitle = secondary.CleanTitle;
            }

            // Links: Merge and deduplicate
            if (primary.Links == null)
            {
                primary.Links = new List<Links>();
            }

            if (secondary.Links != null)
            {
                foreach (var link in secondary.Links)
                {
                    // Check if link already exists (by name and url)
                    if (!primary.Links.Any(l =>
                        l.Name?.Equals(link.Name, StringComparison.OrdinalIgnoreCase) == true &&
                        l.Url?.Equals(link.Url, StringComparison.OrdinalIgnoreCase) == true))
                    {
                        primary.Links.Add(link);
                    }
                }
            }

            // Ratings: Prefer higher vote count
            if (secondary.Ratings?.Votes > primary.Ratings?.Votes)
            {
                primary.Ratings = secondary.Ratings;
            }

            // Editions: merge editions, preferring richer data
            var primaryEditions = primary.Editions?.Value ?? new List<Edition>();
            var secondaryEditions = secondary.Editions?.Value ?? new List<Edition>();

            if (!primaryEditions.Any() && secondaryEditions.Any())
            {
                primary.Editions = secondary.Editions;
            }
            else if (primaryEditions.Any() && secondaryEditions.Any())
            {
                var primaryEdition = primaryEditions.First();
                var secondaryEdition = secondaryEditions.First();

                if (string.IsNullOrWhiteSpace(primaryEdition.Overview) && !string.IsNullOrWhiteSpace(secondaryEdition.Overview))
                {
                    primaryEdition.Overview = secondaryEdition.Overview;
                }

                if (string.IsNullOrWhiteSpace(primaryEdition.Isbn13) && !string.IsNullOrWhiteSpace(secondaryEdition.Isbn13))
                {
                    primaryEdition.Isbn13 = secondaryEdition.Isbn13;
                }

                if (string.IsNullOrWhiteSpace(primaryEdition.Publisher) && !string.IsNullOrWhiteSpace(secondaryEdition.Publisher))
                {
                    primaryEdition.Publisher = secondaryEdition.Publisher;
                }

                if (string.IsNullOrWhiteSpace(primaryEdition.Disambiguation) && !string.IsNullOrWhiteSpace(secondaryEdition.Disambiguation))
                {
                    primaryEdition.Disambiguation = secondaryEdition.Disambiguation;
                }

                if (secondaryEdition.Links != null)
                {
                    primaryEdition.Links ??= new List<Links>();
                    foreach (var link in secondaryEdition.Links)
                    {
                        if (!primaryEdition.Links.Any(l =>
                            l.Name?.Equals(link.Name, StringComparison.OrdinalIgnoreCase) == true &&
                            l.Url?.Equals(link.Url, StringComparison.OrdinalIgnoreCase) == true))
                        {
                            primaryEdition.Links.Add(link);
                        }
                    }
                }
            }

            // Author: Prefer more complete author metadata
            if (primary.AuthorMetadata == null && secondary.AuthorMetadata != null)
            {
                primary.AuthorMetadata = secondary.AuthorMetadata;
            }
            else if (primary.AuthorMetadata != null && secondary.AuthorMetadata != null)
            {
                var primaryAuthor = primary.AuthorMetadata.Value;
                var secondaryAuthor = secondary.AuthorMetadata.Value;

                // Merge author links
                if (primaryAuthor.Links == null)
                {
                    primaryAuthor.Links = new List<Links>();
                }

                if (secondaryAuthor.Links != null)
                {
                    foreach (var link in secondaryAuthor.Links)
                    {
                        if (!primaryAuthor.Links.Any(l =>
                            l.Name?.Equals(link.Name, StringComparison.OrdinalIgnoreCase) == true))
                        {
                            primaryAuthor.Links.Add(link);
                        }
                    }
                }
            }

            if ((primary.Author == null || (primary.Author.IsLoaded && primary.Author.Value == null)) && secondary.Author != null)
            {
                primary.Author = secondary.Author;
            }

            return primary;
        }

        private int GetCompletenessScore(Book book)
        {
            var score = 0;

            if (!string.IsNullOrWhiteSpace(book.Title)) score += 10;
            if (book.ReleaseDate.HasValue) score += 5;
            if (book.Links != null && book.Links.Any()) score += 10;
            if (book.AuthorMetadata != null) score += 10;
            if (book.Ratings != null && book.Ratings.Votes > 0) score += 5;
            if (!string.IsNullOrWhiteSpace(book.ForeignBookId)) score += 5;
            var edition = book.Editions?.Value?.FirstOrDefault();
            if (edition != null)
            {
                if (!string.IsNullOrWhiteSpace(edition.Overview)) score += 10;
                if (!string.IsNullOrWhiteSpace(edition.Isbn13)) score += 5;
                if (!string.IsNullOrWhiteSpace(edition.Publisher)) score += 3;
                if (!string.IsNullOrWhiteSpace(edition.Disambiguation)) score += 2;
            }
            if (book.Editions != null && (!book.Editions.IsLoaded || (book.Editions.Value != null && book.Editions.Value.Any()))) score += 5;

            return score;
        }
    }
}
