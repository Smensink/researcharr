import { createSelector } from 'reselect';
import getSectionState from 'Utilities/State/getSectionState';

function createSortedSectionSelector(section, comparer) {
  // ⚡ Bolt: Optimize memoization by only depending on the relevant slice of state.
  // This prevents unnecessary re-sorting when unrelated parts of the Redux state change.
  return createSelector(
    (state) => getSectionState(state, section, true),
    (sectionState) => {
      if (!sectionState || !sectionState.items) {
        return sectionState;
      }

      return {
        ...sectionState,
        items: [...sectionState.items].sort(comparer)
      };
    }
  );
}

export default createSortedSectionSelector;
