import { createSelector } from 'reselect';

function createAuthorSelector() {
  return createSelector(
    (state, { authorId }) => authorId,
    (state) => state.authors?.itemMap,
    (state) => state.authors?.items,
    (authorId, itemMap, allAuthors) => {
      if (!authorId || !itemMap || !allAuthors) {
        return undefined;
      }
      const index = itemMap[authorId];
      if (index == null) {
        return undefined;
      }
      return allAuthors[index];
    }
  );
}

export default createAuthorSelector;
