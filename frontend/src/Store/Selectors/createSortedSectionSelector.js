import { createSelector } from 'reselect';
import getSectionState from 'Utilities/State/getSectionState';

// ⚡ Bolt: Using a specific input selector instead of (state) => state prevents
// this selector from re-computing and re-sorting on every single state change in the app.
// It will now only re-compute when the relevant section of the state actually changes.
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
