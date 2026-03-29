import PropTypes from 'prop-types';
import React, { memo } from 'react';
import styles from './TableRow.css';

function TableRow(props) {
  const {
    className,
    children,
    overlayContent,
    ...otherProps
  } = props;

  return (
    <tr
      className={className}
      {...otherProps}
    >
      {children}
    </tr>
  );
}

TableRow.propTypes = {
  className: PropTypes.string.isRequired,
  children: PropTypes.node,
  overlayContent: PropTypes.bool
};

TableRow.defaultProps = {
  className: styles.row
};

// ⚡ Bolt: Memoizing TableRow to prevent unnecessary re-renders of rows when parent data changes, but this row's data remains the same.
export default memo(TableRow);
