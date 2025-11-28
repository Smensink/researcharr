import { createSelector, createSelectorCreator, defaultMemoize } from 'reselect';
import hasDifferentItemsOrOrder from 'Utilities/Object/hasDifferentItemsOrOrder';
import createClientSideCollectionSelector from './createClientSideCollectionSelector';

function createUnoptimizedSelector(uiSection) {
  return createSelector(
    createClientSideCollectionSelector('authors', uiSection),
    (authors) => {
      // Filter out journals from the authors list (they have their own page)
      const filteredItems = authors.items.filter((s) => s.type !== 'journal');

      const items = filteredItems.map((s) => {
        const {
          id,
          sortName,
          sortNameLastFirst
        } = s;

        return {
          id,
          sortName,
          sortNameLastFirst
        };
      });

      return {
        ...authors,
        items
      };
    }
  );
}

function authorListEqual(a, b) {
  return hasDifferentItemsOrOrder(a, b);
}

const createAuthorEqualSelector = createSelectorCreator(
  defaultMemoize,
  authorListEqual
);

function createAuthorClientSideCollectionItemsSelector(uiSection) {
  return createAuthorEqualSelector(
    createUnoptimizedSelector(uiSection),
    (author) => author
  );
}

export default createAuthorClientSideCollectionItemsSelector;
