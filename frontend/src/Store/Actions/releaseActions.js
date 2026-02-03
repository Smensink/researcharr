import { createAction } from 'redux-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionFilterReducer from './Creators/Reducers/createSetClientSideCollectionFilterReducer';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';

//
// Variables

export const section = 'releases';
export const bookSection = 'releases.book';
export const authorSection = 'releases.author';

let abortCurrentRequest = null;

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  items: [],
  sortKey: 'releaseWeight',
  sortDirection: sortDirections.ASCENDING,

  // Streaming search state
  isStreamingSearch: false,
  searchId: null,
  searchProgress: {
    totalIndexers: 0,
    completedIndexers: 0,
    indexerStatuses: {}
  },
  sortPredicates: {
    age: function(item, direction) {
      return item.ageMinutes;
    },
    peers: function(item, direction) {
      const seeders = item.seeders || 0;
      const leechers = item.leechers || 0;

      return seeders * 1000000 + leechers;
    },
    rejections: function(item, direction) {
      const rejections = item.rejections;
      const releaseWeight = item.releaseWeight;

      if (rejections.length !== 0) {
        return releaseWeight + 1000000;
      }

      return releaseWeight;
    }
  },

  filters: [
    {
      key: 'all',
      label: 'All',
      filters: []
    },
    {
      key: 'discography-pack',
      label: 'Discography',
      filters: [
        {
          key: 'discography',
          value: true,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'not-discography-pack',
      label: 'Not Discography',
      filters: [
        {
          key: 'discography',
          value: false,
          type: filterTypes.EQUAL
        }
      ]
    }
  ],

  filterPredicates: {
    quality: function(item, value, type) {
      const qualityId = item.quality.quality.id;

      if (type === filterTypes.EQUAL) {
        return qualityId === value;
      }

      if (type === filterTypes.NOT_EQUAL) {
        return qualityId !== value;
      }

      // Default to false
      return false;
    },

    rejectionCount: function(item, value, type) {
      const rejectionCount = item.rejections.length;

      switch (type) {
        case filterTypes.EQUAL:
          return rejectionCount === value;

        case filterTypes.GREATER_THAN:
          return rejectionCount > value;

        case filterTypes.GREATER_THAN_OR_EQUAL:
          return rejectionCount >= value;

        case filterTypes.LESS_THAN:
          return rejectionCount < value;

        case filterTypes.LESS_THAN_OR_EQUAL:
          return rejectionCount <= value;

        case filterTypes.NOT_EQUAL:
          return rejectionCount !== value;

        default:
          return false;
      }
    },

    peers: function(item, value, type) {
      const seeders = item.seeders || 0;
      const leechers = item.leechers || 0;
      const peers = seeders + leechers;

      switch (type) {
        case filterTypes.EQUAL:
          return peers === value;

        case filterTypes.GREATER_THAN:
          return peers > value;

        case filterTypes.GREATER_THAN_OR_EQUAL:
          return peers >= value;

        case filterTypes.LESS_THAN:
          return peers < value;

        case filterTypes.LESS_THAN_OR_EQUAL:
          return peers <= value;

        case filterTypes.NOT_EQUAL:
          return peers !== value;

        default:
          return false;
      }
    }
  },

  filterBuilderProps: [
    {
      name: 'title',
      label: 'Title',
      type: filterBuilderTypes.STRING
    },
    {
      name: 'age',
      label: 'Age',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'protocol',
      label: 'Protocol',
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.PROTOCOL
    },
    {
      name: 'indexerId',
      label: 'Indexer',
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.INDEXER
    },
    {
      name: 'size',
      label: 'Size',
      type: filterBuilderTypes.NUMBER,
      valueType: filterBuilderValueTypes.BYTES
    },
    {
      name: 'seeders',
      label: 'Seeders',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'leechers',
      label: 'Peers',
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'quality',
      label: 'Quality',
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.QUALITY
    },
    {
      name: 'customFormatScore',
      label: () => translate('CustomFormatScore'),
      type: filterBuilderTypes.NUMBER
    },
    {
      name: 'rejectionCount',
      label: 'Rejection Count',
      type: filterBuilderTypes.NUMBER
    }
  ],

  book: {
    selectedFilterKey: 'all'
  },

  author: {
    selectedFilterKey: 'all'
  }
};

export const persistState = [
  'releases.selectedFilterKey',
  'releases.book.customFilters',
  'releases.author.customFilters'
];

//
// Actions Types

export const FETCH_RELEASES = 'releases/fetchReleases';
export const CANCEL_FETCH_RELEASES = 'releases/cancelFetchReleases';
export const SET_RELEASES_SORT = 'releases/setReleasesSort';
export const CLEAR_RELEASES = 'releases/clearReleases';
export const GRAB_RELEASE = 'releases/grabRelease';
export const UPDATE_RELEASE = 'releases/updateRelease';
export const SET_BOOK_RELEASES_FILTER = 'releases/setBookReleasesFilter';
export const SET_AUTHOR_RELEASES_FILTER = 'releases/setAuthorReleasesFilter';

// Streaming search actions
export const START_STREAMING_SEARCH = 'releases/startStreamingSearch';
export const ADD_STREAMING_RESULTS = 'releases/addStreamingResults';
export const UPDATE_SEARCH_PROGRESS = 'releases/updateSearchProgress';
export const STREAMING_SEARCH_COMPLETE = 'releases/streamingSearchComplete';

//
// Action Creators

export const fetchReleases = createThunk(FETCH_RELEASES);
export const cancelFetchReleases = createThunk(CANCEL_FETCH_RELEASES);
export const setReleasesSort = createAction(SET_RELEASES_SORT);
export const clearReleases = createAction(CLEAR_RELEASES);
export const grabRelease = createThunk(GRAB_RELEASE);
export const updateRelease = createAction(UPDATE_RELEASE);
export const setBookReleasesFilter = createAction(SET_BOOK_RELEASES_FILTER);
export const setAuthorReleasesFilter = createAction(SET_AUTHOR_RELEASES_FILTER);

// Streaming search action creators
export const startStreamingSearch = createThunk(START_STREAMING_SEARCH);
export const addStreamingResults = createAction(ADD_STREAMING_RESULTS);
export const updateSearchProgress = createAction(UPDATE_SEARCH_PROGRESS);
export const streamingSearchComplete = createAction(STREAMING_SEARCH_COMPLETE);

//
// Helpers

const fetchReleasesHelper = createFetchHandler(section, '/release');

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_RELEASES]: function(getState, payload, dispatch) {
    const abortRequest = fetchReleasesHelper(getState, payload, dispatch);

    abortCurrentRequest = abortRequest;
  },

  [CANCEL_FETCH_RELEASES]: function(getState, payload, dispatch) {
    if (abortCurrentRequest) {
      abortCurrentRequest = abortCurrentRequest();
    }
  },

  [GRAB_RELEASE]: function(getState, payload, dispatch) {
    const guid = payload.guid;

    dispatch(updateRelease({ guid, isGrabbing: true }));

    const promise = createAjaxRequest({
      url: '/release',
      method: 'POST',
      contentType: 'application/json',
      dataType: 'json',
      data: JSON.stringify(payload)
    }).request;

    promise.done((data) => {
      dispatch(updateRelease({
        guid,
        isGrabbing: false,
        isGrabbed: true,
        grabError: null
      }));
    });

    promise.fail((xhr) => {
      const grabError = xhr.responseJSON && xhr.responseJSON.message || 'Failed to add to download queue';

      dispatch(updateRelease({
        guid,
        isGrabbing: false,
        isGrabbed: false,
        grabError
      }));
    });
  },

  [START_STREAMING_SEARCH]: function(getState, payload, dispatch) {
    // Clear previous results and start streaming search
    dispatch({
      type: CLEAR_RELEASES
    });

    dispatch({
      type: 'releases/set',
      payload: {
        section,
        isFetching: true,
        isStreamingSearch: true,
        searchId: null,
        items: [],
        searchProgress: {
          totalIndexers: 0,
          completedIndexers: 0,
          indexerStatuses: {}
        }
      }
    });

    const promise = createAjaxRequest({
      url: '/release/search',
      method: 'POST',
      contentType: 'application/json',
      dataType: 'json',
      data: JSON.stringify(payload)
    }).request;

    promise.done((data) => {
      dispatch({
        type: 'releases/set',
        payload: {
          section,
          searchId: data.searchId,
          isFetching: false,
          isPopulated: true,
          isStreamingSearch: false,
          searchProgress: {
            totalIndexers: data.totalIndexers,
            completedIndexers: data.completedIndexers,
            indexerStatuses: {}
          }
        }
      });
    });

    promise.fail((xhr) => {
      dispatch({
        type: 'releases/set',
        payload: {
          section,
          isFetching: false,
          isStreamingSearch: false,
          error: xhr
        }
      });
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_RELEASES]: (state) => {
    const {
      book,
      author,
      ...otherDefaultState
    } = defaultState;

    return Object.assign({}, state, otherDefaultState);
  },

  [UPDATE_RELEASE]: (state, { payload }) => {
    const guid = payload.guid;
    const newState = Object.assign({}, state);
    const items = newState.items;

    // Return early if there aren't any items (the user closed the modal)
    if (!items.length) {
      return;
    }

    const index = items.findIndex((item) => item.guid === guid);
    const item = Object.assign({}, items[index], payload);

    newState.items = [...items];
    newState.items.splice(index, 1, item);

    return newState;
  },

  [SET_RELEASES_SORT]: createSetClientSideCollectionSortReducer(section),
  [SET_BOOK_RELEASES_FILTER]: createSetClientSideCollectionFilterReducer(bookSection),
  [SET_AUTHOR_RELEASES_FILTER]: createSetClientSideCollectionFilterReducer(authorSection),

  [ADD_STREAMING_RESULTS]: (state, { payload }) => {
    const { results, indexerId, indexerName, totalIndexers, completedIndexers, indexerStatuses } = payload;
    const newState = Object.assign({}, state);
    const existingItems = [...newState.items];

    // Add new results, update existing ones if better (fewer rejections)
    results.forEach((result) => {
      const existingIndex = existingItems.findIndex((item) => item.guid === result.guid);

      if (existingIndex >= 0) {
        // Update if this one has fewer rejections
        const existing = existingItems[existingIndex];
        if (result.rejections.length < existing.rejections.length) {
          existingItems[existingIndex] = result;
        }
      } else {
        existingItems.push(result);
      }
    });

    newState.items = existingItems;
    newState.isPopulated = true;
    newState.searchProgress = {
      totalIndexers: totalIndexers || state.searchProgress.totalIndexers,
      completedIndexers: completedIndexers || state.searchProgress.completedIndexers,
      indexerStatuses: indexerStatuses || state.searchProgress.indexerStatuses
    };

    return newState;
  },

  [UPDATE_SEARCH_PROGRESS]: (state, { payload }) => {
    const { totalIndexers, completedIndexers, indexerStatuses } = payload;

    return {
      ...state,
      searchProgress: {
        totalIndexers: totalIndexers !== undefined ? totalIndexers : state.searchProgress.totalIndexers,
        completedIndexers: completedIndexers !== undefined ? completedIndexers : state.searchProgress.completedIndexers,
        indexerStatuses: indexerStatuses || state.searchProgress.indexerStatuses
      }
    };
  },

  [STREAMING_SEARCH_COMPLETE]: (state, { payload }) => {
    const { totalIndexers, completedIndexers, indexerStatuses } = payload;

    return {
      ...state,
      isFetching: false,
      isStreamingSearch: false,
      searchProgress: {
        totalIndexers: totalIndexers || state.searchProgress.totalIndexers,
        completedIndexers: completedIndexers || state.searchProgress.completedIndexers,
        indexerStatuses: indexerStatuses || state.searchProgress.indexerStatuses
      }
    };
  }

}, defaultState, section);
