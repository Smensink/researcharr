import { createSelector } from 'reselect';
import getSectionState from 'Utilities/State/getSectionState';

// ⚡ Bolt: Use a specific state slice as an input selector to prevent expensive re-sorts
// when unrelated parts of the state change. Also added safety checks for uninitialized items.
function createSortedSectionSelector(section, comparer) {
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
