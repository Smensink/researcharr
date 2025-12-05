using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Mesh
{
    public interface IMeshService
    {
        List<MeshDescriptorDto> SearchTerms(string query, int limit = 20);
        MeshDescriptorDto GetDescriptor(string descriptorUi);
        List<MeshDescriptorDto> Explode(string descriptorUi);
        Task<MeshImportResult> ImportAsync(string sourceUrl);
        MeshImportResult ImportFromStream(System.IO.Stream stream, string sourceName);
    }

    public class MeshImportResult
    {
        public int Descriptors { get; set; }
        public int Terms { get; set; }
        public string Version { get; set; }
        public string SourceUrl { get; set; }
    }

    public class MeshDescriptorDto
    {
        public string DescriptorUi { get; set; }
        public string PreferredTerm { get; set; }
        public List<string> TreeNumbers { get; set; }
        public string ScopeNote { get; set; }
        public List<string> Synonyms { get; set; }
    }

    public class MeshService : IMeshService
    {
        private readonly IMeshDescriptorRepository _descriptorRepo;
        private readonly IMeshTermRepository _termRepo;
        private readonly IMeshMetadataRepository _metadataRepo;

        public MeshService(IMeshDescriptorRepository descriptorRepo, IMeshTermRepository termRepo)
        {
            _descriptorRepo = descriptorRepo;
            _termRepo = termRepo;
            _metadataRepo = null;
        }

        public MeshService(IMeshDescriptorRepository descriptorRepo, IMeshTermRepository termRepo, IMeshMetadataRepository metadataRepo)
        {
            _descriptorRepo = descriptorRepo;
            _termRepo = termRepo;
            _metadataRepo = metadataRepo;
        }

        public List<MeshDescriptorDto> SearchTerms(string query, int limit = 20)
        {
            if (query.IsNullOrWhiteSpace())
            {
                return new List<MeshDescriptorDto>();
            }

            var lower = query.ToLowerInvariant();

            // Use indexed SQL-based search instead of loading all terms into memory
            var matchingTerms = _termRepo.SearchByTerm(lower, limit * 5);

            // Get unique descriptor UIs from matching terms
            var descriptorUis = matchingTerms.Select(t => t.DescriptorUi).Distinct().Take(limit).ToList();

            // Fetch the actual descriptors
            var descriptors = _descriptorRepo.All()
                .Where(d => descriptorUis.Contains(d.DescriptorUi))
                .ToList();

            return descriptors.Select(Map).ToList();
        }

        public MeshDescriptorDto GetDescriptor(string descriptorUi)
        {
            var descriptor = _descriptorRepo.All().FirstOrDefault(d => d.DescriptorUi == descriptorUi);
            if (descriptor == null)
            {
                return null;
            }

            return Map(descriptor);
        }

        public List<MeshDescriptorDto> Explode(string descriptorUi)
        {
            var descriptor = _descriptorRepo.All().FirstOrDefault(d => d.DescriptorUi == descriptorUi);
            if (descriptor == null)
            {
                return new List<MeshDescriptorDto>();
            }

            var treeNumbers = (descriptor.TreeNumbers ?? string.Empty).Split(',').Where(x => !x.IsNullOrWhiteSpace()).ToList();
            var allDescriptors = _descriptorRepo.All().ToList();

            var exploded = new List<MeshDescriptorDto> { Map(descriptor) };

            foreach (var tn in treeNumbers)
            {
                var children = allDescriptors.Where(d => (d.TreeNumbers ?? string.Empty).Split(',')
                    .Any(t => t.StartsWith(tn) && t != tn)).ToList();
                exploded.AddRange(children.Select(Map));
            }

            return exploded.DistinctBy(x => x.DescriptorUi).ToList();
        }

        public async Task<MeshImportResult> ImportAsync(string sourceUrl)
        {
            if (sourceUrl.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Mesh source URL is required", nameof(sourceUrl));
            }

            using var client = new HttpClient();
            var xml = await client.GetStringAsync(sourceUrl);

            using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            return ImportFromStream(stream, sourceUrl);
        }

        public MeshImportResult ImportFromStream(System.IO.Stream stream, string sourceName)
        {
            using var reader = new System.IO.StreamReader(stream);
            var content = reader.ReadToEnd();

            var doc = XDocument.Parse(content);
            var descRecords = doc.Descendants("DescriptorRecord").ToList();

            // Wipe existing data
            _descriptorRepo.DeleteMany(_descriptorRepo.All().Select(d => d.Id).ToList());
            _termRepo.DeleteMany(_termRepo.All().Select(t => t.Id).ToList());

            var descriptors = new List<MeshDescriptor>();
            var terms = new List<MeshTerm>();

            foreach (var record in descRecords)
            {
                var ui = record.Element("DescriptorUI")?.Value;
                var pref = record.Element("DescriptorName")?.Element("String")?.Value;
                var scope = record.Element("ScopeNote")?.Value;
                var treeNumbers = record.Element("TreeNumberList")?.Elements("TreeNumber").Select(x => x.Value).ToList() ?? new List<string>();

                if (ui.IsNullOrWhiteSpace())
                {
                    continue;
                }

                descriptors.Add(new MeshDescriptor
                {
                    DescriptorUi = ui,
                    PreferredTerm = pref,
                    ScopeNote = scope,
                    TreeNumbers = string.Join(",", treeNumbers)
                });

                var termStrings = record.Descendants("TermList").Descendants("Term").Select(t => t.Element("String")?.Value).Where(x => !x.IsNullOrWhiteSpace()).Distinct().ToList();
                foreach (var t in termStrings)
                {
                    terms.Add(new MeshTerm
                    {
                        DescriptorUi = ui,
                        Term = t,
                        IsPreferred = string.Equals(t, pref, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }

            if (descriptors.Count > 0)
            {
                _descriptorRepo.InsertMany(descriptors);
            }

            if (terms.Count > 0)
            {
                _termRepo.InsertMany(terms);
            }

            if (_metadataRepo != null)
            {
                _metadataRepo.DeleteMany(_metadataRepo.All().Select(m => m.Id).ToList());
                _metadataRepo.Insert(new MeshMetadata
                {
                    SourceUrl = sourceName,
                    Version = DateTime.UtcNow.Year.ToString(),
                    ImportedAt = DateTime.UtcNow
                });
            }

            return new MeshImportResult
            {
                Descriptors = descriptors.Count,
                Terms = terms.Count,
                Version = DateTime.UtcNow.Year.ToString(),
                SourceUrl = sourceName
            };
        }

        private MeshDescriptorDto Map(MeshDescriptor descriptor)
        {
            var terms = _termRepo.FindByDescriptor(descriptor.DescriptorUi);
            return new MeshDescriptorDto
            {
                DescriptorUi = descriptor.DescriptorUi,
                PreferredTerm = descriptor.PreferredTerm,
                TreeNumbers = (descriptor.TreeNumbers ?? string.Empty).Split(',').Where(x => !x.IsNullOrWhiteSpace()).ToList(),
                ScopeNote = descriptor.ScopeNote,
                Synonyms = terms.Select(t => t.Term).Distinct().ToList()
            };
        }
    }
}
