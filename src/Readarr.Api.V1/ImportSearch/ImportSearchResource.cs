using System;
using NzbDrone.Core.Books.Import;

namespace Readarr.Api.V1.ImportSearch
{
    public class ImportSearchResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ImportSearchSource Source { get; set; }
        public ImportSearchStatus Status { get; set; }
        public string Message { get; set; }
        public int Total { get; set; }
        public int Matched { get; set; }
        public int Queued { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Started { get; set; }
        public DateTime? Ended { get; set; }
    }

    public class ImportSearchItemResource
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Doi { get; set; }
        public string Pmid { get; set; }
        public ImportSearchItemStatus Status { get; set; }
        public string Message { get; set; }
        public int? BookId { get; set; }
    }
}
