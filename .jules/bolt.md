## 2026-02-04 - [Reselect Optimization: Narrow Input Selectors]
**Learning:** Using `(state) => state` as an input selector in Reselect is a major performance bottleneck. It causes the result function to re-run on every Redux action because the state reference changes.
**Action:** Always use specific state slices or specialized getters (like `getSectionState`) as input selectors to ensure memoization works correctly.
