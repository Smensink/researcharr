# Researcharr

Researcharr is a fork of Readarr refocused on academic papers and DOIs. It fetches metadata from multiple scholarly sources, searches research-first indexers, and automates grabs/renames via the familiar Servarr workflow.

## What’s different
- Metadata aggregator that merges CrossRef (authoritative DOI registry) with OpenAlex (concepts, institutions, citations).
- Research-friendly indexers: LibGen (known good mirrors), Unpaywall, ArXiv, Sci-Hub DOI search, PubMed Central.
- DOI-safe matching: download decisions backfill author/title only when DOI matches.
- Path safety fixes: long multi-author filenames are truncated to avoid filesystem errors.
- BibTeX export endpoint for any paper: `/api/v1/book/{id}/export/bibtex`.

## Quick start
- Backend: `dotnet build src/Readarr.sln` (or `./build.sh` for full pipeline).
- Frontend dev: `yarn install --frozen-lockfile && yarn start` from `frontend/`.
- Run host: `dotnet run --project src/NzbDrone.Host/Readarr.Host.csproj --framework net6.0` (defaults to port 7337).
- Docker: build with `-p:RunAnalyzers=false` if StyleCop nags during publish.

## Usage notes
- Preferred LibGen mirrors: `libgen.li`, `libgen.vg`, `libgen.la`, `libgen.bz`, `libgen.gl`.
- DOI lookups fetch merged metadata; title/author backfill is gated by DOI equality to avoid false positives.
- PubMed Central requests use XML and free full-text filters; Unpaywall authors are parsed from `raw_author_name`.

## Roadmap (near-term)
- Expand metadata merge (better edition-level enrichment, citations).
- Additional academic sources (e.g., Semantic Scholar) and tests for indexer parsing.
- Optional Calibre-style tagging for imported papers.

## License
- GNU GPL v3 (inherits from upstream Readarr).
