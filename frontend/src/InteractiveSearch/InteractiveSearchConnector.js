import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as releaseActions from 'Store/Actions/releaseActions';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import InteractiveSearch from './InteractiveSearch';

function createMapStateToProps(appState, { type }) {
  return createSelector(
    (state) => state.releases.items.length,
    (state) => state.releases.isStreamingSearch,
    (state) => state.releases.searchProgress,
    createClientSideCollectionSelector('releases', `releases.${type}`),
    createUISettingsSelector(),
    (totalReleasesCount, isStreamingSearch, searchProgress, releases, uiSettings) => {
      return {
        totalReleasesCount,
        isStreamingSearch,
        searchProgress,
        longDateFormat: uiSettings.longDateFormat,
        timeFormat: uiSettings.timeFormat,
        ...releases
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    dispatchFetchReleases(payload) {
      dispatch(releaseActions.fetchReleases(payload));
    },

    dispatchStartStreamingSearch(payload) {
      dispatch(releaseActions.startStreamingSearch(payload));
    },

    onSortPress(sortKey, sortDirection) {
      dispatch(releaseActions.setReleasesSort({ sortKey, sortDirection }));
    },

    onFilterSelect(selectedFilterKey) {
      const action = props.type === 'book' ?
        releaseActions.setBookReleasesFilter :
        releaseActions.setAuthorReleasesFilter;

      dispatch(action({ selectedFilterKey }));
    },

    onGrabPress(payload) {
      dispatch(releaseActions.grabRelease(payload));
    }
  };
}

class InteractiveSearchConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      searchPayload,
      isPopulated,
      dispatchStartStreamingSearch
    } = this.props;

    // If search results are not yet isPopulated, start streaming search
    // otherwise re-show the existing props.

    if (!isPopulated) {
      dispatchStartStreamingSearch(searchPayload);
    }
  }

  //
  // Render

  render() {
    const {
      dispatchFetchReleases,
      dispatchStartStreamingSearch,
      ...otherProps
    } = this.props;

    return (

      <InteractiveSearch
        {...otherProps}
      />
    );
  }
}

InteractiveSearchConnector.propTypes = {
  searchPayload: PropTypes.object.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  isStreamingSearch: PropTypes.bool,
  searchProgress: PropTypes.object,
  dispatchFetchReleases: PropTypes.func.isRequired,
  dispatchStartStreamingSearch: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, createMapDispatchToProps)(InteractiveSearchConnector);
