# Repository Guidelines

## Project Structure & Module Organization
`src/Readarr.sln` owns the .NET solution; key workloads sit inside `src/NzbDrone.Core` (domain logic), `src/NzbDrone.Host` (service host), `Readarr.Api.V1`, and related test projects. The React/TypeScript UI is under `frontend/src` and emits `_output/UI` during webpack builds. Data contracts live in `schemas/`, localization JSON in `src/NzbDrone.Core/Localization`, and release assets/services under `distribution/`. Scripts such as `build.sh`, `test.sh`, and `docs.sh` glue these layers together in CI.

## Build, Test, and Development Commands
- `dotnet build src/Readarr.sln` – quick smoke build of every backend project.
- `dotnet run --project src/NzbDrone.Host/Readarr.Host.csproj --framework net6.0` – host runtime using the new port 7337.
- `./build.sh` – end-to-end release build: yarn install, lint, webpack, and RID-specific `dotnet publish` into `_artifacts/`.
- `yarn install --frozen-lockfile && yarn start` – install UI deps and run webpack watch while iterating on `frontend`.
- `./test.sh Linux Unit Test` (swap args) – thin wrapper around `dotnet test` using the assembly/filter list baked into the script.

## Coding Style & Naming Conventions
`.editorconfig` enforces UTF-8, LF, spaces, and four-space indents for C#. Prefer `var`, order `using` statements with `System.*` first, and prefix private fields with `_camelCase`; StyleCop analyzers will fail builds otherwise. Run `dotnet format` (or IDE analyzers) plus `yarn lint`/`yarn lint-fix` before committing. The UI follows ESLint + Prettier and Stylelint, so keep component files PascalCase, hooks camelCase, and sorted imports (`eslint-plugin-simple-import-sort`).

## Testing Guidelines
`test.sh` bundles assemblies like `Readarr.Core.Test.dll` and `Readarr.Integration.Test.dll` and forwards them to `dotnet test` with coverlet settings from `src/coverlet.runsettings`. Choose a platform (`Windows|Linux|Mac`), a scope (`Unit|Integration|Automation`), and `Coverage` vs `Test` to control filters and reports. Categories exclude `ManualTest`, so mark new suites with the correct `Category` attribute. UI changes currently depend on manual smoke tests—document any future automated coverage in-tree.

## Commit & Pull Request Guidelines
When cloning the full repository you will see commit subjects in the style `<area>: <imperative summary>` (e.g., `Indexer: tighten LibGen parser`). Continue that structure, keep bodies wrapped near 72 characters, and reference issue IDs in the footer. PRs must declare database migrations, summarize the intent, attach UI screenshots when applicable, tick off tests/translations/wiki updates, and close issues with `Fixes #NNNN` per `.github/PULL_REQUEST_TEMPLATE.md`.

## Security & Configuration Tips
Never post vulnerability details publicly; follow `SECURITY.md` by contacting a Servarr dev on Discord or emailing `development@servarr.com`. Runtime data directories live in `%ProgramData%/Readarr` (Windows) or `~/.config/Readarr` (Unix), so sanitize local configs—especially ports, OpenAlex keys, or LibGen/Sci-Hub endpoints—before committing.
