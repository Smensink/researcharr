using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Import;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Validation;
using Readarr.Http;

namespace Readarr.Api.V1.ImportSearch
{
    [V1ApiController]
    [Route("/api/v1/importsearch")]
    public class ImportSearchController : Controller
    {
        private readonly IImportSearchService _importSearchService;
        private readonly IManageCommandQueue _commandQueue;

        public ImportSearchController(IImportSearchService importSearchService,
                                      IManageCommandQueue commandQueue)
        {
            _importSearchService = importSearchService;
            _commandQueue = commandQueue;
        }

        [HttpGet]
        public List<ImportSearchResource> GetAll()
        {
            return _importSearchService.AllJobs().Select(Map).ToList();
        }

        [HttpGet("{id:int}/items")]
        public List<ImportSearchItemResource> GetItems(int id)
        {
            return _importSearchService.GetItems(id).Select(Map).ToList();
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public ImportSearchResource Upload([FromForm] IFormFile file, [FromForm] string name)
        {
            if (file == null || file.Length == 0)
            {
                throw new ValidationException("file", "A file is required");
            }

            var jobName = string.IsNullOrWhiteSpace(name)
                ? Path.GetFileNameWithoutExtension(file.FileName)
                : name;

            using var stream = file.OpenReadStream();
            var items = _importSearchService.ParseStream(file.FileName, stream, out var source);

            if (items.Count == 0)
            {
                throw new ValidationException("file", "No records found in upload");
            }

            var job = _importSearchService.CreateJob(jobName, source, items);
            _commandQueue.Push(new ProcessImportSearchCommand(job.Id), CommandPriority.Normal, CommandTrigger.Manual);
            return Map(job);
        }

        [HttpPost("{id:int}/process")]
        public void Process(int id)
        {
            _commandQueue.Push(new ProcessImportSearchCommand(id), CommandPriority.Normal, CommandTrigger.Manual);
        }

        [HttpGet("query")]
        public List<ImportSearchItemResource> Search(string q)
        {
            var items = _importSearchService.ParseQuery(q, out var _);
            return items.Select(Map).ToList();
        }

        private static ImportSearchResource Map(ImportSearchJob job)
        {
            return new ImportSearchResource
            {
                Id = job.Id,
                Name = job.Name,
                Source = job.Source,
                Status = job.Status,
                Message = job.Message,
                Total = job.Total,
                Matched = job.Matched,
                Queued = job.Queued,
                Completed = job.Completed,
                Failed = job.Failed,
                Created = job.Created,
                Started = job.Started,
                Ended = job.Ended
            };
        }

        private static ImportSearchItemResource Map(ImportSearchItem item)
        {
            return new ImportSearchItemResource
            {
                Id = item.Id,
                JobId = item.JobId,
                Title = item.Title,
                Authors = item.Authors,
                Doi = item.Doi,
                Pmid = item.Pmid,
                Status = item.Status,
                Message = item.Message,
                BookId = item.BookId
            };
        }
    }
}
