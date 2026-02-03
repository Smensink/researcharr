# Bolt's Journal - Critical Performance Learnings

## 2025-05-15 - Redux Selector Performance Anti-Pattern
**Learning:** Using `(state) => state` as an input selector in Reselect is a major performance bottleneck. It causes the selector's result function to execute on every single action dispatched to the store, because the global state object reference changes. This is particularly expensive when the result function performs data transformations like `.sort()` or `.filter()`.
**Action:** Always use specific state slice selectors (e.g., `(state) => state.slice`) as input selectors. In this codebase, `getSectionState(state, section, true)` is the idiomatic way to access a slice by its path while maintaining reference stability for memoization.

## 2025-05-15 - Unused Variables and Strict Linting
**Learning:** The frontend ESLint configuration strictly enforces `no-unused-vars`. Destructuring props that are not used in the component body (common in legacy components) will fail the lint check.
**Action:** When refactoring or optimizing components, always remove unused destructured variables to ensure the build passes.
