using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Books.Import
{
    public enum ImportSearchStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public enum ImportSearchSource
    {
        Unknown,
        PubMed,
        RIS,
        Ovid,
        Embase
    }

    public enum ImportSearchItemStatus
    {
        Pending,
        Matched,
        Queued,
        Completed,
        Failed
    }

    public class ImportSearchJob : ModelBase
    {
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

    public class ImportSearchItem : ModelBase
    {
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
