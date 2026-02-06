## 2025-05-22 - Global O(N) selection optimization with WeakMap
**Learning:** In applications using immutable state updates (like Redux or React setState), utility functions that iterate over state objects can be optimized globally using a `WeakMap`. By caching the result based on the object reference, redundant O(N) calculations during re-renders are reduced to O(1).
**Action:** Always check if core utility functions that process state objects (like `getSelectedIds`) can benefit from reference-based memoization.

## 2025-05-22 - Gating expensive calculations in render
**Learning:** Calculating derived data (like selected ID lists) at the top of a `render()` method is wasteful if that data is only needed for a conditional UI element (like an editor footer).
**Action:** Move expensive calculations inside the conditional block that requires them, or use a ternary to gate their execution based on the relevant state (e.g., `isEditorActive`).
