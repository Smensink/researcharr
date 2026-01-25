## 2024-07-22 - Keep Commits Atomic

**Learning:** A targeted optimization was rejected because I ran `lint-fix` and committed dozens of unrelated formatting changes across the codebase. This polluted the PR, making the meaningful change difficult to review and violating the principle of atomic commits.

**Action:** I will no longer run broad, auto-fixing commands like `lint-fix` and commit all the results. I must isolate my changes to only the files directly related to the optimization. If linting fixes are required, they should be in a separate, dedicated commit/PR. Always review staged files to ensure no unrelated changes are included.

## 2024-07-23 - Never Commit Build Artifacts

**Learning:** My change was rejected because I accidentally committed a vast number of build artifacts (`_temp/`, `node_modules`) and dependency-related files instead of just my source code. This is a major violation that breaks builds and pollutes the version history.

**Action:** Always run `git status` before committing to verify the list of staged files. Ensure that only intentional, hand-edited source code files are included. If the workspace becomes messy, the correct recovery procedure is to save the intended source code, use `reset_all` to clean the repository, and then re-apply the single-file change. Never commit files from `_temp`, `_output`, `_artifacts`, or `node_modules`.
