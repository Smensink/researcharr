import PropTypes from 'prop-types';
import React, { memo, useState } from 'react';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons } from 'Helpers/Props';
import BookInteractiveSearchModalConnector from './Search/BookInteractiveSearchModalConnector';
import styles from './BookSearchCell.css';

// This component was converted to a functional component and wrapped with React.memo
// to prevent unnecessary re-renders. As a frequently used cell in lists, this
// avoids re-rendering when parent components update but this component's props
// remain unchanged, leading to a measurable performance improvement.
const BookSearchCell = ({
  bookId,
  bookTitle,
  authorName,
  isSearching,
  onSearchPress,
  ...otherProps
}) => {
  const [isDetailsModalOpen, setDetailsModalOpen] = useState(false);

  const onManualSearchPress = () => {
    setDetailsModalOpen(true);
  };

  const onDetailsModalClose = () => {
    setDetailsModalOpen(false);
  };

  return (
    <TableRowCell className={styles.BookSearchCell}>
      <SpinnerIconButton
        name={icons.SEARCH}
        isSpinning={isSearching}
        onPress={onSearchPress}
      />

      <IconButton
        name={icons.INTERACTIVE}
        onPress={onManualSearchPress}
      />

      <BookInteractiveSearchModalConnector
        isOpen={isDetailsModalOpen}
        bookId={bookId}
        bookTitle={bookTitle}
        authorName={authorName}
        onModalClose={onDetailsModalClose}
        {...otherProps}
      />
    </TableRowCell>
  );
};

BookSearchCell.propTypes = {
  bookId: PropTypes.number.isRequired,
  authorId: PropTypes.number.isRequired,
  bookTitle: PropTypes.string.isRequired,
  authorName: PropTypes.string.isRequired,
  isSearching: PropTypes.bool.isRequired,
  onSearchPress: PropTypes.func.isRequired
};

export default memo(BookSearchCell);
