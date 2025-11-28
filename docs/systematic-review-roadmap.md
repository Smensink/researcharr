# Systematic Review Feature Roadmap

This document lists the work needed to support systematic reviews end-to-end. Use the checkboxes to track progress and spawn issues as needed.

## Goals
- Unified search across scholarly sources with reproducible strategies.
- Clean, de-duplicated corpus with normalized metadata.
- Structured screening, full-text retrieval, evidence extraction, quality assessment, and exports.
- Collaboration, auditability, and PRISMA-friendly reporting.

## Phase 0 – Foundations
- [ ] Define schemas for `SearchStrategy`, `SourceResult`, `Study`, `ScreeningDecision`, `ExtractionForm`, `RiskOfBiasAssessment`, and audit events.
- [ ] Add background job + queue pattern for long-running searches; include progress + cancellation hooks.
- [ ] Introduce a pluggable “source” interface: `search(query, options) -> normalized records + cursor`.
- [ ] Set up feature flags for incremental rollout; add metrics (latency, success rate per source).

## Phase 1 – Search Ingestion (Sources)
- [ ] PubMed: REST integration (esearch/efetch), supports date range, article type, language, MeSH expansion toggle.
- [ ] Crossref/OpenAlex: DOI-first normalization; include funding + OA status.
- [ ] arXiv: category + date filters; map arXiv IDs to DOI when available.
- [ ] Google Scholar/Web of Science/Embase/Cochrane: add “upload RIS/CSV” importer + manual run log (avoid scraping); allow API credentials if institutional access exists.
- [ ] Query builder UI: PICO-aware fields, Boolean groups, saved strategies, per-source overrides (date, type, language).
- [ ] Store reproducible search strings per source; log run metadata (timestamp, filters, counts).

## Phase 2 – Normalization & Deduplication
- [ ] Normalize records to unified schema (title, authors, journal, year, DOI/PMID/PMCID/ISBN, abstract, keywords/MeSH/Emtree, OA status).
- [ ] Deterministic dedup: DOI/PMID/PMCID/ISBN keys; fall back to title+year+first author.
- [ ] Fuzzy dedup: normalized title similarity with thresholds; manual merge/review queue for conflicts.
- [ ] Surface provenance: which sources contributed which fields; keep source-specific IDs.

## Phase 3 – Screening Workflow
- [ ] Title/abstract screening UI with keyboard shortcuts; include inclusion/exclusion reasons.
- [ ] Dual-reviewer assignment, blinding, and conflict resolution flow; compute inter-rater reliability (Cohen’s kappa).
- [ ] PRISMA counts auto-tracking (identified, screened, excluded, included); export diagram later.
- [ ] Filters: study design, publication year, language, source, OA flag, tags.

## Phase 4 – Full-Text Retrieval
- [ ] Integrate Unpaywall/PMC/arXiv/OpenAlex for OA links; fallback to institutional resolver link-out.
- [ ] Allow manual PDF upload + versioning; store file hash and link to Study.
- [ ] Background fetcher with retry/backoff and per-source rate limits.
- [ ] Indicate availability status (found, paywalled, uploaded, failed).

## Phase 5 – Data Extraction & Evidence Tagging
- [ ] Configurable extraction forms (study design, sample size, arms, outcomes, effect sizes, adverse events); field types + validation.
- [ ] Inline PDF highlights mapped to structured fields (text span -> field provenance).
- [ ] Calculators for common effect sizes (RR/OR/MD/SMD) with unit checks.
- [ ] Export extracted data to CSV/JSON; version extraction entries and audit changes.

## Phase 6 – Quality/Risk of Bias
- [ ] Built-in checklists: Cochrane RoB, ROBINS-I, GRADE; templated questions per domain.
- [ ] Per-study scoring with justification notes; roll-up summary views.
- [ ] Reports summarizing risk distributions and sensitivity flags.

## Phase 7 – Alerts & Reruns
- [ ] Saved search reruns on schedule; store deltas (new/removed records).
- [ ] Notifications (email/webhook) for deltas and failed jobs.
- [ ] Change log per search strategy with parameter history.

## Phase 8 – Collaboration & Audit
- [ ] Roles/permissions for search, screening, extraction, quality review.
- [ ] Comments and mentions on studies and decisions.
- [ ] Full audit trail: who searched, decided, extracted, or edited; timestamps.

## Phase 9 – Exports & Reporting
- [ ] PRISMA flow diagram generation from tracked counts.
- [ ] Export formats: RIS/BibTeX/CSV/JSON; RevMan/DistillerSR/EPPI-compatible CSVs.
- [ ] API/webhook to deliver normalized records and decisions to notebooks or downstream systems.

## Operational Considerations
- [ ] Rate-limiters and polite defaults per source; configurable API keys where applicable.
- [ ] Backfill scripts for existing libraries: import RIS/CSV/EndNote exports.
- [ ] Health checks and dashboards: source uptime, dedup accuracy samples, screening throughput.
- [ ] Analytics: time-to-screen, completion per phase, conflicts rate, OA coverage.
