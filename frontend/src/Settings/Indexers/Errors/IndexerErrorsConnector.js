import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchIndexerFailures, fetchIndexerStatistics } from 'Store/Actions/settingsActions';
import { fetchIndexers } from 'Store/Actions/settingsActions';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import IndexerErrors from './IndexerErrors';

function createMapStateToProps() {
  return createSelector(
    (state, { match }) => {
      const indexerId = parseInt(match.params.id, 10);
      return indexerId;
    },
    (state) => state.settings.indexers,
    createUISettingsSelector(),
    (indexerId, indexers, uiSettings) => {
      const indexer = indexers.items.find((i) => i.id === indexerId);
      const failures = indexers.failures && indexers.failures[indexerId] ? indexers.failures[indexerId] : [];
      const statistics = indexers.statistics && indexers.statistics[indexerId] ? indexers.statistics[indexerId] : null;

      return {
        indexerId,
        indexerName: indexer ? indexer.name : null,
        statistics,
        failures,
        isFetching: indexers.isFailuresFetching,
        isPopulated: failures.length > 0 || (indexers.failures && indexers.failures[indexerId] !== undefined),
        error: indexers.failuresError && indexers.failuresError.indexerId === indexerId ? indexers.failuresError : null,
        shortDateFormat: uiSettings.shortDateFormat,
        timeFormat: uiSettings.timeFormat
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchIndexerFailures: fetchIndexerFailures,
  dispatchFetchIndexerStatistics: fetchIndexerStatistics,
  dispatchFetchIndexers: fetchIndexers
};

class IndexerErrorsConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      indexerId,
      dispatchFetchIndexerFailures,
      dispatchFetchIndexerStatistics,
      dispatchFetchIndexers
    } = this.props;

    dispatchFetchIndexers();
    dispatchFetchIndexerStatistics();
    dispatchFetchIndexerFailures({ indexerId });
  }

  //
  // Listeners

  onRefreshPress = () => {
    const {
      indexerId,
      dispatchFetchIndexerFailures
    } = this.props;

    dispatchFetchIndexerFailures({ indexerId });
  };

  //
  // Render

  render() {
    return (
      <IndexerErrors
        {...this.props}
        onRefreshPress={this.onRefreshPress}
      />
    );
  }
}

IndexerErrorsConnector.propTypes = {
  match: PropTypes.object.isRequired,
  indexerId: PropTypes.number.isRequired,
  dispatchFetchIndexerFailures: PropTypes.func.isRequired,
  dispatchFetchIndexerStatistics: PropTypes.func.isRequired,
  dispatchFetchIndexers: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(IndexerErrorsConnector);

