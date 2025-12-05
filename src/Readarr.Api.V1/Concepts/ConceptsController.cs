using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Concepts;
using Readarr.Http;

namespace Readarr.Api.V1.Concepts
{
    [V1ApiController]
    public class ConceptsController : Controller
    {
        private readonly IOpenAlexConceptService _conceptService;

        public ConceptsController(IOpenAlexConceptService conceptService)
        {
            _conceptService = conceptService;
        }

        [HttpGet("search")]
        public List<ConceptResource> Search(string query, int limit = 20)
        {
            var concepts = _conceptService.SearchConcepts(query, limit);
            return concepts.Select(Map).ToList();
        }

        [HttpPost("import")]
        public async Task<ConceptImportResource> Import()
        {
            var result = await _conceptService.ImportFromOpenAlexAsync();
            return new ConceptImportResource
            {
                Concepts = result.Concepts,
                ImportedAt = result.ImportedAt
            };
        }

        private static ConceptResource Map(OpenAlexConceptDto dto)
        {
            return new ConceptResource
            {
                OpenAlexId = dto.OpenAlexId,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                Level = dto.Level,
                WorksCount = dto.WorksCount
            };
        }
    }
}
