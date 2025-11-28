# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Researcharr is a research paper management system forked from Readarr. It monitors researchers (formerly authors), tracks their publications, and downloads research papers from academic sources. The system integrates with OpenAlex for metadata, LibGen/Sci-Hub for paper discovery, and supports direct HTTP downloads.

**Key Transformation**: This is a fork of Readarr transitioning from ebook/audiobook management to research paper management. You'll see legacy naming (Author, Book, ISBN) alongside new naming (Researcher, Paper, DOI). The default port is 7337 (changed from 8787).

## Architecture

### Backend (.NET 6.0)

The backend is a modular ASP.NET Core application using DryIoc for dependency injection:

- **NzbDrone.Core**: Domain logic, services, repositories. Core business rules live here.
  - `Books/`: Domain models for Authors, Books, Series (being transitioned to Researchers/Papers)
  - `MetadataSource/`: OpenAlex integration (`OpenAlexProxy`) and legacy metadata providers
  - `Indexers/`: LibGen, Sci-Hub, and other indexer implementations
  - `Download/`: Download client implementations including HTTP direct download
  - `Datastore/`: Repository pattern with SQLite/PostgreSQL support via custom ORM
  - `DecisionEngine/`: Logic for accepting/rejecting releases based on quality profiles
  - `MediaFiles/`: File scanning, importing, renaming, and organization

- **Readarr.Api.V1**: REST API controllers using ASP.NET Core MVC
  - Follows RESTful conventions with versioned endpoints (`/api/v1/...`)
  - Resources in each controller directory map domain models to DTOs

- **Readarr.Http**: HTTP infrastructure, middleware, authentication
  - `Authentication/`: Forms-based and API key authentication
  - `Frontend/`: Static file serving for the React UI
  - `Middleware/`: Request pipeline components

- **NzbDrone.Host**: Application host, startup, bootstrapping
  - `Startup.cs`: Service registration, middleware pipeline configuration
  - `Bootstrap.cs`: DryIoc container setup and module loading

- **NzbDrone.Common**: Cross-platform utilities (filesystem, process management, HTTP client)

### Frontend (React + Redux)

Located in `frontend/src/`, built with Webpack, uses TypeScript and CSS Modules:

- **Store**: Redux state management with `redux-thunk` for async actions
  - `Actions/`: Action creators for each domain slice
  - `Middleware/`: Custom middleware for SignalR, API calls
  - `Selectors/`: Reselect selectors for derived state

- **Component Structure**: Feature-based organization (Author, Book, Settings, etc.)
  - Each feature has its own components, state, and API integration
  - Uses `connected-react-router` for routing with Redux

- **API Communication**: Custom middleware intercepts actions to make API calls
  - SignalR for real-time updates from the backend
  - API responses automatically update Redux store

### Key Patterns

1. **Proxy Pattern**: External services use proxy classes (e.g., `OpenAlexProxy`, `LibGenRequestGenerator`)
2. **Repository Pattern**: All data access through repositories extending `BasicRepository<T>`
3. **Service Layer**: Business logic in services (e.g., `RefreshAuthorService`, `BookMonitoredService`)
4. **Lazy Loading**: Related entities use `LazyLoaded<T>` pattern for deferred loading
5. **Migrations**: Database schema changes in `src/NzbDrone.Core/Datastore/Migration/`

## Common Commands

### Docker (Recommended)

The project includes Docker support for consistent builds and deployment:

```bash
# Build and run with docker-compose
docker-compose up --build

# Run in detached mode
docker-compose up -d

# Rebuild after changes
docker-compose up --build --force-recreate

# View logs
docker-compose logs -f researcharr

# Stop containers
docker-compose down

# Stop and remove volumes (clean slate)
docker-compose down -v
```

**Docker Setup**:
- Multi-stage build: Node.js (frontend) → .NET SDK (backend) → .NET Runtime (final)
- Includes FlareSolverr service for cloudflare bypass
- Data persisted in Docker volumes (`researcharr_config`, `researcharr_downloads`)
- Books mounted from `./books` directory
- Exposed on port 7337

### Local Development (Without Docker)

```bash
# Full production build (frontend + backend + packages)
./build.sh

# Development build (faster, no StyleCop)
./dev-build.sh

# Development build and run immediately
./dev-build.sh --run

# Frontend only (watch mode)
yarn start
# or
yarn build

# Backend only
dotnet build src/Readarr.sln

# Backend with specific RID
dotnet publish src/NzbDrone.Console/Readarr.Console.csproj \
  -f net6.0 -r osx-arm64 --self-contained \
  -o _output/net6.0/osx-arm64/publish \
  -p:RunAnalyzersDuringBuild=false
```

### Running Locally

```bash
# After dev-build.sh
./_output/net6.0/osx-arm64/publish/Readarr --nobrowser --data=/tmp/researcharr-test

# From source (debug mode)
dotnet run --project src/NzbDrone.Host/Readarr.Host.csproj --framework net6.0

# Default URL: http://localhost:7337
```

### Testing

```bash
# Run all unit tests
./test.sh Linux Unit Test

# Run integration tests
./test.sh Linux Integration Test

# Run specific test project
dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj

# With coverage
./test.sh Linux Unit Coverage
```

### Linting

```bash
# Frontend linting
yarn lint
yarn lint-fix

# CSS linting (Linux)
yarn stylelint-linux

# CSS linting (Windows)
yarn stylelint-windows

# Backend formatting
dotnet format src/Readarr.sln
```

## Development Workflow

### Docker-Based Development

For most development work, use Docker for consistent builds:

1. Make changes to source code (frontend or backend)
2. Rebuild and restart containers:
   ```bash
   docker-compose up --build --force-recreate
   ```
3. Access at `http://localhost:7337`
4. Check logs: `docker-compose logs -f researcharr`

**Important**: The Docker build runs full production builds, so changes require container rebuilds. For rapid frontend iteration, consider local development with `yarn start`.

### Frontend Changes (Local)

1. Make changes in `frontend/src/`
2. Run `yarn start` for watch mode
3. **Debug Mode**: UI assets served from `_output/net6.0/{RID}/UI`
4. **Production Mode**: UI assets served from `_output/net6.0/{RID}/publish/UI`
5. After `yarn build`, copy to appropriate location:
   ```bash
   # For debug mode:
   cp -r _output/UI/* _output/net6.0/osx-arm64/UI/

   # For publish mode:
   cp -r _output/UI/* _output/net6.0/osx-arm64/publish/UI/
   ```

### Backend Changes

1. Make changes in `src/`
2. If modifying data models, add migration in `src/NzbDrone.Core/Datastore/Migration/`
3. Register new services in appropriate module or `Startup.cs`
4. Build with `dotnet build` or use `./dev-build.sh`
5. **StyleCop Workaround**: If StyleCop SA1200 errors block builds, use:
   ```bash
   dotnet build -p:RunAnalyzersDuringBuild=false
   ```

### Adding New Indexers

1. Create folder in `src/NzbDrone.Core/Indexers/{IndexerName}/`
2. Implement `IIndexer` interface with Settings, RequestGenerator, Parser
3. Settings class for user configuration
4. RequestGenerator for building search URLs
5. Parser for converting responses to `ReleaseInfo` objects
6. Register in DryIoc container (typically auto-discovered via assembly scanning)

### Database Migrations

All migrations in `src/NzbDrone.Core/Datastore/Migration/`:
- Name format: `{Number}_{Description}.cs`
- Inherit from `NzbDroneMigrationBase`
- Use `Alter`, `Create`, `Delete`, `Execute` methods
- Both SQLite and PostgreSQL are supported

## Important Context

### Researcharr-Specific Changes

This fork has made the following key changes from Readarr:

1. **Metadata Source**: Switched from Goodreads to OpenAlex
   - `OpenAlexProxy` implements `IOpenAlexProxy`
   - `BookInfoProxy` delegates to OpenAlex while maintaining legacy interface
   - Fetches up to 200 papers per researcher

2. **Indexers**: Added LibGen and Sci-Hub support
   - Located in `src/NzbDrone.Core/Indexers/LibGen/` and `.../SciHub/`
   - Direct PDF downloads via HTTP client

3. **Download Clients**: HTTP direct download added
   - `src/NzbDrone.Core/Download/Clients/Http/`
   - No torrent/usenet required for academic papers

4. **UI Terminology**: Partial migration in progress
   - "ISBN" → "DOI" (completed)
   - "Author" → "Researcher" (in localization files)
   - "Book" → "Paper" (in localization files)
   - Many components still use legacy naming internally

5. **Port**: Changed from 8787 to 7337
   - Updated in configuration and default settings

### Known Issues

1. **StyleCop**: SA1200 errors may block builds
   - Use `-p:RunAnalyzersDuringBuild=false` to bypass

2. **UI Deployment**: Frontend builds output to `_output/UI/`
   - Must manually copy to publish folder after build
   - Debug vs. publish mode serves from different directories

3. **Legacy Code**: Many references to "Author" and "Book" in C# code
   - UI localization handles display text
   - Backend domain models retain original naming

4. **Calibre Integration**: Partially removed but some references remain
   - Calibre-specific features should be skipped/removed when encountered

## Testing Approach

- **Unit Tests**: Use NUnit, FluentAssertions, Moq
- **Integration Tests**: Full API tests with in-memory database
- **Manual Testing**: No automated UI tests; verify UI changes manually
- **Categories**: Tests use `[Category]` attributes; ManualTest excluded by default

## File Locations

- **Config**: `~/.config/Readarr` (Linux/Mac), `%ProgramData%/Readarr` (Windows)
- **Database**: SQLite files in config directory (`readarr.db`, `logs.db`)
- **Logs**: Written to config directory and console
- **Builds**: `_output/` for artifacts, `_artifacts/` for packages
- **UI Assets**: `_output/UI/` after webpack build

## Notes on Dependencies

- Uses custom fork of several libraries (check `src/Libraries/`)
- DryIoc for DI (not Microsoft.Extensions.DependencyInjection)
- SignalR for real-time communication
- Dapper-like custom ORM in `BasicRepository`
- NLog for logging

## When Making Changes

1. **Respect the transition**: Use Paper/Researcher in UI text, but Author/Book in code is acceptable
2. **Migrations required**: Any database schema changes need a migration
3. **API versioning**: All API endpoints under `/api/v1/`
4. **Redux patterns**: Follow existing action/reducer/selector structure
5. **CSS Modules**: Use `.css` files co-located with components
6. **TypeScript**: Frontend is TypeScript, use proper typing
7. **Localization**: User-facing text goes in `src/NzbDrone.Core/Localization/Core/en.json`
- researcharr logs an be found at http://localhost:7337/logfile/readarr.debug.txt and in the docker container log files along with the flaresolverr logs
## Recent Enhancements (Quick Wins - 2025-11-28)

### Indexers
The following indexers are implemented and functional:

1. **LibGen** (`src/NzbDrone.Core/Indexers/LibGen/`)
   - Enhanced with browser-like headers to bypass anti-bot detection
   - Supports multiple mirrors (libgen.li, libgen.vg, libgen.la, etc.)
   - Handles multi-author papers with filename truncation to prevent PathTooLongException
   - Properly sets Author/Book/Doi fields for matching

2. **Unpaywall** (`src/NzbDrone.Core/Indexers/Unpaywall/`)
   - DOI-based open access paper discovery
   - Fixed to use correct `raw_author_name` field from API
   - Requires email address for API usage

3. **ArXiv** (`src/NzbDrone.Core/Indexers/Arxiv/`)
   - Preprint server for physics, CS, math papers
   - XML-based API with full metadata support
   - Properly extracts DOI and author information

4. **SciHub** (`src/NzbDrone.Core/Indexers/SciHub/`)
   - Comprehensive academic paper access
   - Supports multiple mirror domains

5. **PubMed Central** (`src/NzbDrone.Core/Indexers/PubMedCentral/`) - NEW!
   - Biomedical and life sciences papers
   - Uses NCBI E-utilities API
   - Supports optional API key for higher rate limits
   - Free full-text access to PMC articles

### Features

#### BibTeX Export
**Endpoint**: `GET /api/v1/book/{id}/export/bibtex`

Export any paper to BibTeX format for citation management.

**Usage**:
```bash
curl http://localhost:7337/api/v1/book/123/export/bibtex
```

**Example Output**:
```bibtex
@article{Smith2023Quantum,
  author = {John Smith},
  title = {{Quantum Computing Applications}},
  year = {2023},
  doi = {10.1234/example},
}
```

### Fixed Issues

1. **PathTooLongException** - LibGen papers with many authors now truncate to "FirstAuthor et al." in filenames
2. **DOI-Based Backfilling** - Only backfills metadata when DOI matches, preventing false positives
3. **Parser Field Population** - All indexers properly set Author, Book, and Doi fields
