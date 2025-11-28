# Researcharr Project Handoff Document

**Date**: 2025-11-21
**Current Workspace**: `/Users/sebastianmensink/Documents/Researcharr-develop/`
**Goal**: Transform Readarr into Researcharr (research paper management)

---

## 🎯 Project Goals

Transform Readarr into **Researcharr**, a research paper management system with:

1.  **OpenAlex Integration** - Primary metadata provider for researchers and papers
2.  **LibGen & Sci-Hub Indexers** - Finding research papers via these sources
3.  **Direct HTTP Downloads** - Download PDFs directly without torrent/usenet clients
4.  **UI Rebranding** - Change all user-facing text from book terminology to paper terminology
5.  **Feature Cleanup** - Remove book-specific features (Calibre, Goodreads, EPUB handling)
6.  **Port Change** - Default port from 8787 to 7337

---

## ✅ Accomplishments (Cumulative)

### 1. Core Components Implemented

- **OpenAlex Metadata**: `OpenAlexSettings`, `OpenAlexProxy` (implements `IOpenAlexProxy`), `OpenAlexResources`.
- **LibGen Indexer**: `LibGenSettings`, `LibGenRequestGenerator`, `LibGenParser`, `LibGen`.
- **SciHub Indexer**: `SciHubSettings`, `SciHubRequestGenerator`, `SciHubParser`, `SciHub`.
- **HTTP Download Client**: `HttpDownloadSettings`, `HttpDownload`.

### 2. Refactoring & Integration

- **Metadata Architecture**:
  - Created `IOpenAlexProxy` interface.
  - Refactored `BookInfoProxy` to inject `IOpenAlexProxy` and delegate metadata calls (Author/Book info, Search) to OpenAlex.
  - Preserved `AddDbIds` logic in `BookInfoProxy` to ensure database synchronization.
  - Registered `OpenAlexSettings` in `Startup.cs` for Dependency Injection.
- **Legacy Removal**:
  - Removed Goodreads and Calibre dependencies from `ImportListSyncService`, `EBookTagService`, `CandidateService`, `BookInfoProxy`.
  - Deleted legacy files in `NzbDrone.Core/MetadataSource/Goodreads`, `NzbDrone.Core/Books/Calibre`, etc.
  - Cleaned up `CustomScript.cs` environment variables.

### 3. UI Rebranding & Cleanup

- Updated Login, Sidebar, Header, Error pages.
- Changed terminology: Author -> Researcher, Book -> Paper, etc.
- Updated Port to 7337.
- **Latest Session (2025-11-21)**:
  - **ISBN → DOI Rebranding**: Replaced all UI references from "ISBN" to "DOI" in search placeholders and metadata profile settings.
  - **Search Improvements**: Added explicit `sort=relevance_score:desc` to OpenAlex API requests for both authors and works.
  - **Unified Search**: Modified `BookInfoProxy.SearchForNewEntity` to return both authors and works in combined search results.
  - **UI Compactification**: Removed `BookCover` and `AuthorPoster` components from search results; reduced margins/padding for compact layout.
  - **Books → Papers**: Renamed "Books" section to "Papers" on Author details page via `en.json` localization updates.

### 4. Author Adding & Paper Monitoring (NEW)

- **OpenAlex Works Fetching**:
  - Modified `OpenAlexProxy.GetAuthorInfo` to fetch up to 200 papers per author from OpenAlex API.
  - Makes secondary API call: `GET /works?filter=author.id:{id}&per_page=200`
  - Populates `author.Books` with fetched works as `LazyLoaded<List<Book>>`.
- **Frontend Verification**:
  - Confirmed `AddNewAuthorModalContent` includes `AddAuthorOptionsForm` with monitoring dropdown.
  - Monitoring options (All Books, Future Books, Missing Books, etc.) correctly map to backend `MonitorTypes` enum.
- **Backend Processing**:
  - Existing `RefreshAuthorService` handles author refresh and applies monitoring settings.
  - `MonitorNewBookService` and `BookMonitoredService` process monitoring options correctly.

### 5. UI/Frontend Fixes

- Fixed UI auth/authorization flow so static assets load without auth; frontend now renders.
- Hid Calibre/ebook settings in the root folder modal (papers focus) and defaulted `outputProfile` to the valid enum `Default` to stop API validation failures when adding root folders.
- Corrected lingering `window.Researcharr` references (logo/sidebar) so JS no longer crashes.
- build.sh now copies built UI into published RID folders automatically after webpack; npm build path works if yarn is missing.
- Published osx-arm64 build and restarted the app on 7337 (current binary `_output/net6.0/osx-arm64/publish/Readarr`, logs `/tmp/researcharr.log`).

---

## ⚠️ Current Status & Known Issues

### 1. Build Status

- **Status**: Should compile with `-p:RunAnalyzersDuringBuild=false`.
- **Known Issue**: StyleCop SA1200 errors block publish without disabling analyzers.
- **Action Required**: Use `dotnet build -p:RunAnalyzersDuringBuild=false` or `dotnet publish -p:RunAnalyzersDuringBuild=false`.

### 2. Deployment

- **Current Issue**: Assembly version mismatches and missing `Readarr.Mono.dll` when publishing individual projects.
- **Workaround**: Use full solution publish: `dotnet publish src/NzbDrone.Console/Readarr.Console.csproj -f net6.0 -r osx-arm64 --self-contained -o _output/net6.0/osx-arm64/publish -p:RunAnalyzersDuringBuild=false`
- **UI Deployment**: Copy UI after frontend build: `mkdir -p _output/net6.0/osx-arm64/publish/UI && cp -r _output/UI/* _output/net6.0/osx-arm64/publish/UI/`

### 3. UI Development & Debug Mode (CRITICAL)

- **Debug Mode Behavior**: When running `Readarr` locally without specific flags (or with `dotnet run`), it defaults to **Debug mode**.
- **Asset Path**: In Debug mode, the application serves UI assets from `_output/net6.0/osx-arm64/UI` (or equivalent RID path), **NOT** from `publish/UI`.
- **Consequence**: Deploying changes to `publish/UI` will be ignored if the app is running in Debug mode.
- **Fix**: Update the assets in the Debug output directory:
  ```bash
  # For Debug mode (local dev):
  mkdir -p _output/net6.0/osx-arm64/UI
  cp -r _output/UI/* _output/net6.0/osx-arm64/UI/
  ```

### 3. Dependency Injection

- **OpenAlex**: `OpenAlexSettings` is registered. `OpenAlexProxy` implements `IOpenAlexProxy`.
- **Verification Needed**: Ensure `OpenAlexProxy` is correctly registered in the DI container (DryIoc). It might be auto-registered via `AutoAddServices`, but verify if `IOpenAlexProxy` injection into `BookInfoProxy` works at runtime.

### 4. Legacy Cleanup

- **Status**: Major components removed.
- **Remaining**:
  - Test files (`NzbDrone.Core.Test`) likely reference removed components and will fail compilation/execution.
  - Some localization strings may still reference legacy terminology.

---

## 📝 Next Steps for the Incoming Agent

1.  **Test Author Adding Workflow**:

    - Start Researcharr using your normal workflow (binary at `_output/net6.0/osx-arm64/publish/Readarr`).
    - Navigate to `http://localhost:7337/add/search`.
    - Search for "Gary Hoffman" (OpenAlex ID: A5107386586).
    - Click author name to open Add Author modal.
    - Select "All Books" monitoring option and add author.
    - Verify papers are fetched and displayed on author details page.

2.  **Fix Deployment Pipeline**:

    - Investigate why `Readarr.Mono.dll` is missing from publish output.
    - Create reliable build/publish script that handles all dependencies consistently.
    - Consider using the original build scripts if available.

3.  **Test Cleanup**:

    - Run `dotnet test`.
    - Fix or delete tests that reference removed Goodreads/Calibre components.

4.  **End-to-End Test**:

    - Add a Researcher (Author) via UI (should use OpenAlex).
    - Verify papers are fetched automatically.
    - Search for a Paper (Book).
    - Grab a release via LibGen/SciHub.
    - Download via HttpDownloadClient.

---

## 📂 Key File Locations

- **Metadata**: `src/NzbDrone.Core/MetadataSource/OpenAlex/`
  - `OpenAlexProxy.cs` - Modified to fetch works in `GetAuthorInfo` method
- **Proxy Integration**: `src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs`
- **Indexers**: `src/NzbDrone.Core/Indexers/LibGen/`, `src/NzbDrone.Core/Indexers/SciHub/`
- **Download Client**: `src/NzbDrone.Core/Download/Clients/HttpDownload/`
- **Startup Config**: `src/NzbDrone.Host/Startup.cs`
- **Localization**: `src/NzbDrone.Core/Localization/Core/en.json` - Updated with "Papers" terminology
- **Frontend Search**:
  - `frontend/src/Search/Author/AddNewAuthorModalContent.js` - Author add modal
  - `frontend/src/Search/Common/AddAuthorOptionsForm.js` - Monitoring options form
  - `frontend/src/Search/Author/AddNewAuthorSearchResult.js` - Compact search results

---

## 🔧 Build & Publish Guide

### Quick Reference

```bash
# Full production build (backend + frontend + packages)
./build.sh --all

# Development build (backend + frontend only, faster)
./build.sh --backend --frontend --runtime osx-arm64 --framework net6.0

# Frontend only (after making UI changes)
./build.sh --frontend

# Backend only (after C# changes)
./build.sh --backend --runtime osx-arm64 --framework net6.0
```

### Recommended: Using dev-build.sh (Development)

For day-to-day development, use the **`dev-build.sh`** script which automates the entire process:

```bash
# Build everything and start the app
./dev-build.sh --run

# Just build (don't start)
./dev-build.sh
```

**What it does:**

1. Stops any running Researcharr instances
2. Builds frontend (yarn or npm)
3. Builds backend with StyleCop disabled
4. Deploys UI to publish directory
5. Optionally starts the app

**Advantages:**

- ✅ No StyleCop build failures
- ✅ Automatic UI deployment
- ✅ One-command workflow
- ✅ Prevents file lock errors
- ✅ Faster iteration

See `DEV_BUILD.md` for full documentation.

### Recommended: Using build.sh (Production)

For production builds and packages, use the original **`build.sh`** script:

The project includes a comprehensive build script (`build.sh`) that handles all build complexities. **This is the recommended approach.**

#### Full Production Build

```bash
# Build everything - backend, frontend, and create packages
./build.sh --all

# Or specify individual components
./build.sh --backend --frontend --packages
```

This will:

1. Build all C# projects for all platforms
2. Run webpack to build the frontend
3. Copy UI assets to all RID output directories
4. Create distribution packages

#### Platform-Specific Build (Faster for Development)

```bash
# Build for specific runtime (much faster)
./build.sh --backend --frontend --runtime osx-arm64 --framework net6.0

# For Linux
./build.sh --backend --frontend --runtime linux-x64 --framework net6.0

# For Windows
./build.sh --backend --frontend --runtime win-x64 --framework net6.0
```

#### Output Locations

After building, binaries and UI are located at:

- **Backend**: `_output/net6.0/{runtime}/publish/`
- **Frontend**: `_output/UI/` (then copied to each runtime publish folder)
- **Tests**: `_tests/net6.0/{runtime}/publish/`
- **Packages**: `_artifacts/{runtime}/net6.0/Readarr/`

### Manual Build (Alternative Method)

If `build.sh` isn't working or you need more control:

#### 1. Frontend Build

```bash
# Install dependencies (first time only)
yarn install --frozen-lockfile

# Or if yarn is not available
npm install --legacy-peer-deps

# Build frontend
yarn run build --env production
# Or: npm run build -- --env production
```

Frontend output: `_output/UI/`

#### 2. Backend Build

```bash
# Build the solution
dotnet build src/Readarr.sln -c Release -p:RunAnalyzersDuringBuild=false

# Or publish for specific runtime
dotnet publish src/Readarr.sln \
  -c Release \
  -r osx-arm64 \
  -f net6.0 \
  --self-contained \
  -p:RunAnalyzersDuringBuild=false \
  -p:Platform=Posix
```

#### 3. Copy UI to Backend Output

```bash
# For each runtime identifier you built
RID=osx-arm64  # or linux-x64, win-x64, etc.

# Copy to publish directory
mkdir -p _output/net6.0/$RID/publish/UI
cp -r _output/UI/* _output/net6.0/$RID/publish/UI/

# Also copy to non-publish directory (for Debug mode)
mkdir -p _output/net6.0/$RID/UI
cp -r _output/UI/* _output/net6.0/$RID/UI/
```

### Running Researcharr

#### Production Mode (from publish folder)

```bash
# Standard run
./_output/net6.0/osx-arm64/publish/Readarr

# With custom data directory
./_output/net6.0/osx-arm64/publish/Readarr --nobrowser --data=/path/to/data

# Logs location (default)
tail -f ~/.config/Readarr/logs/Readarr.txt
```

#### Development Mode

```bash
# Run from source (uses Debug configuration)
dotnet run --project src/NzbDrone.Console/Readarr.Console.csproj

# Note: In Debug mode, UI is served from _output/net6.0/{runtime}/UI
# NOT from publish/UI subdirectory!
```

### Known Build Issues & Solutions

#### 1. StyleCop Errors Block Build

**Problem**: SA1200, SA1513 errors prevent compilation

**Solution**: Disable StyleCop during build:

```bash
-p:RunAnalyzersDuringBuild=false
```

#### 2. File Lock Errors During Build

**Problem**: "Cannot copy file X because it is being used by another process"

**Solution**: Stop all running Researcharr instances before building:

```bash
# Find and kill running instances
pkill -f Readarr

# Or find the process
ps aux | grep Readarr
kill <PID>
```

#### 3. Missing UI After Build

**Problem**: Application runs but shows 404 or blank page

**Causes**:

1. **Frontend not built**: Run `./build.sh --frontend` or `yarn run build`
2. **Debug mode serving wrong directory**: Copy UI to `_output/net6.0/{runtime}/UI/` not just `publish/UI/`
3. **Cache issue**: Hard refresh browser (Cmd+Shift+R on Mac)

**Solution**:

```bash
# Ensure UI is built
./build.sh --frontend

# Copy to BOTH locations
RID=osx-arm64
cp -r _output/UI/* _output/net6.0/$RID/UI/
cp -r _output/UI/* _output/net6.0/$RID/publish/UI/
```

#### 4. Assembly Version Mismatches

**Problem**: Different assemblies have different versions

**Solution**: Always build from solution root using `build.sh` or full `dotnet build src/Readarr.sln`

### Cache Busting for UI Changes

After modifying frontend code, the browser may cache old assets. The app uses a version parameter (`?v=navy3`) in `index.ejs`:

```html
<script src="<%= htmlWebpackPlugin.options.urlBase %>/initialize.js?v=navy3"></script>
```

Increment this version number when deploying UI changes to force cache refresh.

### Platform-Specific Notes

#### macOS (Apple Silicon)

- Runtime: `osx-arm64`
- Platform: `Posix`
- Self-contained recommended for distribution

#### macOS (Intel)

- Runtime: `osx-x64`
- Platform: `Posix`

#### Linux

- Runtime: `linux-x64` (most common)
- Also: `linux-arm64`, `linux-musl-x64` (Alpine)
- Platform: `Posix`

#### Windows

- Runtime: `win-x64` (most common)
- Also: `win-x86` (32-bit)
- Platform: `Windows`

### Testing After Build

1. **Verify backend compiled**:

   ```bash
   ls -lh _output/net6.0/osx-arm64/publish/Readarr
   # Should be ~145KB executable
   ```

2. **Verify UI exists**:

   ```bash
   ls _output/net6.0/osx-arm64/publish/UI/
   # Should contain: index.html, Content/, fonts/, etc.
   ```

3. **Run and test**:
   ```bash
   ./_output/net6.0/osx-arm64/publish/Readarr --nobrowser
   # Open http://localhost:7337
   # Check: Navy theme, "Papers" terminology, indexers available
   ```

### Deployment Checklist

Before deploying to users:

- [ ] Run full build: `./build.sh --all`
- [ ] Test on target platform
- [ ] Verify all 8 indexers appear in Settings > Indexers
- [ ] Test adding a researcher (author)
- [ ] Test paper search and retrieval
- [ ] Check logs for errors
- [ ] Package using: `./build.sh --packages`

---

**Note**: The solution file is `src/Readarr.sln`. Do not rename it to avoid breaking project references.

---

## 📥 Updates (Latest Work)

### **Session 2025-11-21 (Paper Addition Bug Fixes & DOI Deduplication)**

#### **Problem Statement**

Users reported that adding papers was failing, specifically papers like "The structure of DNA". Investigation revealed two critical bugs:

1. **Journal/Source Addition Failure**: `OpenAlexProxy.GetAuthorInfo` treated all IDs as Author IDs, failing for Journal IDs (starting with 'S')
2. **Author Metadata Matching Bug**: `AddSkyhookData` in `AddBookService` was matching author metadata using `ForeignBookId` instead of `ForeignAuthorId`, causing NullReferenceException for papers with multiple authors
3. **Duplicate Paper Handling**: No deduplication mechanism existed - papers could be added multiple times via different routes (e.g., via different authors or journals)

#### **What Was Implemented**

**1. Fixed Journal/Source Addition (`OpenAlexProxy.cs`)**

- Modified `GetAuthorInfo` to detect Source IDs (starting with 'S')
- Implemented `GetSourceInfo` method to fetch from `sources/{id}` endpoint
- Updated works fetching to use `filter=primary_location.source.id:{id}` for Sources
- **Test Coverage**: Created `OpenAlexProxyFixture.cs` with tests for both Author IDs and Source IDs
- **Status**: ✅ Tests pass, journals can now be added via UI (verified with "Nature")

**2. Fixed Author Metadata Matching Bug (`AddBookService.cs`)**

- Changed `AddSkyhookData` to match author metadata using `newBook.AuthorMetadata.Value.ForeignAuthorId` instead of `tuple.Item1` (which was the book ID)
- Added fallback to use first available author metadata if exact match not found
- Added logging for when author metadata doesn't match
- **Test Coverage**: Created `AddBookServiceFixture.cs` with test `should_add_book_when_ids_mismatch`
- **Status**: ✅ Tests pass

**3. Implemented DOI-Based Deduplication**
Created a multi-layered lookup system in the database:

- **`EditionRepository.cs`**: Added `FindByIsbn13(string isbn13)` - since DOI is stored in `Edition.Isbn13`
- **`EditionService.cs`**: Added `FindByIsbn13(string isbn13)` wrapper
- **`BookService.cs`**: Added `FindBookByIsbn13(string isbn13)` - finds edition by DOI, returns parent book
- **`AddBookService.cs`**: Modified `AddBook` to check for existing books via:
  1. First: `FindById(ForeignBookId)` - lookup by OpenAlex Work ID
  2. Then: `FindBookByIsbn13(doi)` - lookup by DOI if Work ID doesn't match
  3. If found: uses existing book via `UseDbFieldsFrom(dbBook)` to prevent duplicates
- **Test Coverage**: `AddBookServiceFixture.cs` tests both deduplication paths
- **Status**: ✅ Tests pass

#### **Critical Bug Discovered (NOT YET FIXED)**

While testing, discovered a **third related bug** in `BookInfoProxy.cs:268`:

```
NzbDrone.Core.MetadataSource.BookInfo.BookInfoException:
Expected author metadata for id [W2127674396] in book data [W2127674396][THE STRUCTURE OF DNA]
at BookInfoProxy.AddDbIds(String authorId, Book book, Dictionary`2 authors)
```

**Root Cause**: `BookInfoProxy.AddDbIds` has the same bug as `AddBookService.AddSkyhookData` - it's comparing `authorId` parameter (which is the Work/Book ID) against `author.ForeignAuthorId` in the dictionary. This fails for papers because:

- The method is called with `authorId = foreignBookId` (the Work ID like "W2127674396")
- But the dictionary is keyed by actual Author IDs (like "A123...")
- For papers with proper author data, these IDs don't match, causing the exception

**Impact**: Papers cannot be added via the UI because `BookInfoProxy.GetBookInfo` throws exceptions before `AddBookService` even gets called.

#### **Files Modified**

- `src/NzbDrone.Core/MetadataSource/OpenAlex/OpenAlexProxy.cs` - Source ID detection, GetSourceInfo implementation
- `src/NzbDrone.Core/Books/Services/AddBookService.cs` - Author metadata matching fix, DOI deduplication
- `src/NzbDrone.Core/Books/Services/BookService.cs` - Added FindBookByIsbn13
- `src/NzbDrone.Core/Books/Services/EditionService.cs` - Added FindByIsbn13
- `src/NzbDrone.Core/Books/Repositories/EditionRepository.cs` - Added FindByIsbn13
- `src/NzbDrone.Core.Test/MetadataSource/OpenAlexProxyFixture.cs` - NEW test fixture
- `src/NzbDrone.Core.Test/Books/AddBookServiceFixture.cs` - NEW test fixture

#### **Test Results**

```bash
# OpenAlexProxyFixture - ✅ PASS
dotnet test --filter OpenAlexProxyFixture
# Result: Tests passed (Author ID and Source ID handling verified)

# AddBookServiceFixture - ✅ PASS (3/3)
DOTNET_ROLL_FORWARD=Major dotnet test --filter AddBookServiceFixture
# - should_use_existing_book_if_doi_matches ✅
# - should_add_new_book_if_doi_does_not_match ✅
# - should_add_book_when_ids_mismatch ✅
```

#### **Manual Verification Attempted**

- Started Researcharr on http://localhost:7337
- Attempted to add "The structure of DNA" via browser
- **Result**: ❌ Failed due to BookInfoProxy bug (see above)
- **Logs**: `/tmp/researcharr-devdata/logs/` shows BookInfoException

---

### **Previous Sessions**

- **Journal search (OpenAlex sources)**: Author lookup now also queries OpenAlex sources (journals) and returns them as containers with `disambiguation = "Journal"` (e.g., searching "Nature" shows journals first). Institutions from OpenAlex `last_known_institutions` map into author disambiguation for search display.
- **Works ingestion relaxations**: DOI accepted instead of ISBN for metadata filtering; works bypass metadata-profile filtering; clean titles set to satisfy DB; citations mapped to Ratings; journal name mapped into editions/book resource for UI.
- **Quality Profile hidden in add modals**: Add-author/add-paper options form hides Quality Profile (UI only; backend unchanged).
- **Runtime**: Researcharr currently running on http://localhost:7337

## ⚠️ Outstanding / Next Steps

### **IMMEDIATE (Critical Bug Fix Required)**

**Fix `BookInfoProxy.AddDbIds` Author Matching Logic**

- **File**: `src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs` (line ~268)
- **Issue**: Method receives `authorId` parameter that contains the Book/Work ID, but tries to match it against Author IDs in the dictionary
- **Solution**: Similar to `AddBookService` fix - extract actual author ID from book data instead of using the `authorId` parameter
- **Steps**:
  1. View `BookInfoProxy.cs` around line 268
  2. Identify where `authorId` is used in author lookup
  3. Replace with logic that extracts author ID from the book's author metadata
  4. Add fallback similar to AddBookService if author not found
  5. Test with "The structure of DNA" paper

### **After Critical Fix**

- **Test End-to-End Paper Addition**:

  - Add "The structure of DNA" via UI
  - Verify it's added successfully
  - Search for it again and verify DOI deduplication prevents double-add
  - Check database to confirm single record

- **Add flow behavior**: Still Readarr-like. Needs change so adding a paper monitors only that paper and auto-creates/attaches a journal container; consider UI distinction between journals vs researchers in search results.

- **Quality profiles**: Backend/API still present; only hidden in UI. Decide whether to remove further or leave hidden.

- **Publish friction**: Stop the running process before `dotnet publish` to avoid pdb lock errors.

## 🔧 Recent Commands

- UI: `npm run build`
- Publish: `dotnet publish src/NzbDrone.Console/Readarr.Console.csproj -c Debug -r osx-arm64 --self-contained -o _output/net6.0/osx-arm64/publish -f net6.0 -p:RunAnalyzersDuringBuild=false`
- Run: `./_output/net6.0/osx-arm64/publish/Readarr --nobrowser --data=/tmp/researcharr-devdata`
- Test: `DOTNET_ROLL_FORWARD=Major dotnet test src/NzbDrone.Core.Test/Readarr.Core.Test.csproj --filter AddBookServiceFixture -p:RunAnalyzersDuringBuild=false`

## Session 2025-11-22 (UI Discrepancies Fixed)

- **Issue**: Persistent red theme and "Quality Profile" field despite code changes.
- **Root Cause**: Application was running in Debug mode, serving assets from `_output/net6.0/osx-arm64/UI` instead of the updated `publish/UI`.
- **Fix**:
  - Identified correct asset directory via `IndexHtmlMapper.cs`.
  - Manually deployed updated assets (Navy theme, no Quality Profile) to `_output/net6.0/osx-arm64/UI`.
  - Verified fix with browser agent (Navy header confirmed).
- **Prevention**: Added "UI Development & Debug Mode" section to this document.
