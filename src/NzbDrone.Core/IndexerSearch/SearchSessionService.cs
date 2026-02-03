using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.IndexerSearch
{
    public interface ISearchSessionService
    {
        SearchSession CreateSession(SearchCriteriaBase criteria, IEnumerable<IIndexer> indexers, int? bookId = null, int? authorId = null);
        SearchSession GetSession(string searchId);
        List<DownloadDecision> MergeDecisions(string searchId, IIndexer indexer, List<DownloadDecision> newDecisions);
        void MarkIndexerComplete(string searchId, int indexerId, int resultCount);
        void MarkIndexerFailed(string searchId, int indexerId, string errorMessage);
        void MarkSearchComplete(string searchId);
        List<DownloadDecision> GetAllDecisions(string searchId);
    }

    public class SearchSessionService : ISearchSessionService
    {
        private readonly ICached<SearchSession> _sessionCache;
        private readonly Logger _logger;

        private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(5);

        public SearchSessionService(ICacheManager cacheManager, Logger logger)
        {
            _sessionCache = cacheManager.GetCache<SearchSession>(GetType(), "sessions");
            _logger = logger;
        }

        public SearchSession CreateSession(SearchCriteriaBase criteria, IEnumerable<IIndexer> indexers, int? bookId = null, int? authorId = null)
        {
            var session = new SearchSession
            {
                Criteria = criteria,
                BookId = bookId,
                AuthorId = authorId
            };

            foreach (var indexer in indexers)
            {
                var indexerId = indexer.Definition.Id;
                session.PendingIndexers.Add(indexerId);
                session.IndexerStatuses[indexerId] = new IndexerSearchStatus
                {
                    IndexerId = indexerId,
                    IndexerName = indexer.Definition.Name,
                    State = SearchIndexerState.Searching,
                    ResultCount = 0
                };
            }

            _sessionCache.Set(session.SearchId, session, SessionTtl);
            _logger.Debug("Created search session {0} with {1} indexers", session.SearchId, session.TotalIndexers);

            return session;
        }

        public SearchSession GetSession(string searchId)
        {
            return _sessionCache.Find(searchId);
        }

        public List<DownloadDecision> MergeDecisions(string searchId, IIndexer indexer, List<DownloadDecision> newDecisions)
        {
            var session = GetSession(searchId);
            if (session == null)
            {
                _logger.Warn("Search session {0} not found for merge", searchId);
                return new List<DownloadDecision>();
            }

            var addedOrUpdated = new List<DownloadDecision>();

            foreach (var decision in newDecisions)
            {
                var guid = decision.RemoteBook?.Release?.Guid;
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                // Try to add or update if better
                var added = session.BestDecisionByGuid.AddOrUpdate(
                    guid,
                    decision,
                    (key, existing) =>
                    {
                        // Keep the one with fewer rejections, or higher indexer priority if tied
                        if (decision.Rejections.Count() < existing.Rejections.Count())
                        {
                            return decision;
                        }

                        if (decision.Rejections.Count() == existing.Rejections.Count())
                        {
                            var newPriority = decision.RemoteBook?.Release?.IndexerPriority ?? IndexerDefinition.DefaultPriority;
                            var existingPriority = existing.RemoteBook?.Release?.IndexerPriority ?? IndexerDefinition.DefaultPriority;

                            if (newPriority < existingPriority)
                            {
                                return decision;
                            }
                        }

                        return existing;
                    });

                // Track if this was actually added or if it updated (improved) an existing one
                if (ReferenceEquals(added, decision))
                {
                    addedOrUpdated.Add(decision);
                }
            }

            _logger.Debug("Merged {0} decisions from {1}, {2} were new/better", newDecisions.Count, indexer.Definition.Name, addedOrUpdated.Count);

            return addedOrUpdated;
        }

        public void MarkIndexerComplete(string searchId, int indexerId, int resultCount)
        {
            var session = GetSession(searchId);
            if (session == null)
            {
                return;
            }

            session.PendingIndexers.Remove(indexerId);
            session.CompletedIndexers.Add(indexerId);

            if (session.IndexerStatuses.TryGetValue(indexerId, out var status))
            {
                status.State = SearchIndexerState.Completed;
                status.ResultCount = resultCount;
                status.CompletedAt = DateTime.UtcNow;
            }

            _logger.Debug("Indexer {0} completed for session {1}, {2}/{3} complete",
                indexerId, searchId, session.CompletedCount, session.TotalIndexers);
        }

        public void MarkIndexerFailed(string searchId, int indexerId, string errorMessage)
        {
            var session = GetSession(searchId);
            if (session == null)
            {
                return;
            }

            session.PendingIndexers.Remove(indexerId);
            session.CompletedIndexers.Add(indexerId);

            if (session.IndexerStatuses.TryGetValue(indexerId, out var status))
            {
                status.State = SearchIndexerState.Failed;
                status.ErrorMessage = errorMessage;
                status.CompletedAt = DateTime.UtcNow;
            }

            _logger.Debug("Indexer {0} failed for session {1}: {2}", indexerId, searchId, errorMessage);
        }

        public void MarkSearchComplete(string searchId)
        {
            var session = GetSession(searchId);
            if (session == null)
            {
                return;
            }

            session.IsComplete = true;
            _logger.Debug("Search session {0} complete with {1} total results", searchId, session.BestDecisionByGuid.Count);
        }

        public List<DownloadDecision> GetAllDecisions(string searchId)
        {
            var session = GetSession(searchId);
            if (session == null)
            {
                return new List<DownloadDecision>();
            }

            return session.BestDecisionByGuid.Values.ToList();
        }
    }
}
