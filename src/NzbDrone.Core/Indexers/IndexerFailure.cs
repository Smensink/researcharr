using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Indexers
{
    public enum IndexerOperationType
    {
        RssSync = 0,
        Search = 1,
        Test = 2,
        Parse = 3,
        Download = 4
    }

    public enum IndexerErrorType
    {
        HttpError = 0,
        Timeout = 1,
        ParseError = 2,
        AuthError = 3,
        ConnectionFailure = 4,
        RateLimit = 5,
        CloudflareCaptcha = 6,
        Unknown = 7
    }

    public class IndexerFailure : ModelBase
    {
        public int IndexerId { get; set; }
        public IndexerOperationType OperationType { get; set; }
        public IndexerErrorType ErrorType { get; set; }
        public string ErrorMessage { get; set; }
        public int? HttpStatusCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

