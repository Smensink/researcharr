# Bolt's Journal ⚡

## 2025-05-15 - Redux Selector Anti-pattern: (state) => state
**Learning:** Using `(state) => state` as an input selector in `reselect` is a performance killer. It causes the result function to execute on every single state change in the Redux store, regardless of whether the relevant data changed.
**Action:** Always use specific state slices as input selectors. Use utilities like `getSectionState(state, section, true)` to target the exact data needed for the selector.

## 2025-05-15 - Robust Selectors
**Learning:** Selectors should be defensive. Adding safety checks for `undefined` state slices or items prevents runtime crashes during initial loads or when data is missing.
**Action:** Include checks like `if (!sectionState || !sectionState.items)` before performing operations like `sort` or `map`.
