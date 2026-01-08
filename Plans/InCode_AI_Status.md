# In-Code AI Implementation Status

## Last Updated: January 7, 2026

---

## Summary

| Component | Status | Progress |
|-----------|--------|----------|
| Shared Infrastructure | Not Started | 0% |
| Feature 1: Error Assistant | Not Started | 0% |
| Feature 2: Description Analysis | Not Started | 0% |
| Feature 3: Metadata Analysis | Not Started | 0% |
| Feature 4: MissedReason Assistant | Not Started | 0% |
| Feature 5: Schedule Analysis | Deferred | 0% |

---

## Shared Infrastructure

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| AnthropicApiKey in Credentials.json | Not Started | Add to gitignored credentials file |
| AiUsageLimits table | Not Started | Create in DatabaseSetup.cs |
| AiUsageLog table | Not Started | Create in DatabaseSetup.cs |
| ClaudeApiService | Not Started | Core API communication class |
| SendPromptAsync method | Not Started | One-shot prompts for task-specific assistants |
| Rate limiting logic | Not Started | Check limits before calls |
| Usage tracking logic | Not Started | Log all calls to AiUsageLog |
| Admin AI Limits dialog | Not Started | UI for managing per-user limits |

---

## Feature 1: AI Error Assistant

### Purpose
Transform cryptic error messages into plain English with fix steps.

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| TranslateErrorAsync method | Not Started | Add to AiAssistants class |
| ErrorDisplayService | Not Started | Central service for error dialogs |
| "AI Translate" button on error dialogs | Not Started | UI integration |
| Explanation display UI | Not Started | Show original + AI explanation |

---

## Feature 2: AI Description Analysis

### Purpose
Standardize inconsistent activity descriptions.

### Tier Progress

| Tier | Feature | Status |
|------|---------|--------|
| 1 | Single description cleanup | Not Started |
| 2 | Batch standardization | Not Started |
| 3 | Similarity detection | Not Started |
| 4 | Real-time duplicate warning | Not Started |

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| AnalyzeDescriptionsAsync method | Not Started | Add to AiAssistants class |
| DescriptionAbbreviations table | Not Started | Store abbreviation dictionary |
| UI trigger location | Undecided | Context menu? Toolbar? Column header? |
| Before/after review dialog | Not Started | User reviews suggestions before applying |

---

## Feature 3: Metadata Consistency Analysis

### Purpose
Flag inconsistent categorical values that fragment dashboard aggregations.

### Tier Progress

| Tier | Feature | Status |
|------|---------|--------|
| 1 | Frequency report (no AI) | Not Started |
| 2 | Pattern matching (no AI) | Not Started |
| 3 | AI-enhanced analysis | Not Started |
| 4 | Auto-suggest on entry | Not Started |

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| ValidateMetadataAsync method | Not Started | Add to AiAssistants class |
| MetadataDecisions table | Not Started | Store user decisions, sync to Azure |
| Analysis UI | Not Started | Button trigger, badge on sync |
| Decision workflow UI | Not Started | Valid / Standardize To / Ignore |

---

## Feature 4: AI MissedReason Assistant

### Purpose
Standardize MissedStartReason/MissedFinishReason fields with details.

### Tier Progress

| Tier | Feature | Status |
|------|---------|--------|
| 1 | Category dropdown (no AI) | Not Started |
| 2 | Greyed suggestion on focus | Not Started |
| 3 | Standardization dialog | Not Started |
| 4 | Bulk analysis | Not Started |
| 5 | Historical lookup | Not Started |

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| SuggestMissedReasonAsync method | Not Started | Add to AiAssistants class |
| MissedReasonPatterns table | Not Started | Store historical patterns |
| Category dropdown UI | Not Started | Non-AI tier 1 |
| Greyed suggestion UI | Not Started | Tab to accept |
| Standardization dialog | Not Started | Optional detail prompts |
| Bulk analysis UI | Not Started | Grid-wide review |

---

## Feature 5: AI Schedule Analysis

### Purpose
Flag relationship violations, sequence logic errors, progress anomalies.

### Status: Deferred

Waiting for:
- Features 1-4 proven
- ScheduleRelationships table (P6 import)
- ConstructionRules table

### Preliminary Scope

| Check | Status |
|-------|--------|
| Predecessor incomplete but successor started | Deferred |
| Out-of-sequence work (painting before welding) | Deferred |
| Anomaly detection (0% to 100% in one day) | Deferred |
| Critical path activities behind schedule | Deferred |

---

## Known Issues

*None currently*

---

## Notes & Decisions

| Date | Decision |
|------|----------|
| 2026-01-07 | API key stored in gitignored Credentials.json |
| 2026-01-07 | Usage limits stored in separate table, not Users table |
| 2026-01-07 | All AI features are opt-in via button click, never automatic |
| 2026-01-07 | Feature 5 (Schedule Analysis) deferred until infrastructure proven |

---

## Files to Create

| File | Status | Notes |
|------|--------|-------|
| `Services/ClaudeApiService.cs` | Not Started | Core API communication |
| `Services/AiAssistants.cs` | Not Started | Task-specific assistant methods |
| `Models/AiUsageLimit.cs` | Not Started | Usage limit model |
| `Dialogs/AdminAiLimitsDialog.xaml` | Not Started | Admin UI for limits |
