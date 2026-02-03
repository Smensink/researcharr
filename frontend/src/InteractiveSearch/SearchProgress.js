import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { icons } from 'Helpers/Props';
import styles from './SearchProgress.css';

function getStatusIcon(state) {
  switch (state) {
    case 'searching':
      return icons.SPINNER;
    case 'completed':
      return icons.CHECK;
    case 'failed':
      return icons.DANGER;
    default:
      return icons.SPINNER;
  }
}

function getStatusClass(state) {
  switch (state) {
    case 'completed':
      return styles.completed;
    case 'failed':
      return styles.failed;
    default:
      return styles.searching;
  }
}

function SearchProgress(props) {
  const {
    isStreamingSearch,
    searchProgress
  } = props;

  if (!isStreamingSearch && !searchProgress) {
    return null;
  }

  const {
    totalIndexers = 0,
    completedIndexers = 0,
    indexerStatuses = {}
  } = searchProgress || {};

  const indexerStatusList = Object.values(indexerStatuses);
  const isComplete = completedIndexers >= totalIndexers && totalIndexers > 0;
  const progressPercent = totalIndexers > 0 ? Math.round((completedIndexers / totalIndexers) * 100) : 0;

  if (isComplete && !isStreamingSearch) {
    return null;
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        {isStreamingSearch ? (
          <LoadingIndicator size={20} />
        ) : null}
        <span className={styles.title}>
          {isStreamingSearch ? 'Searching...' : 'Search Complete'}
        </span>
        <span className={styles.progress}>
          {completedIndexers} / {totalIndexers} indexers
          {progressPercent > 0 ? ` (${progressPercent}%)` : ''}
        </span>
      </div>

      {indexerStatusList.length > 0 && (
        <div className={styles.indexerList}>
          {indexerStatusList.map((status) => (
            <div
              key={status.indexerId}
              className={`${styles.indexerStatus} ${getStatusClass(status.state)}`}
            >
              <Icon
                name={getStatusIcon(status.state)}
                isSpinning={status.state === 'searching'}
              />
              <span className={styles.indexerName}>
                {status.indexerName}
              </span>
              {status.resultCount > 0 && (
                <span className={styles.resultCount}>
                  ({status.resultCount} results)
                </span>
              )}
              {status.errorMessage && (
                <span className={styles.errorMessage} title={status.errorMessage}>
                  Error
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

SearchProgress.propTypes = {
  isStreamingSearch: PropTypes.bool,
  searchProgress: PropTypes.shape({
    totalIndexers: PropTypes.number,
    completedIndexers: PropTypes.number,
    indexerStatuses: PropTypes.object
  })
};

export default SearchProgress;
