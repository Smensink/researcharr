import classNames from 'classnames';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './AdvancedSearch.css';

const typeOptions = [
  { value: '', label: 'Any type' },
  { value: 'journal-article', label: 'Article' },
  { value: 'review-article', label: 'Review article' },
  { value: 'posted-content', label: 'Preprint' }
];

const sortOptions = [
  { value: '', label: 'Relevance' },
  { value: 'cited_by_count:desc', label: 'Most cited' },
  { value: 'publication_date:desc', label: 'Newest first' },
  { value: 'publication_date:asc', label: 'Oldest first' }
];

const perPageOptions = [25, 50, 100];

function AdvancedSearch() {
  const [searchQuery, setSearchQuery] = useState('');
  const [yearFrom, setYearFrom] = useState('');
  const [yearTo, setYearTo] = useState('');
  const [articleType, setArticleType] = useState('');
  const [isOaOnly, setIsOaOnly] = useState(false);
  const [sort, setSort] = useState('');
  const [perPage, setPerPage] = useState(25);
  const [maxResults, setMaxResults] = useState(1000);
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState('');
  const [streaming, setStreaming] = useState(false);
  const [fetchedCount, setFetchedCount] = useState(0);
  const [streamPagesFetched, setStreamPagesFetched] = useState(0);
  const [stopStreaming, setStopStreaming] = useState(false);
  const [streamCursor, setStreamCursor] = useState('*');

  const [conceptQuery, setConceptQuery] = useState('');
  const [conceptSuggestions, setConceptSuggestions] = useState([]);
  const [selectedConcepts, setSelectedConcepts] = useState([]);

  const [meshQuery, setMeshQuery] = useState('');
  const [meshSuggestions, setMeshSuggestions] = useState([]);
  const [meshSelections, setMeshSelections] = useState([]);

  const [nextCursor, setNextCursor] = useState(null);
  const [cursorHistory, setCursorHistory] = useState(['*']);
  const [savedSearches, setSavedSearches] = useState([]);
  const [saveName, setSaveName] = useState('');

  const [selectedWorks, setSelectedWorks] = useState([]);
  const [bookStatuses, setBookStatuses] = useState({});

  // Logic operators for search components
  const [keywordMeshOperator, setKeywordMeshOperator] = useState('AND');
  const [searchConceptOperator, setSearchConceptOperator] = useState('AND');
  const [conceptGroupOperators, setConceptGroupOperators] = useState({ 1: 'OR', 2: 'OR', 3: 'OR' });
  const [meshGroupOperators, setMeshGroupOperators] = useState({ 1: 'OR', 2: 'OR', 3: 'OR' });

  // Constants
  const streamMaxPages = 10000; // Effectively unlimited (will stop when no more results)

  // Refs to avoid stale closures in streaming
  const stopStreamingRef = useRef(false);
  const fetchedCountRef = useRef(0);
  const streamPagesFetchedRef = useRef(0);
  const conceptRequestRef = useRef(null);
  const meshRequestRef = useRef(null);

  const buildFilterString = useCallback((includeConcepts = true) => {
    const filters = [];

    if (yearFrom || yearTo) {
      const from = yearFrom || '';
      const to = yearTo || '';
      filters.push(`publication_year:${from}-${to}`);
    }

    if (articleType) {
      filters.push(`type:${articleType}`);
    }

    if (isOaOnly) {
      filters.push('is_oa:true');
    }

    if (includeConcepts && selectedConcepts.length > 0) {
      // Group concepts and combine per group; then combine groups with per-group operators
      const groups = new Map();
      selectedConcepts.forEach((c) => {
        const gid = c.groupId || 1;
        const entry = groups.get(gid) || { required: [], optional: [] };
        const cleanId = c.id.replace('https://openalex.org/', '');
        if (c.required === false) {
          entry.optional.push(cleanId);
        } else {
          entry.required.push(cleanId);
        }
        groups.set(gid, entry);
      });

      const groupedClauses = [];
      groups.forEach((group, gid) => {
        const parts = [];
        group.required.forEach((id) => parts.push(`concepts.id:${id}`));
        if (group.optional.length) {
          parts.push(`concepts.id:${group.optional.join('|')}`);
        }
        if (parts.length) {
          groupedClauses.push({ gid, clause: parts.length === 1 ? parts[0] : parts.join(',') });
        }
      });

      // Sort by group id for deterministic combination
      groupedClauses.sort((a, b) => (a.gid || 0) - (b.gid || 0));

      // Combine groups with per-group operators (each group operator applies when merging it into the running expression)
      groupedClauses.forEach((item, idx) => {
        if (idx === 0) {
          filters.push(item.clause);
        } else {
          const op = conceptGroupOperators[item.gid] || 'OR';
          if (op === 'AND') {
            filters.push(item.clause);
          } else {
            // OR: merge into a single OR filter
            const flattened = [];
            filters.forEach((f) => {
              if (f.startsWith('concepts.id:')) {
                flattened.push(f.replace('concepts.id:', ''));
              }
            });
            const current = item.clause.startsWith('concepts.id:') ? item.clause.replace('concepts.id:', '') : item.clause;
            if (flattened.length || current) {
              const combined = flattened.concat(current.split(',')).filter(Boolean).join('|');
              // Remove existing concept id filters before adding merged OR
              const nonConcept = filters.filter((f) => !f.startsWith('concepts.id:'));
              nonConcept.push(`concepts.id:${combined}`);
              filters.splice(0, filters.length, ...nonConcept);
            }
          }
        }
      });
    }

    return filters.join(',');
  }, [articleType, conceptGroupOperators, isOaOnly, selectedConcepts, yearFrom, yearTo]);

  const meshSearchClause = useMemo(() => {
    if (!meshSelections.length) {
      return '';
    }

    const groups = new Map();
    meshSelections.forEach((sel) => {
      const gid = sel.groupId || 1;
      const entry = groups.get(gid) || { required: [], optional: [] };
      const terms = [sel.preferredTerm, ...(sel.synonyms || [])].filter(Boolean);
      if (!terms.length) {
        return;
      }
      const clause = `(${terms.map((t) => `"${t}"`).join(' OR ')})`;
      if (sel.required === false) {
        entry.optional.push(clause);
      } else {
        entry.required.push(clause);
      }
      groups.set(gid, entry);
    });

    const groupedClauses = [];
    groups.forEach((group, gid) => {
      const requiredPart = group.required.length ? group.required.join(' AND ') : '';
      const optionalPart = group.optional.length ? group.optional.join(' OR ') : '';

      let clause = '';
      if (requiredPart && optionalPart) {
        clause = `(${requiredPart}) OR (${optionalPart})`;
      } else if (requiredPart) {
        clause = requiredPart;
      } else if (optionalPart) {
        clause = optionalPart;
      }

      if (clause) {
        groupedClauses.push({ gid, clause });
      }
    });

    if (!groupedClauses.length) {
      return '';
    }

    groupedClauses.sort((a, b) => (a.gid || 0) - (b.gid || 0));

    return groupedClauses.reduce((acc, current, idx) => {
      if (idx === 0) {
        return current.clause;
      }

      const op = meshGroupOperators[current.gid] || 'OR';
      return `(${acc}) ${op} (${current.clause})`;
    }, '');
  }, [meshGroupOperators, meshSelections]);

  const conceptFilterForOrMerge = useMemo(() => {
    const optional = selectedConcepts.filter((c) => c.required === false).map((c) => c.id.replace('https://openalex.org/', ''));
    if (optional.length === 0) {
      return '';
    }
    return optional;
  }, [selectedConcepts]);

  const fullSearchString = useMemo(() => {
    return [searchQuery.trim(), meshSearchClause].filter(Boolean).join(` ${keywordMeshOperator} `);
  }, [keywordMeshOperator, meshSearchClause, searchQuery]);

  const fetchSaved = useCallback(() => {
    createAjaxRequest({
      url: '/advancedsearch/saved',
      method: 'GET',
      dataType: 'json'
    }).request
      .then((data) => setSavedSearches(data || []))
      .fail((xhr, textStatus, error) => {
        console.error('[AdvancedSearch] Failed to fetch saved searches:', { xhr, textStatus, error });
        setSavedSearches([]);
      });
  }, []);

  useEffect(() => {
    fetchSaved();
  }, [fetchSaved]);

  const fetchConcepts = useCallback((query) => {
    console.log('[AdvancedSearch] fetchConcepts called with query:', query);
    if (!query || query.length < 2) {
      setConceptSuggestions([]);
      return;
    }

    // Cancel previous request
    if (conceptRequestRef.current) {
      console.log('[AdvancedSearch] Aborting previous concept request');
      conceptRequestRef.current.abortRequest();
    }

    const ajaxRequest = createAjaxRequest({
      url: `/concepts/search?query=${encodeURIComponent(query)}&limit=20`,
      method: 'GET',
      dataType: 'json'
    });

    conceptRequestRef.current = ajaxRequest;

    ajaxRequest.request
      .then((data) => {
        console.log('[AdvancedSearch] Concepts received:', data);
        setConceptSuggestions((data || []).map((c) => ({
          id: c.openAlexId ? `https://openalex.org/${c.openAlexId}` : c.id,
          name: c.displayName || c.name || '',
          required: true,
          groupId: 1
        })));
      })
      .fail((xhr, textStatus, error) => {
        console.error('[AdvancedSearch] Concepts request failed:', { xhr, textStatus, error, aborted: xhr.aborted });
        // Don't clear suggestions if request was aborted (user is still typing)
        if (!xhr.aborted) {
          setConceptSuggestions([]);
        }
      });
  }, []);

  const fetchMesh = useCallback((query) => {
    console.log('[AdvancedSearch] fetchMesh called with query:', query);
    if (!query || query.length < 2) {
      setMeshSuggestions([]);
      return;
    }

    // Cancel previous request
    if (meshRequestRef.current) {
      console.log('[AdvancedSearch] Aborting previous mesh request');
      meshRequestRef.current.abortRequest();
    }

    const ajaxRequest = createAjaxRequest({
      url: `/mesh/search?query=${encodeURIComponent(query)}`,
      method: 'GET',
      dataType: 'json'
    });

    meshRequestRef.current = ajaxRequest;

    ajaxRequest.request
      .then((data) => {
        console.log('[AdvancedSearch] MeSH received:', data);
        setMeshSuggestions((data || []).map((m) => ({
          descriptorUi: m.descriptorUi,
          preferredTerm: m.preferredTerm || '',
          synonyms: m.synonyms || [],
          required: true,
          groupId: 1
        })));
      })
      .fail((xhr, textStatus, error) => {
        console.error('[AdvancedSearch] MeSH request failed:', { xhr, textStatus, error, aborted: xhr.aborted });
        // Don't clear suggestions if request was aborted (user is still typing)
        if (!xhr.aborted) {
          setMeshSuggestions([]);
        }
      });
  }, []);

  useEffect(() => {
    if (!conceptQuery) {
      setConceptSuggestions([]);
      return undefined;
    }
    const timer = setTimeout(() => fetchConcepts(conceptQuery), 120);
    return () => clearTimeout(timer);
  }, [conceptQuery, fetchConcepts]);

  useEffect(() => {
    if (!meshQuery) {
      setMeshSuggestions([]);
      return undefined;
    }
    const timer = setTimeout(() => fetchMesh(meshQuery), 120);
    return () => clearTimeout(timer);
  }, [meshQuery, fetchMesh]);

  const mergeResults = useCallback((existing, incoming) => {
    if (!incoming || !incoming.length) {
      return existing;
    }

    const map = new Map();
    existing.forEach((r) => {
      if (r?.openAlexId) {
        map.set(r.openAlexId, r);
      }
    });

    incoming.forEach((r) => {
      if (r?.openAlexId && !map.has(r.openAlexId)) {
        map.set(r.openAlexId, r);
      }
    });

    return Array.from(map.values());
  }, []);

  const fetchStatuses = useCallback((openAlexIds) => {
    if (!openAlexIds || openAlexIds.length === 0) {
      return;
    }

    createAjaxRequest({
      url: '/advancedsearch/status',
      method: 'POST',
      data: JSON.stringify(openAlexIds),
      contentType: 'application/json',
      dataType: 'json'
    }).request.then((statuses) => {
      if (statuses && Array.isArray(statuses)) {
        const statusMap = {};
        statuses.forEach((s) => {
          if (s && s.openAlexId) {
            statusMap[s.openAlexId] = s;
          }
        });
        setBookStatuses((prev) => ({ ...prev, ...statusMap }));
      }
    }).fail((xhr, textStatus, error) => {
      // Silently fail - status fetching is optional
      if (xhr.status !== 0) { // Only log if not aborted
        console.error('[AdvancedSearch] Failed to fetch statuses:', { xhr, textStatus, error });
      }
    });
  }, []);

  const requestWorksPage = useCallback((cursorValue = '*') => {
    const hasSearch = fullSearchString.trim().length > 0;
    const hasConcepts = selectedConcepts.length > 0;

    // If OR operator and both search and concepts exist, make two calls and merge
    if (searchConceptOperator === 'OR' && hasSearch && hasConcepts) {
      const call1 = createAjaxRequest({
        url: '/advancedsearch/works',
        method: 'GET',
        dataType: 'json',
        data: {
          search: fullSearchString,
          filter: buildFilterString(false), // Exclude concepts
          sort,
          perPage,
          cursor: cursorValue
        }
      }).request;

      const call2 = createAjaxRequest({
        url: '/advancedsearch/works',
        method: 'GET',
        dataType: 'json',
        data: {
          search: '', // No search, just concepts
          filter: buildFilterString(true), // Include concepts
          sort,
          perPage,
          cursor: cursorValue
        }
      }).request;

      // Merge results from both calls
      return Promise.all([call1, call2]).then(([data1, data2]) => {
        const merged = mergeResults(data1?.results || [], data2?.results || []);
        return {
          results: merged,
          nextCursor: data1?.nextCursor || data2?.nextCursor || null
        };
      });
    }

    // Normal AND behavior
    return createAjaxRequest({
      url: '/advancedsearch/works',
      method: 'GET',
      dataType: 'json',
      data: {
        search: fullSearchString,
        filter: buildFilterString(),
        sort,
        perPage,
        cursor: cursorValue
      }
    }).request;
  }, [buildFilterString, fullSearchString, mergeResults, perPage, searchConceptOperator, selectedConcepts.length, sort]);

  const fetchWorks = useCallback((cursorValue = '*', trailOverride = null, overrides = {}, append = false) => {
    setLoading(true);
    setStatus('Searching OpenAlex…');
    setSelectedWorks([]);

    const filterString = overrides.filterString ?? buildFilterString();
    const searchString = overrides.searchString ?? fullSearchString;
    const sortString = overrides.sortString ?? sort;
    const perPageValue = overrides.perPage ?? perPage;

    createAjaxRequest({
      url: '/advancedsearch/works',
      method: 'GET',
      dataType: 'json',
      data: {
        search: searchString,
        filter: filterString,
        sort: sortString,
        perPage: perPageValue,
        cursor: cursorValue
      }
    }).request.then((data) => {
      const pageResults = data?.results || [];
      setResults((prev) => {
        const merged = append ? mergeResults(prev, pageResults) : pageResults;
        // Respect maxResults when manually paging
        return merged.slice(0, maxResults);
      });
      setNextCursor(data?.nextCursor || null);
      setFetchedCount((prev) => {
        const newCount = append ? prev + pageResults.length : pageResults.length;
        return Math.min(newCount, maxResults);
      });

      if (trailOverride) {
        setCursorHistory(trailOverride);
      } else {
        setCursorHistory((prev) => {
          const trimmed = prev.slice();
          if (!trimmed.length || trimmed[trimmed.length - 1] !== cursorValue) {
            trimmed.push(cursorValue);
          }
          return trimmed;
        });
      }

      if (!pageResults.length) {
        setStatus('No results for this query');
      } else {
        const total = append ? Math.min(fetchedCount + pageResults.length, maxResults) : Math.min(pageResults.length, maxResults);
        setStatus(`Showing ${total} result${total === 1 ? '' : 's'}`);
        // Fetch statuses for the results
        const ids = pageResults.map((r) => r.openAlexId).filter(Boolean);
        if (ids.length > 0) {
          fetchStatuses(ids);
        }
      }
    }).fail(() => setStatus('Search failed')).always(() => setLoading(false));
  }, [buildFilterString, fetchedCount, fullSearchString, maxResults, mergeResults, perPage, sort, fetchStatuses]);

  const runSearch = () => {
    setCursorHistory(['*']);
    setStopStreaming(false);
    stopStreamingRef.current = false;
    setStreaming(true);
    setLoading(true);
    setStatus('Starting search...');
    setResults([]);
    setFetchedCount(0);
    fetchedCountRef.current = 0;
    setStreamPagesFetched(0);
    streamPagesFetchedRef.current = 0;
    setStreamCursor('*');
    streamSearch('*', streamMaxPages);
  };

  const goNext = () => {
    if (!nextCursor) {
      return;
    }

    const nextTrail = cursorHistory.concat([nextCursor]);
    fetchWorks(nextCursor, nextTrail);
  };

  const goPrev = () => {
    if (cursorHistory.length <= 1) {
      return;
    }

    const previousTrail = cursorHistory.slice(0, -1);
    const prevCursor = previousTrail[previousTrail.length - 1];
    fetchWorks(prevCursor, previousTrail);
  };

  const toggleSelection = (id) => {
    setSelectedWorks((prev) => {
      if (prev.includes(id)) {
        return prev.filter((x) => x !== id);
      }
      return prev.concat([id]);
    });
  };

  const addWork = (openAlexId, doi) => {
    createAjaxRequest({
      url: '/advancedsearch/add',
      method: 'POST',
      data: JSON.stringify({
        openAlexId,
        doi
      }),
      contentType: 'application/json'
    });
    setStatus('Queued import for this work');
    setTimeout(() => {
      fetchStatuses([openAlexId]);
    }, 2000);
  };

  const bulkAdd = () => {
    if (!selectedWorks.length) {
      return;
    }

    const payload = selectedWorks.map((id) => ({ openAlexId: id }));
    const selectedIds = [...selectedWorks]; // Capture current selection

    createAjaxRequest({
      url: '/advancedsearch/bulkadd',
      method: 'POST',
      data: JSON.stringify(payload),
      contentType: 'application/json'
    });

    setStatus(`Queued ${selectedWorks.length} items for import and search`);
    setSelectedWorks([]);
    // Refresh statuses after a short delay to allow books to be added
    setTimeout(() => {
      fetchStatuses(selectedIds);
    }, 2000);
  };

  const onSaveSearch = () => {
    if (!saveName.trim()) {
      setStatus('Name your saved search first');
      return;
    }

    const resource = {
      name: saveName.trim(),
      searchString: fullSearchString,
      filterString: buildFilterString(),
      sortString: sort,
      cursor: null,
      meshSelections,
      selectedConcepts,
      keywordMeshOperator,
      searchConceptOperator,
      conceptGroupOperators,
      meshGroupOperators
    };

    createAjaxRequest({
      url: '/advancedsearch/saved',
      method: 'POST',
      data: JSON.stringify(resource),
      contentType: 'application/json',
      dataType: 'json'
    }).request.then(() => {
      setSaveName('');
      fetchSaved();
      setStatus('Saved search created');
    });
  };

  const applySavedSearch = (search) => {
    const searchString = search.searchString || '';
    const filterString = search.filterString || '';
    const sortString = search.sortString || '';

    setSearchQuery(searchString);
    setSort(sortString);
    setYearFrom('');
    setYearTo('');
    setArticleType('');
    setIsOaOnly(false);
    setSelectedConcepts([]);

    // Best-effort parse of filter string for UI
    if (filterString) {
      const parts = filterString.split(',');
      parts.forEach((p) => {
        if (p.startsWith('publication_year:')) {
          const [, range] = p.split(':');
          const [from, to] = range.split('-');
          setYearFrom(from || '');
          setYearTo(to || '');
        } else if (p.startsWith('type:')) {
          setArticleType(p.replace('type:', ''));
        } else if (p === 'is_oa:true') {
          setIsOaOnly(true);
        } else if (p.startsWith('concepts.id:')) {
          const ids = p.replace('concepts.id:', '').split('|');
          setSelectedConcepts(ids.map((id) => ({
            id: id.startsWith('https://openalex.org/') ? id : `https://openalex.org/${id}`,
            name: id
          })));
        }
      });
    }

    setMeshSelections((search.meshSelections || []).map((m) => ({ ...m, groupId: m.groupId || 1 })));
    setSelectedConcepts((search.selectedConcepts || []).map((c) => ({ ...c, groupId: c.groupId || 1 })));
    setKeywordMeshOperator(search.keywordMeshOperator || 'AND');
    setSearchConceptOperator(search.searchConceptOperator || 'AND');
    setConceptGroupOperators(search.conceptGroupOperators || search.conceptGroupOperator ? { 1: search.conceptGroupOperator || 'OR' } : { 1: 'OR', 2: 'OR', 3: 'OR' });
    setMeshGroupOperators(search.meshGroupOperators || search.meshGroupOperator ? { 1: search.meshGroupOperator || 'OR' } : { 1: 'OR', 2: 'OR', 3: 'OR' });
    setCursorHistory(['*']);
    fetchWorks('*', ['*'], { searchString, filterString, sortString });
  };

  const removeConcept = (id) => {
    setSelectedConcepts((prev) => prev.filter((c) => c.id !== id));
  };

  const removeMesh = (descriptorUi) => {
    setMeshSelections((prev) => prev.filter((m) => m.descriptorUi !== descriptorUi));
  };

  const toggleConceptRequirement = (id) => {
    setSelectedConcepts((prev) => prev.map((c) => (c.id === id ? { ...c, required: !(c.required !== false) } : c)));
  };

  const toggleMeshRequirement = (descriptorUi) => {
    setMeshSelections((prev) => prev.map((m) => (m.descriptorUi === descriptorUi ? { ...m, required: !(m.required !== false) } : m)));
  };

  const setConceptGroup = (id, groupId) => {
    setSelectedConcepts((prev) => prev.map((c) => (c.id === id ? { ...c, groupId } : c)));
  };

  const setMeshGroup = (descriptorUi, groupId) => {
    setMeshSelections((prev) => prev.map((m) => (m.descriptorUi === descriptorUi ? { ...m, groupId } : m)));
  };

  const setConceptGroupOperator = (groupId, value) => {
    setConceptGroupOperators((prev) => ({ ...prev, [groupId]: value }));
  };

  const setMeshGroupOperator = (groupId, value) => {
    setMeshGroupOperators((prev) => ({ ...prev, [groupId]: value }));
  };

  const renderedStatus = status || 'Ready';
  const streamProgress = streaming ? Math.min(100, Math.round((streamPagesFetched / streamMaxPages) * 100)) : null;
  const queryHint = 'Examples:\n' +
    '- Keywords only: critical care AND sepsis\n' +
    '- Keywords + MeSH (AND): critical care AND ("heart failure" OR "cardiac failure")\n' +
    '- Topics + keywords (OR): heart failure OR device therapy\n' +
    'AND behavior: keywords, MeSH, and concepts must all match.\n' +
    'OR behavior: keywords and concepts are searched separately and merged; MeSH still expands keywords.';

  const streamSearch = useCallback((cursorValue = '*', remainingPages = streamMaxPages) => {
    console.log('[AdvancedSearch] streamSearch called:', { cursorValue, remainingPages, stopped: stopStreamingRef.current });
    // Check stop condition using ref (avoids stale closure)
    if (stopStreamingRef.current || remainingPages <= 0) {
      setStreaming(false);
      setStatus(`Streaming stopped. Fetched ${fetchedCountRef.current} results.`);
      setLoading(false);
      return;
    }

    const currentPage = streamPagesFetchedRef.current + 1;
    setStatus(`Streaming… page ${currentPage}`);

    requestWorksPage(cursorValue).then((data) => {
      console.log('[AdvancedSearch] Page data received:', { resultsCount: data?.results?.length, nextCursor: data?.nextCursor });
      // Check if stopped while request was in flight
      if (stopStreamingRef.current) {
        setStreaming(false);
        setStatus(`Streaming stopped. Fetched ${fetchedCountRef.current} results.`);
        setLoading(false);
        return;
      }

      const pageResults = data?.results || [];
      const next = data?.nextCursor;

      // Update refs immediately
      fetchedCountRef.current += pageResults.length;
      streamPagesFetchedRef.current += 1;

      // Batch state updates
      setResults((prev) => mergeResults(prev, pageResults));
      setFetchedCount(fetchedCountRef.current);
      setStreamPagesFetched(streamPagesFetchedRef.current);
      setStreamCursor(next || null);
      
      // Fetch statuses for new results (outside of state update)
      const newIds = pageResults.map((r) => r.openAlexId).filter(Boolean);
      if (newIds.length > 0) {
        fetchStatuses(newIds);
      }

      // Continue if there's more and not stopped
      if (next && !stopStreamingRef.current && remainingPages > 1 && fetchedCountRef.current < maxResults) {
        // Increase timeout to 300ms to reduce server load
        setTimeout(() => streamSearch(next, remainingPages - 1), 300);
      } else {
        setStreaming(false);
        setLoading(false);
        const total = fetchedCountRef.current;
        setStatus(`Done. Fetched ${total} result${total === 1 ? '' : 's'}.`);
      }
    }).fail((error) => {
      console.error('Streaming failed:', error);
      setStreaming(false);
      setLoading(false);
      setStatus('Streaming failed.');
    });
  }, [maxResults, mergeResults, requestWorksPage, streamMaxPages, fetchStatuses]);

  return (
    <PageContent title="Advanced Search">
      <PageContentBody>
        <div className={styles.advancedSearch}>
          <div className={styles.hero}>
            <div>
              <h1>Advanced Search</h1>
              <p>Build OpenAlex-powered queries, layer MeSH concepts, and add papers directly to Researcharr.</p>
              <div className={styles.hintBox}>
                <div className={styles.hintTitle}>Query examples</div>
                <pre className={styles.hintText}>{queryHint}</pre>
              </div>
              <div className={styles.heroStats}>
                <div className={styles.heroStat}>
                  <small>Concept filters</small>
                  <strong>{selectedConcepts.length}</strong>
                </div>
                <div className={styles.heroStat}>
                  <small>MeSH terms</small>
                  <strong>{meshSelections.length}</strong>
                </div>
                <div className={styles.heroStat}>
                  <small>Saved searches</small>
                  <strong>{savedSearches.length}</strong>
                </div>
              </div>
            </div>
            <div>
              <div className={styles.fieldLabel}>Save this build</div>
              <div className={styles.actionsRow}>
                <input
                  className={styles.textInput}
                  placeholder="Name your search"
                  value={saveName}
                  onChange={(e) => setSaveName(e.target.value)}
                />
                <Button onPress={onSaveSearch}>Save</Button>
              </div>
              <small className={styles.helperText}>Store the current query, filters, and logic as a reusable template.</small>
              <div className={styles.actionsRow} style={{ marginTop: 10 }}>
                <Button onPress={runSearch} kind="primary">{streaming ? 'Streaming…' : 'Run & stream'}</Button>
                {streaming && (
                  <Button onPress={() => {
                    setStopStreaming(true);
                    stopStreamingRef.current = true;
                  }} kind="default">
                    Stop
                  </Button>
                )}
              </div>
            </div>
          </div>

          <div className={styles.layout}>
            <div className={styles.panel}>
              <div className={styles.panelTitle}>Query builder</div>
              <div className={styles.panelSubhead}>Start broad with keywords, then layer concepts and MeSH to focus your results.</div>
              <div className={styles.controlGroup}>
                <div className={styles.sectionHeader}>
                  <div className={styles.sectionTitle}>What to search</div>
                  <div className={styles.sectionHint}>Describe the topic, then add structured concepts to narrow or expand.</div>
                </div>
                <div>
                  <div className={styles.fieldLabel}>Keywords</div>
                  <input
                    className={styles.textInput}
                    placeholder="Topic, phrase, DOI, etc."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && runSearch()}
                  />
                  <div className={styles.helperText}>Free-text terms searched across title/abstract; use quotes for exact phrases.</div>
                </div>

                <div>
                  <div className={styles.fieldLabel}>Concepts (OpenAlex)</div>
                  <input
                    className={styles.textInput}
                    placeholder="Search concepts (e.g., critical care)"
                    value={conceptQuery}
                    onChange={(e) => setConceptQuery(e.target.value)}
                  />
                  <div className={styles.helperText}>Add up to three groups. Use AND for must-have concepts and OR for optional ones.</div>
                  {conceptSuggestions.length > 0 && (
                    <div className={styles.suggestions}>
                      {conceptSuggestions.map((c) => (
                        <div
                          key={c.id}
                          className={styles.suggestionRow}
                          onClick={() => {
                            if (!selectedConcepts.find((s) => s.id === c.id)) {
                              setSelectedConcepts(selectedConcepts.concat([c]));
                            }
                            setConceptSuggestions([]);
                            setConceptQuery('');
                          }}
                        >
                          {c.name}
                        </div>
                      ))}
                    </div>
                  )}
                {selectedConcepts.length > 0 && (
                  <div className={styles.chips}>
                    {selectedConcepts.map((c) => (
                      <div key={c.id} className={styles.chip}>
                        <div className={styles.chipLabel}>{c.name}</div>
                          <div className={styles.chipActions}>
                            <select
                              className={styles.groupSelect}
                              value={c.groupId || 1}
                              onChange={(e) => setConceptGroup(c.id, Number(e.target.value))}
                              title="Assign group for grouping logic"
                            >
                              <option value={1}>Group 1</option>
                              <option value={2}>Group 2</option>
                              <option value={3}>Group 3</option>
                            </select>
                            <button
                              type="button"
                              className={classNames(styles.toggleChip, {
                                [styles.toggleChipActive]: c.required !== false
                              })}
                              onClick={() => toggleConceptRequirement(c.id)}
                              title="Toggle required/optional"
                            >
                              {c.required === false ? 'OR' : 'AND'}
                            </button>
                            <button type="button" onClick={() => removeConcept(c.id)}>✕</button>
                          </div>
                        </div>
                      ))}
                    </div>
                )}
                {selectedConcepts.length > 1 && (
                  <div className={styles.smallControlRow}>
                    <div className={styles.fieldLabel}>Concept group operators (with previous)</div>
                    <div className={styles.helperText}>Groups combine left to right. Choose whether each group must also match (AND) or can broaden results (OR).</div>
                    {[1, 2, 3].map((gid) => (
                      selectedConcepts.find((c) => (c.groupId || 1) === gid) ? (
                        <div key={`concept-op-${gid}`} className={styles.groupOperatorRow}>
                          <span>Group {gid}</span>
                          <select
                            className={styles.selectInput}
                            value={conceptGroupOperators[gid] || 'OR'}
                            onChange={(e) => setConceptGroupOperator(gid, e.target.value)}
                            title="Operator used to combine this group with earlier groups"
                          >
                            <option value="AND">AND</option>
                            <option value="OR">OR</option>
                          </select>
                        </div>
                      ) : null
                    ))}
                  </div>
                )}
              </div>

              <div>
                <div className={styles.fieldLabel}>MeSH terms</div>
                <input
                    className={styles.textInput}
                    placeholder="Search MeSH (heart failure, COPD...)"
                    value={meshQuery}
                    onChange={(e) => setMeshQuery(e.target.value)}
                  />
                  <div className={styles.helperText}>Use AND for required terms; OR keeps them optional. MeSH expands to synonyms automatically.</div>
                  {meshSuggestions.length > 0 && (
                    <div className={styles.suggestions}>
                      {meshSuggestions.map((m) => (
                        <div
                          key={m.descriptorUi}
                          className={styles.suggestionRow}
                          onClick={() => {
                            if (!meshSelections.find((s) => s.descriptorUi === m.descriptorUi)) {
                              setMeshSelections(meshSelections.concat([{
                                descriptorUi: m.descriptorUi,
                                preferredTerm: m.preferredTerm,
                                synonyms: m.synonyms || []
                              }]));
                            }
                            setMeshSuggestions([]);
                            setMeshQuery('');
                          }}
                        >
                          <div>{m.preferredTerm}</div>
                          <small style={{ color: '#8fa4c2' }}>{m.synonyms?.slice(0, 3).join(', ')}</small>
                        </div>
                      ))}
                    </div>
                  )}
                  {meshSelections.length > 0 && (
                    <div className={styles.chips}>
                      {meshSelections.map((m) => (
                        <div key={m.descriptorUi} className={styles.chip}>
                          <div className={styles.chipLabel}>{m.preferredTerm}</div>
                          <div className={styles.chipActions}>
                            <select
                              className={styles.groupSelect}
                              value={m.groupId || 1}
                              onChange={(e) => setMeshGroup(m.descriptorUi, Number(e.target.value))}
                              title="Assign group for grouping logic"
                            >
                              <option value={1}>Group 1</option>
                              <option value={2}>Group 2</option>
                              <option value={3}>Group 3</option>
                            </select>
                            <button
                              type="button"
                              className={classNames(styles.toggleChip, {
                                [styles.toggleChipActive]: m.required !== false
                              })}
                              onClick={() => toggleMeshRequirement(m.descriptorUi)}
                              title="Toggle required/optional"
                            >
                              {m.required === false ? 'OR' : 'AND'}
                            </button>
                            <button type="button" onClick={() => removeMesh(m.descriptorUi)}>✕</button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                  {meshSelections.length > 1 && (
                    <div className={styles.smallControlRow}>
                      <div className={styles.fieldLabel}>MeSH group operators (with previous)</div>
                      <div className={styles.helperText}>Same idea as concepts: groups combine sequentially using AND/OR.</div>
                      {[1, 2, 3].map((gid) => (
                        meshSelections.find((m) => (m.groupId || 1) === gid) ? (
                          <div key={`mesh-op-${gid}`} className={styles.groupOperatorRow}>
                            <span>Group {gid}</span>
                            <select
                              className={styles.selectInput}
                              value={meshGroupOperators[gid] || 'OR'}
                              onChange={(e) => setMeshGroupOperator(gid, e.target.value)}
                              title="Operator used to combine this group with earlier groups"
                            >
                              <option value="AND">AND</option>
                              <option value="OR">OR</option>
                            </select>
                          </div>
                        ) : null
                      ))}
                    </div>
                  )}
              </div>

              <div className={styles.sectionHeader}>
                <div className={styles.sectionTitle}>Combine pieces</div>
                <div className={styles.sectionHint}>Controls the boolean logic between keyword/MeSH and concept filters.</div>
              </div>
              <ul className={styles.helperList}>
                <li>Keywords/MeSH logic: AND = all text terms required; OR = any text term allowed.</li>
                <li>Search + Concepts: AND = must match both text terms and at least one concept group; OR = either is enough.</li>
              </ul>
              <div className={styles.filtersGrid}>
                  <div>
                    <div className={styles.fieldLabel}>Keywords/MeSH Logic</div>
                    <select
                      className={styles.selectInput}
                      value={keywordMeshOperator}
                      onChange={(e) => setKeywordMeshOperator(e.target.value)}
                      title="How to combine keywords with MeSH terms"
                    >
                      <option value="AND">AND (all must match)</option>
                      <option value="OR">OR (any can match)</option>
                    </select>
                  </div>
                  <div>
                    <div className={styles.fieldLabel}>Search/Concepts Logic</div>
                    <select
                      className={styles.selectInput}
                      value={searchConceptOperator}
                      onChange={(e) => setSearchConceptOperator(e.target.value)}
                      title="How to combine keywords+MeSH with concepts"
                    >
                      <option value="AND">AND (all must match)</option>
                      <option value="OR">OR (any can match)</option>
                    </select>
                  </div>
                </div>

              <div className={styles.sectionHeader}>
                <div className={styles.sectionTitle}>Filter results</div>
                <div className={styles.sectionHint}>Limit by year, article type, and sort order.</div>
              </div>
              <div className={styles.filtersGrid}>
                  <div>
                    <div className={styles.fieldLabel}>Year from</div>
                    <input
                      className={styles.textInput}
                      type="number"
                      placeholder="e.g. 2018"
                      value={yearFrom}
                      onChange={(e) => setYearFrom(e.target.value)}
                    />
                  </div>
                  <div>
                    <div className={styles.fieldLabel}>Year to</div>
                    <input
                      className={styles.textInput}
                      type="number"
                      placeholder="e.g. 2025"
                      value={yearTo}
                      onChange={(e) => setYearTo(e.target.value)}
                    />
                  </div>
              </div>

              <div className={styles.filtersGrid}>
                  <div>
                    <div className={styles.fieldLabel}>Type</div>
                    <select
                      className={styles.selectInput}
                      value={articleType}
                      onChange={(e) => setArticleType(e.target.value)}
                    >
                      {typeOptions.map((opt) => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <div className={styles.fieldLabel}>Sort by</div>
                    <select
                      className={styles.selectInput}
                      value={sort}
                      onChange={(e) => setSort(e.target.value)}
                    >
                      {sortOptions.map((opt) => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  </div>
              </div>

              <div className={styles.sectionHeader}>
                <div className={styles.sectionTitle}>Paging & streaming</div>
                <div className={styles.sectionHint}>Control how many items are pulled at once and when to stop.</div>
              </div>
              <div className={styles.filtersGrid}>
                  <div>
                    <div className={styles.fieldLabel}>Per page</div>
                    <select
                      className={styles.selectInput}
                      value={perPage}
                      onChange={(e) => setPerPage(Number(e.target.value))}
                    >
                      {perPageOptions.map((opt) => (
                        <option key={opt} value={opt}>{opt}</option>
                      ))}
                    </select>
                    <div className={styles.helperText}>How many items to load per page and per streaming request.</div>
                  </div>
                  <div>
                    <div className={styles.fieldLabel}>Result limit</div>
                    <input
                      className={styles.textInput}
                      type="number"
                      min="1"
                      max="10000"
                      value={maxResults}
                      onChange={(e) => setMaxResults(Number(e.target.value) || 0)}
                    />
                    <small className={styles.helperText}>Stop streaming once this many total results are fetched.</small>
                  </div>
                  <div>
                    <div className={styles.fieldLabel}>Open access</div>
                    <div className={styles.toggles}>
                      <div
                        className={classNames(styles.toggleChip, {
                          [styles.toggleChipActive]: !isOaOnly
                        })}
                        onClick={() => setIsOaOnly(false)}
                      >
                        Any
                      </div>
                      <div
                        className={classNames(styles.toggleChip, {
                          [styles.toggleChipActive]: isOaOnly
                        })}
                        onClick={() => setIsOaOnly(true)}
                      >
                        OA only
                      </div>
                    </div>
                    <div className={styles.helperText}>Choose whether to only return papers with an open access copy.</div>
                  </div>
                </div>
              </div>
            </div>

            <div className={styles.panel}>
              <div className={styles.resultsHeader}>
                <div className={styles.panelTitle}>Results</div>
                <div className={styles.status}>{renderedStatus}</div>
              </div>

              {results.length > 0 && (
                <div className={styles.actionsRow} style={{ marginBottom: 8 }}>
                  <Button
                    kind="default"
                    onPress={() => setSelectedWorks(results.map((r) => r.openAlexId))}
                  >
                    Select all on page
                  </Button>
                  <Button
                    kind="default"
                    onPress={() => setSelectedWorks([])}
                    disabled={selectedWorks.length === 0}
                  >
                    Clear selection
                  </Button>
                  <Button
                    kind="primary"
                    onPress={bulkAdd}
                    disabled={!selectedWorks.length}
                  >
                    Add selected ({selectedWorks.length})
                  </Button>
                </div>
              )}

              {loading && <LoadingIndicator />}
              {streaming && (
                <div className={styles.progressBarContainer}>
                  <div className={styles.progressMeta}>
                    <span>Streaming pages…</span>
                    <span>{fetchedCount} items</span>
                  </div>
                  <div className={styles.progressOuter}>
                    <div className={styles.progressInner} style={{ width: `${streamProgress || 0}%` }} />
                  </div>
                </div>
              )}

              <div className={styles.resultList}>
                {results.map((r) => {
                  const isSelected = selectedWorks.includes(r.openAlexId);
                  const bookStatus = bookStatuses[r.openAlexId];
                  return (
                    <div key={r.openAlexId} className={styles.resultCard}>
                      <div>
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => toggleSelection(r.openAlexId)}
                        />
                      </div>
                      <div style={{ flex: 1 }}>
                        <h3 className={styles.resultTitle}>{r.title}</h3>
                        <div className={styles.authors}>{r.authors?.slice(0, 6).join(', ')}</div>
                        <div className={styles.resultMeta}>
                          {r.year && <span className={styles.badgeMuted}>{r.year}</span>}
                          {r.journal && <span className={styles.badgeMuted}>{r.journal}</span>}
                          {r.doi && <a className={styles.badgeMuted} href={r.doi} target="_blank" rel="noreferrer">DOI</a>}
                          {r.isOpenAccess && <span className={styles.pill}>Open Access</span>}
                          {r.citedByCount != null && <span className={styles.badgeMuted}>Cited {r.citedByCount}</span>}
                        </div>
                        {bookStatus && (
                          <div className={styles.statusRow} style={{ marginTop: 8 }}>
                            {bookStatus.isMonitored && (
                              <span className={styles.statusBadge} style={{ background: '#2cb1a9', color: '#fff' }}>
                                Monitored
                              </span>
                            )}
                            {bookStatus.hasFile && (
                              <span className={styles.statusBadge} style={{ background: '#0b8750', color: '#fff' }}>
                                Downloaded
                              </span>
                            )}
                            {bookStatus.lastHistoryEventType && (
                              <span className={styles.statusBadge} style={{ background: '#1a2538', color: '#9bb2d0' }}>
                                {bookStatus.lastHistoryEventType.replace('Book', '').replace('File', '')}
                              </span>
                            )}
                            {!bookStatus.isMonitored && !bookStatus.hasFile && !bookStatus.lastHistoryEventType && (
                              <span className={styles.statusBadge} style={{ background: '#1a2538', color: '#9bb2d0' }}>
                                Not added
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                      <div>
                        <Button onPress={() => addWork(r.openAlexId, r.doi)} kind="default">Add</Button>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className={styles.pagination}>
                <div className={styles.status}>
                  {cursorHistory.length > 1 ? `Page ${cursorHistory.length}` : 'Page 1'}
                </div>
                <div className={styles.actionsRow}>
                  <Button onPress={goPrev} disabled={cursorHistory.length <= 1}>Previous</Button>
                  <Button onPress={goNext} disabled={!nextCursor}>Next</Button>
                </div>
              </div>
            </div>
          </div>

          <div className={styles.panel}>
            <div className={styles.panelTitle}>Saved searches</div>
            {savedSearches.length === 0 && <div className={styles.status}>No saved searches yet.</div>}
            <div className={styles.savedList}>
              {savedSearches.map((s) => (
                <div
                  key={s.id}
                  className={styles.savedRow}
                  onClick={() => applySavedSearch(s)}
                >
                  <div className={styles.savedRowTitle}>{s.name}</div>
                  <small>{s.searchString || 'No search string'}</small>
                  {s.filterString && (
                    <div className={styles.chips} style={{ marginTop: 6 }}>
                      <span className={styles.badgeMuted}>{s.filterString}</span>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default AdvancedSearch;
