import _ from 'lodash';
import { batchActions } from 'redux-batched-actions';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import findSelectedFilters from 'Utilities/Filter/findSelectedFilters';
import getSectionState from 'Utilities/State/getSectionState';
import { set, updateServerSideCollection } from '../baseActions';

function createFetchServerSideCollectionHandler(section, url, fetchDataAugmenter) {
  return function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));

    const sectionState = getSectionState(getState(), section, true);
    const page = payload.page || sectionState.page || 1;

    const data = Object.assign({ page },
      _.pick(sectionState, [
        'pageSize',
        'sortDirection',
        'sortKey'
      ]));

    if (fetchDataAugmenter) {
      fetchDataAugmenter(getState, payload, data);
    }

    const {
      selectedFilterKey,
      filters,
      customFilters
    } = sectionState;

    const selectedFilters = findSelectedFilters(selectedFilterKey, filters, customFilters);

    selectedFilters.forEach((filter) => {
      data[filter.key] = filter.value;
    });

    const promise = createAjaxRequest({
      url,
      data
    }).request;

    promise.done((response) => {
      if (!response || !Array.isArray(response.records)) {
        dispatch(set({
          section,
          isFetching: false,
          isPopulated: false,
          error: { message: 'Invalid response from server' }
        }));
        return;
      }

      // Ensure required properties exist with defaults
      const safeResponse = {
        records: response.records || [],
        totalRecords: response.totalRecords || 0,
        pageSize: response.pageSize || sectionState.pageSize || 25,
        page: response.page || page,
        sortKey: response.sortKey || sectionState.sortKey,
        sortDirection: response.sortDirection || sectionState.sortDirection,
        ..._.omit(response, ['records', 'totalRecords', 'pageSize', 'page', 'sortKey', 'sortDirection'])
      };

      dispatch(batchActions([
        updateServerSideCollection({ section, data: safeResponse }),

        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr
      }));
    });
  };
}

export default createFetchServerSideCollectionHandler;
