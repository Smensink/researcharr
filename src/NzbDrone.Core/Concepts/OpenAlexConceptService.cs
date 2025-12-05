using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NLog;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Concepts
{
    public interface IOpenAlexConceptService
    {
        List<OpenAlexConceptDto> SearchConcepts(string query, int limit = 20);
        Task<ConceptImportResult> ImportFromOpenAlexAsync();
    }

    public class ConceptImportResult
    {
        public int Concepts { get; set; }
        public DateTime ImportedAt { get; set; }
    }

    public class OpenAlexConceptDto
    {
        public string OpenAlexId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int WorksCount { get; set; }
    }

    public class OpenAlexConceptService : IOpenAlexConceptService
    {
        private readonly IOpenAlexConceptRepository _conceptRepo;
        private readonly Logger _logger;

        public OpenAlexConceptService(IOpenAlexConceptRepository conceptRepo, Logger logger)
        {
            _conceptRepo = conceptRepo;
            _logger = logger;
        }

        public List<OpenAlexConceptDto> SearchConcepts(string query, int limit = 20)
        {
            if (query.IsNullOrWhiteSpace())
            {
                return new List<OpenAlexConceptDto>();
            }

            var concepts = _conceptRepo.SearchByName(query, limit);

            return concepts.Select(c => new OpenAlexConceptDto
            {
                OpenAlexId = c.OpenAlexId,
                DisplayName = c.DisplayName,
                Description = c.Description,
                Level = c.Level,
                WorksCount = c.WorksCount
            }).ToList();
        }

        public async Task<ConceptImportResult> ImportFromOpenAlexAsync()
        {
            using var client = new HttpClient();

            // OpenAlex API endpoint for all concepts, paginated
            var baseUrl = "https://api.openalex.org/concepts";
            var perPage = 200;
            string cursor = "*";
            var imported = 0;

            // Clear existing data up-front so partial imports still leave usable data
            _conceptRepo.DeleteMany(_conceptRepo.All().Select(c => c.Id).ToList());

            while (!string.IsNullOrEmpty(cursor))
            {
                var url = $"{baseUrl}?per_page={perPage}&cursor={cursor}";
                var response = await client.GetStringAsync(url);
                var apiResponse = JsonConvert.DeserializeObject<OpenAlexConceptApiResponse>(response);

                if (apiResponse?.Results == null || apiResponse.Results.Count == 0)
                {
                    break;
                }

                var batch = apiResponse.Results.Select(result => new OpenAlexConcept
                {
                    OpenAlexId = ExtractConceptId(result.Id),
                    DisplayName = result.DisplayName ?? string.Empty,
                    Description = result.Description ?? string.Empty,
                    Level = result.Level,
                    CitedByCount = result.CitedByCount,
                    WorksCount = result.WorksCount
                }).ToList();

                if (batch.Count > 0)
                {
                    _conceptRepo.InsertMany(batch);
                    imported += batch.Count;
                }

                _logger.Info("Imported {Imported} OpenAlex concepts so far (cursor {Cursor})", imported, cursor);

                cursor = apiResponse.Meta?.NextCursor;

                // Respect rate limits
                await Task.Delay(100);
            }

            return new ConceptImportResult
            {
                Concepts = imported,
                ImportedAt = DateTime.UtcNow
            };
        }

        private static string ExtractConceptId(string fullId)
        {
            // Convert "https://openalex.org/C71924100" to "C71924100"
            if (string.IsNullOrWhiteSpace(fullId))
            {
                return string.Empty;
            }

            var lastSlash = fullId.LastIndexOf('/');
            return lastSlash >= 0 ? fullId.Substring(lastSlash + 1) : fullId;
        }

        private class OpenAlexConceptApiResponse
        {
            [JsonProperty("results")]
            public List<OpenAlexConceptApiResult> Results { get; set; }

            [JsonProperty("meta")]
            public OpenAlexMeta Meta { get; set; }
        }

        private class OpenAlexConceptApiResult
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("level")]
            public int Level { get; set; }

            [JsonProperty("cited_by_count")]
            public int CitedByCount { get; set; }

            [JsonProperty("works_count")]
            public int WorksCount { get; set; }
        }

        private class OpenAlexMeta
        {
            [JsonProperty("next_cursor")]
            public string NextCursor { get; set; }
        }
    }
}
