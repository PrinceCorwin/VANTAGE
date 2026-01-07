# MILESTONE AI Implementation Items

**Last Updated:** January 1, 2026
**Status:** Planning Phase

---

## Shared Infrastructure

**ClaudeApiService** - Central service for all Claude API calls
- HTTP client wrapper for Anthropic API
- API key in Credentials.cs (gitignored)
- Handles timeouts, retries, offline detection
- Build this first - all features depend on it

---

## Feature 1: AI Error Assistant

**Purpose:** Transform cryptic error messages into plain English with fix steps.

**Approach:** Add "AI Translate" button to any error dialog. User clicks only when they need help understanding. Shows original message + AI explanation side by side.

**Key Decisions:**
- Button-on-demand, not auto-translate (controls cost, no delay)
- Works on any error, not just unhandled ones
- Requires centralizing errors through ErrorDisplayService

**Open Questions:**
- How many MessageBox.Show calls need routing to central service?
- Expand-in-place or new dialog for AI explanation?

---

## Feature 2: AI Description Analysis

**Purpose:** Standardize inconsistent activity descriptions. Critical foundation for REQit material requisition migration.

**Tiers:**
1. Single description cleanup (one string in, one out)
2. Batch standardization (select multiple rows, review before/after)
3. Similarity detection (group likely duplicates)
4. Real-time duplicate warning (on entry)

**Key Decisions:**
- Store abbreviation dictionary in DescriptionAbbreviations table
- User reviews suggestions before applying
- Start with Tier 1, expand as proven

**Open Questions:**
- Where in UI? Context menu? Toolbar? Column header icon?
- Which columns beyond Description?

---

## Feature 3: Metadata Consistency Analysis

**Purpose:** Flag inconsistent categorical values (ROCStep, CompType, PhaseCategory, PhaseCode, UDF18) that fragment dashboard aggregations.

**Approach:** AI analyzes patterns, groups similar values, flags outliers. User decides: Valid, Standardize To, or Ignore. Decisions sync to Azure by ProjectID.

**Tiers:**
1. Frequency report (no AI, just GROUP BY)
2. Pattern matching (fuzzy string grouping)
3. AI-enhanced analysis (typo detection, pattern recognition)
4. Auto-suggest on entry

**Key Decisions:**
- Flag, don't block - users can create new values as needed
- MetadataDecisions table syncs to Azure, scoped by ProjectID
- Trigger: Button click primary, lightweight badge on sync/submit (non-blocking)

**Open Questions:**
- Threshold for flagging outliers?
- Admin lock on decisions?

---

## Feature 4: AI MissedReason Assistant

**Purpose:** Standardize MissedStartReason/MissedFinishReason fields. Prompt for missing details (ETA, PO#, predecessor status) so schedulers don't chase down info.

**Standard Format:** `[CATEGORY]: [Details] - [Resolution/ETA if applicable]`

**Categories:** MATERIAL, PREDECESSOR, WEATHER, MANPOWER, ENGINEERING, CLIENT, EQUIPMENT, ACCESS, SAFETY, QC/REWORK, CHANGE ORDER, OTHER

**Triggers:**
1. Greyed suggestion on focus (Tab to accept, continue editing)
2. AI button in cell for standardization dialog
3. Bulk "Analyze All MissedReasons" for grid-wide review

**Tiers:**
1. Category dropdown (no AI)
2. Greyed suggestion on focus
3. Standardization dialog with optional detail prompts
4. Bulk analysis
5. Historical lookup from ProgressSnapshots

**Key Decisions:**
- Free-form always allowed (categories are suggestions)
- Detail prompts are optional (user provides what they know)
- Trust user's accepted text
- Historical patterns stored in MissedReasonPatterns table

**Open Questions:**
- Cache suggestions per SchedActNO?
- Pattern extraction frequency?

---

## Feature 5: AI Schedule Analysis

**Purpose:** Flag relationship violations, sequence logic errors, progress anomalies, and schedule risks.

**Status:** Deferred until Features 1-4 proven.

**Prerequisites:**
- Import P6 relationships (ScheduleRelationships table)
- Construction rules engine (ConstructionRules table)
- Proven AI infrastructure from simpler features

**Preliminary Scope:**
- Predecessor not complete but successor started
- Painting complete before welding (implicit sequence logic)
- 0% to 100% in one day (anomaly detection)
- Critical path activities behind schedule

---

## Implementation Roadmap

| Phase | Features |
|-------|----------|
| 1 | ClaudeApiService infrastructure |
| 2 | AI Error Assistant |
| 3 | Description Analysis Tiers 1-2 |
| 4 | Metadata Analysis Tiers 1-2 |
| 5 | MissedReason Assistant Tiers 1-3 |
| 6 | Expand all features to higher tiers as needed |
| 7 | Schedule Analysis (after prerequisites) |

---

## Cost Estimates

| Feature | Per-Use Cost |
|---------|--------------|
| Error translation | $0.001-0.003 |
| Description cleanup (single) | $0.001-0.002 |
| Description batch (50 items) | $0.01-0.02 |
| Metadata analysis (500 values) | $0.02-0.05 |
| MissedReason suggestion | $0.001-0.002 |
| MissedReason bulk (50 items) | $0.02-0.05 |

Expected daily cost at full usage: $0.20-0.50/day

---

**END OF DOCUMENT**
