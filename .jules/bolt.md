## 2024-07-22 - Keep Commits Atomic

**Learning:** A targeted optimization was rejected because I ran `lint-fix` and committed dozens of unrelated formatting changes across the codebase. This polluted the PR, making the meaningful change difficult to review and violating the principle of atomic commits.

**Action:** I will no longer run broad, auto-fixing commands like `lint-fix` and commit all the results. I must isolate my changes to only the files directly related to the optimization. If linting fixes are required, they should be in a separate, dedicated commit/PR. Always review staged files to ensure no unrelated changes are included.