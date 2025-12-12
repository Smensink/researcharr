import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import { icons } from 'Helpers/Props';
import styles from './JournalsRow.css';

function JournalsRow(props) {
  const {
    id,
    name,
    titleSlug,
    paperCount,
    monitored,
    columns
  } = props;

  return (
    <TableRow>
      {
        columns.map((column) => {
          const {
            name: columnName,
            isVisible
          } = column;

          if (!isVisible) {
            return null;
          }

          if (columnName === 'name') {
            return (
              <TableRowCell key={columnName} className={styles.name}>
                <Icon
                  name={icons.BOOK}
                  size={14}
                />
                {' '}
                <Link to={`/author/${titleSlug}`}>
                  {name}
                </Link>
              </TableRowCell>
            );
          }

          if (columnName === 'monitored') {
            return (
              <TableRowCell key={columnName} className={styles.monitored}>
                <MonitorToggleButton
                  monitored={monitored}
                  isDisabled={true}
                />
              </TableRowCell>
            );
          }

          if (columnName === 'paperCount') {
            return (
              <TableRowCell key={columnName}>
                {paperCount}
              </TableRowCell>
            );
          }

          return null;
        })
      }
    </TableRow>
  );
}

JournalsRow.propTypes = {
  id: PropTypes.number.isRequired,
  name: PropTypes.string.isRequired,
  titleSlug: PropTypes.string.isRequired,
  paperCount: PropTypes.number.isRequired,
  monitored: PropTypes.bool.isRequired,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired
};

JournalsRow.defaultProps = {
  monitored: false,
  titleSlug: 'unknown',
  paperCount: 0
};

export default JournalsRow;
