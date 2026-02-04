import { createSelector } from 'reselect';
import getSectionState from 'Utilities/State/getSectionState';

function createSortedSectionSelector(section, comparer) {
  return createSelector(
    (state) => getSectionState(state, section, true),
    (sectionState) => {
      // ⚡ Bolt: Prevent expensive sorts and re-renders by using a more specific input selector.
      // ⚡ Bolt: Re-sorting only when the relevant section state changes.
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
