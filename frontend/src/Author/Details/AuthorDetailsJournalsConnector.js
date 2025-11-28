import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import AuthorDetailsJournals from './AuthorDetailsJournals';

function createMapStateToProps() {
  return createSelector(
    createClientSideCollectionSelector('books', 'authorDetails'),
    (books) => {
      return {
        books: books.items || []
      };
    }
  );
}

export default connect(createMapStateToProps)(AuthorDetailsJournals);
