using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NzbDrone.Core.Mesh;
using Readarr.Http;

namespace Readarr.Api.V1.Mesh
{
    [V1ApiController]
    [Route("/api/v1/mesh")]
    [Route("/api/v1/api/v1/mesh")] // tolerate double-prefix clients
    public class MeshController : Controller
    {
        private readonly IMeshService _meshService;
        private readonly ILogger<MeshController> _logger;

        public MeshController(IMeshService meshService, ILogger<MeshController> logger)
        {
            _meshService = meshService;
            _logger = logger;
        }

        [HttpGet("search")]
        public List<MeshDescriptorResource> Search(string query, int limit = 20)
        {
            return _meshService.SearchTerms(query, limit).Select(Map).ToList();
        }

        [HttpGet("{descriptorUi}")]
        public MeshDescriptorResource Get(string descriptorUi)
        {
            var dto = _meshService.GetDescriptor(descriptorUi);
            return dto == null ? null : Map(dto);
        }

        [HttpGet("{descriptorUi}/explode")]
        public List<MeshDescriptorResource> Explode(string descriptorUi)
        {
            return _meshService.Explode(descriptorUi).Select(Map).ToList();
        }

        [HttpGet("import")]
        public Task<ActionResult<MeshImportResource>> ImportFromUrl([FromQuery] string url = null)
        {
            return ImportInternal(url, null);
        }

        [HttpPost("import")]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public Task<ActionResult<MeshImportResource>> Import([FromForm] string url = null,
                                                             [FromForm] Microsoft.AspNetCore.Http.IFormFile file = null)
        {
            return ImportInternal(url, file);
        }

        private async Task<ActionResult<MeshImportResource>> ImportInternal(string url, Microsoft.AspNetCore.Http.IFormFile file)
        {
            MeshImportResult result;

            var sourceUrl = string.IsNullOrWhiteSpace(url) ? "https://nlmpubs.nlm.nih.gov/projects/mesh/MESH_FILES/xmlmesh/desc2025.xml" : url;
            _logger.LogInformation("MeSH import requested via {Method}. url={Url} file={HasFile} contentType={ContentType}",
                Request?.Method, sourceUrl, file != null, Request?.ContentType);

            try
            {
                if (file != null && file.Length > 0)
                {
                    using var stream = file.OpenReadStream();
                    result = _meshService.ImportFromStream(stream, file.FileName);
                }
                else
                {
                    result = await _meshService.ImportAsync(sourceUrl);
                }
            }
            catch (global::System.Exception ex)
            {
                _logger.LogError(ex, "MeSH import failed. url={Url} file={HasFile}", sourceUrl, file != null);
                return BadRequest(new { message = ex.Message });
            }

            _logger.LogInformation("MeSH import complete. descriptors={Descriptors} terms={Terms} version={Version} source={Source}",
                result?.Descriptors, result?.Terms, result?.Version, result?.SourceUrl);

            return Ok(new MeshImportResource
            {
                Descriptors = result.Descriptors,
                Terms = result.Terms,
                Version = result.Version,
                SourceUrl = result.SourceUrl
            });
        }

        private static MeshDescriptorResource Map(MeshDescriptorDto dto)
        {
            return new MeshDescriptorResource
            {
                DescriptorUi = dto.DescriptorUi,
                PreferredTerm = dto.PreferredTerm,
                TreeNumbers = dto.TreeNumbers,
                ScopeNote = dto.ScopeNote,
                Synonyms = dto.Synonyms
            };
        }
    }
}
