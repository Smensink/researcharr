import { createSelector } from 'reselect';
import getSectionState from 'Utilities/State/getSectionState';

// ⚡ Bolt: Optimized by using a specific state slice as an input selector.
// This prevents the selector from re-calculating (and re-sorting) on every state change,
// only running when the relevant section state actually changes.
function createSortedSectionSelector(section, comparer) {
  return createSelector(
    (state) => getSectionState(state, section, true),
    (sectionState) => {
      return {
        ...sectionState,
        items: [...sectionState.items].sort(comparer)
      };
    }
  );
}

export default createSortedSectionSelector;
