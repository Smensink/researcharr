using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.PubMedCentral
{
    public class PubMedCentralParser : IParseIndexerResponse
    {
        public PubMedCentralSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();

            if (indexerResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, "Unexpected Status Code {0}", indexerResponse.HttpResponse.StatusCode);
            }

            try
            {
                var xml = XDocument.Parse(indexerResponse.Content);

                // Parse esearch results to get PMC IDs
                var idList = xml.Descendants("Id").Select(e => e.Value).ToList();

                if (!idList.Any())
                {
                    return releases;
                }

                // For each PMC ID, we need to fetch detailed info
                // In a production system, we'd batch fetch using efetch
                // For now, we'll construct basic ReleaseInfo from PMC IDs
                foreach (var pmcId in idList.Take(20)) // Limit to first 20 to avoid rate limiting
                {
                    try
                    {
                        var release = BuildReleaseFromPmcId(pmcId);
                        if (release != null)
                        {
                            releases.Add(release);
                        }
                    }
                    catch
                    {
                        // Skip failed entries
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list if parsing fails
            }

            return releases;
        }

        private ReleaseInfo BuildReleaseFromPmcId(string pmcId)
        {
            if (string.IsNullOrEmpty(pmcId))
            {
                return null;
            }

            // PMC PDF URL pattern: https://www.ncbi.nlm.nih.gov/pmc/articles/PMC{id}/pdf/
            var pdfUrl = $"https://www.ncbi.nlm.nih.gov/pmc/articles/PMC{pmcId}/pdf/";
            var infoUrl = $"https://www.ncbi.nlm.nih.gov/pmc/articles/PMC{pmcId}/";

            // Note: We don't have full metadata without efetch call
            // In a more complete implementation, we'd fetch title/author/journal from efetch
            // Journal would be in <MedlineCitation><Article><Journal><Title> element
            var release = new ReleaseInfo
            {
                Guid = $"PMC-{pmcId}",
                Title = $"PMC{pmcId}", // Placeholder - would need efetch for real title
                Author = "Unknown Author", // Placeholder - would need efetch for real author
                Book = $"PMC{pmcId}", // Placeholder - would need efetch for real title
                Doi = null, // Would need efetch to get DOI
                Source = null, // Would need efetch to get journal from <Journal><Title> element
                DownloadUrl = pdfUrl,
                InfoUrl = infoUrl,
                Size = 0,
                PublishDate = DateTime.UtcNow,
                DownloadProtocol = DownloadProtocol.Http,
                Categories = new List<int> { 8000 } // ebooks/papers category
            };

            return release;
        }
    }
}
