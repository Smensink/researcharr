import { createSelector } from 'reselect';

function createBookSelector() {
  return createSelector(
    (state, { bookId }) => bookId,
    (state) => state.books?.itemMap,
    (state) => state.books?.items,
    (bookId, itemMap, allBooks) => {
      if (!bookId || !itemMap || !allBooks) {
        return undefined;
      }
      const index = itemMap[bookId];
      if (index == null) {
        return undefined;
      }
      return allBooks[index];
    }
  );
}

export default createBookSelector;
