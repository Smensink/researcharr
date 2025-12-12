import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import { kinds } from 'Helpers/Props';
import formatDateTime from 'Utilities/Date/formatDateTime';
import styles from './IndexerErrorsRow.css';

function IndexerErrorsRow(props) {
  const {
    timestamp,
    operationType,
    errorType,
    errorMessage,
    httpStatusCode,
    columns,
    operationTypeLabels,
    errorTypeLabels,
    shortDateFormat,
    timeFormat
  } = props;

  return (
    <TableRow>
      <TableRowCell className={styles.timestamp}>
        {formatDateTime(timestamp, shortDateFormat, timeFormat, { includeSeconds: true })}
      </TableRowCell>

      <TableRowCell className={styles.operationType}>
        {operationTypeLabels[operationType] || operationType}
      </TableRowCell>

      <TableRowCell className={styles.errorType}>
        <Label kind={kinds.DANGER}>
          {errorTypeLabels[errorType] || errorType}
        </Label>
      </TableRowCell>

      <TableRowCell className={styles.errorMessage}>
        {errorMessage || '-'}
      </TableRowCell>

      <TableRowCell className={styles.httpStatusCode}>
        {httpStatusCode ? (
          <Label
            kind={httpStatusCode >= 500 ? kinds.DANGER : httpStatusCode >= 400 ? kinds.WARNING : kinds.INFO}
          >
            {httpStatusCode}
          </Label>
        ) : '-'}
      </TableRowCell>
    </TableRow>
  );
}

IndexerErrorsRow.propTypes = {
  timestamp: PropTypes.string.isRequired,
  operationType: PropTypes.string.isRequired,
  errorType: PropTypes.string.isRequired,
  errorMessage: PropTypes.string,
  httpStatusCode: PropTypes.number,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  operationTypeLabels: PropTypes.object.isRequired,
  errorTypeLabels: PropTypes.object.isRequired,
  shortDateFormat: PropTypes.string.isRequired,
  timeFormat: PropTypes.string.isRequired
};

export default IndexerErrorsRow;

