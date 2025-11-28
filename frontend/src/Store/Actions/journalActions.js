import { createAction } from 'redux-actions';
import { sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import createFetchHandler from './Creators/createFetchHandler';
import createHandleActions from './Creators/createHandleActions';
import createSetClientSideCollectionSortReducer from './Creators/Reducers/createSetClientSideCollectionSortReducer';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'journals';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  sortKey: 'paperCount',
  sortDirection: sortDirections.DESCENDING,
  items: [],

  columns: [
    {
      name: 'name',
      label: 'Journal Name',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'monitored',
      label: 'Monitored',
      isSortable: true,
      isVisible: true
    },
    {
      name: 'paperCount',
      label: 'Papers',
      isSortable: true,
      isVisible: true
    }
  ]
};

//
// Actions Types

export const FETCH_JOURNALS = 'journals/fetchJournals';
export const SET_JOURNALS_SORT = 'journals/setJournalsSort';
export const SET_JOURNALS_TABLE_OPTION = 'journals/setJournalsTableOption';
export const CLEAR_JOURNALS = 'journals/clearJournals';

//
// Action Creators

export const fetchJournals = createThunk(FETCH_JOURNALS);
export const setJournalsSort = createAction(SET_JOURNALS_SORT);
export const setJournalsTableOption = createAction(SET_JOURNALS_TABLE_OPTION);
export const clearJournals = createAction(CLEAR_JOURNALS);

//
// Action Handlers

export const actionHandlers = handleThunks({
  [FETCH_JOURNALS]: createFetchHandler(section, '/journal')
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_JOURNALS_SORT]: createSetClientSideCollectionSortReducer(section),

  [SET_JOURNALS_TABLE_OPTION]: createSetTableOptionReducer(section),

  [CLEAR_JOURNALS]: (state) => {
    return Object.assign({}, state, {
      isFetching: false,
      isPopulated: false,
      error: null,
      items: []
    });
  }

}, defaultState, section);
