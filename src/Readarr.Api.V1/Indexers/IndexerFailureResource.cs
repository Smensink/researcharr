using System;
using NzbDrone.Core.Indexers;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Indexers
{
    public class IndexerFailureResource : RestResource
    {
        public int IndexerId { get; set; }
        public IndexerOperationType OperationType { get; set; }
        public IndexerErrorType ErrorType { get; set; }
        public string ErrorMessage { get; set; }
        public int? HttpStatusCode { get; set; }
        public DateTime Timestamp { get; set; }

        public IndexerFailureResource()
        {
        }

        public IndexerFailureResource(IndexerFailure model)
        {
            Id = model.Id;
            IndexerId = model.IndexerId;
            OperationType = model.OperationType;
            ErrorType = model.ErrorType;
            ErrorMessage = model.ErrorMessage;
            HttpStatusCode = model.HttpStatusCode;
            Timestamp = model.Timestamp;
        }
    }
}

