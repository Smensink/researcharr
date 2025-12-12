import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import IndexerErrorsRow from './IndexerErrorsRow';
import styles from './IndexerErrors.css';

// Map operation types to their display labels
// Using inline translations to avoid missing key warnings for enum values
const operationTypeLabels = {
  Unknown: 'Unknown',
  RssSync: 'RSS Sync',
  Search: 'Search',
  Test: 'Test',
  Download: 'Download'
};

// Map error types to their display labels
const errorTypeLabels = {
  Unknown: 'Unknown',
  ConnectionFailure: 'Connection Failure',
  Timeout: 'Timeout',
  HttpError: 'HTTP Error',
  AuthError: 'Auth Error',
  RateLimit: 'Rate Limit',
  CloudflareCaptcha: 'Cloudflare Captcha',
  ParseError: 'Parse Error',
  ReleaseUnavailable: 'Release Unavailable'
};

class IndexerErrors extends Component {

  //
  // Render

  render() {
    const {
      indexerId,
      indexerName,
      statistics,
      failures,
      isFetching,
      isPopulated,
      error,
      shortDateFormat,
      timeFormat,
      onRefreshPress
    } = this.props;

    const columns = [
      {
        name: 'timestamp',
        label: translate('Time'),
        isVisible: true
      },
      {
        name: 'operationType',
        label: translate('Operation'),
        isVisible: true
      },
      {
        name: 'errorType',
        label: translate('ErrorType'),
        isVisible: true
      },
      {
        name: 'errorMessage',
        label: translate('ErrorMessage'),
        isVisible: true
      },
      {
        name: 'httpStatusCode',
        label: translate('HttpStatusCode'),
        isVisible: true
      }
    ];

    return (
      <PageContent title={translate('IndexerErrors')}>
        <PageToolbar>
          <PageToolbarSection>
            <PageToolbarButton
              label={translate('Refresh')}
              iconName={icons.REFRESH}
              isSpinning={isFetching}
              onPress={onRefreshPress}
            />
          </PageToolbarSection>
        </PageToolbar>

        <PageContentBody>
          <div className={styles.header}>
            <h2>{indexerName || translate('Indexer')}</h2>
            {statistics && (
              <div className={styles.statistics}>
                <div className={styles.statItem}>
                  <span className={styles.statLabel}>{translate('TotalFailures')}:</span>
                  <span className={styles.statValue}>{statistics.totalFailures || 0}</span>
                </div>
                <div className={styles.statItem}>
                  <span className={styles.statLabel}>{translate('RecentFailures')}:</span>
                  <span className={styles.statValue}>{statistics.recentFailures || 0}</span>
                </div>
                <div className={styles.statItem}>
                  <span className={styles.statLabel}>{translate('FailureRate')}:</span>
                  <span className={styles.statValue}>{statistics.failureRate ? statistics.failureRate.toFixed(2) : '0.00'}%</span>
                </div>
                <div className={styles.statItem}>
                  <span className={styles.statLabel}>{translate('Status')}:</span>
                  <span className={styles.statValue}>
                    {statistics.isHealthy ? translate('Healthy') : translate('Unhealthy')}
                  </span>
                </div>
              </div>
            )}
          </div>

          {statistics && statistics.operationStatistics && Object.keys(statistics.operationStatistics).length > 0 && (
            <div className={styles.breakdown}>
              <h3>{translate('OperationStatistics')}</h3>
              <div className={styles.breakdownList}>
                {Object.entries(statistics.operationStatistics).map(([operation, opStats]) => {
                  if (opStats.totalOperations === 0) {
                    return null; // Skip operations with no data
                  }
                  return (
                    <div key={operation} className={styles.breakdownItem}>
                      <div className={styles.operationHeader}>
                        <span className={styles.operationName}>{operationTypeLabels[operation] || operation}:</span>
                        <span className={styles.operationRate}>
                          {opStats.failureRate ? opStats.failureRate.toFixed(2) : '0.00'}% {translate('FailureRate')}
                        </span>
                      </div>
                      <div className={styles.operationDetails}>
                        <span className={styles.successCount}>
                          {translate('Successes')}: {opStats.successes || 0}
                        </span>
                        <span className={styles.failureCount}>
                          {translate('Failures')}: {opStats.failures || 0}
                        </span>
                        <span className={styles.totalCount}>
                          {translate('Total')}: {opStats.totalOperations || 0}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {statistics && statistics.failuresByErrorType && Object.keys(statistics.failuresByErrorType).length > 0 && (
            <div className={styles.breakdown}>
              <h3>{translate('FailuresByErrorType')}</h3>
              <div className={styles.breakdownList}>
                {Object.entries(statistics.failuresByErrorType).map(([errorType, count]) => (
                  <div key={errorType} className={styles.breakdownItem}>
                    <span>{errorTypeLabels[errorType] || errorType}:</span>
                    <span>{count}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {
            isFetching && !isPopulated &&
              <LoadingIndicator />
          }

          {
            !isFetching && error &&
              <Alert kind={kinds.DANGER}>
                {error.message || translate('UnableToLoadIndexerFailures')}
              </Alert>
          }

          {
            isPopulated && !error && (!failures || failures.length === 0) &&
              <Alert kind={kinds.INFO}>
                {translate('NoIndexerFailures')}
              </Alert>
          }

          {
            isPopulated && !error && failures && failures.length > 0 &&
              <div>
                <Table columns={columns}>
                  <TableBody>
                    {
                      failures.map((failure) => {
                        return (
                          <IndexerErrorsRow
                            key={failure.id}
                            columns={columns}
                            operationTypeLabels={operationTypeLabels}
                            errorTypeLabels={errorTypeLabels}
                            shortDateFormat={shortDateFormat}
                            timeFormat={timeFormat}
                            {...failure}
                          />
                        );
                      })
                    }
                  </TableBody>
                </Table>
              </div>
          }
        </PageContentBody>
      </PageContent>
    );
  }
}

IndexerErrors.propTypes = {
  indexerId: PropTypes.number.isRequired,
  indexerName: PropTypes.string,
  statistics: PropTypes.object,
  failures: PropTypes.arrayOf(PropTypes.object),
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  shortDateFormat: PropTypes.string.isRequired,
  timeFormat: PropTypes.string.isRequired,
  onRefreshPress: PropTypes.func.isRequired
};

export default IndexerErrors;

