## 2024-07-22 - Keep Commits Atomic

**Learning:** A targeted optimization was rejected because I ran `lint-fix` and committed dozens of unrelated formatting changes across the codebase. This polluted the PR, making the meaningful change difficult to review and violating the principle of atomic commits.

**Action:** I will no longer run broad, auto-fixing commands like `lint-fix` and commit all the results. I must isolate my changes to only the files directly related to the optimization. If linting fixes are required, they should be in a separate, dedicated commit/PR. Always review staged files to ensure no unrelated changes are included.
## 2025-01-24 - Redux Selector Memoization with Specific Input Selectors
**Learning:** Using `(state) => state` as an input selector in `reselect` is a major performance bottleneck as it causes the result function (e.g., expensive sorts) to re-run on every single state change in the application.
**Action:** Always use the most specific state slice possible as an input selector. In this codebase, `getSectionState(state, section, true)` is the idiomatic way to extract a stable reference for a section of the state, preserving memoization.
