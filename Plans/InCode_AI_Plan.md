# In-Code AI Implementation Plan

## Overview

Task-specific AI features triggered by user actions or runtime events, not through the conversational AI Assistant. These are single-purpose AI calls invoked by buttons or event handlers.

---

## Shared Infrastructure

### ClaudeApiService

Central service for all Claude API calls (shared with Sidebar AI Assistant).

Location: `Services/ClaudeApiService.cs`

```
+-------------------------------------------------------------+
|                    ClaudeApiService                         |
|  - API key management (from Credentials.json)               |
|  - Token usage tracking                                     |
|  - Rate limiting per user                                   |
|  - Error handling and retries                               |
|                                                             |
|  Methods:                                                   |
|  +-- SendPromptAsync(prompt) -> string                      |
|  |   One-shot, no history, for task-specific assistants     |
|  |                                                          |
|  +-- SendChatAsync(messages, context) -> ChatResponse       |
|  |   Conversation with history, for sidebar chatbot         |
|  |                                                          |
|  +-- SendWithToolsAsync(messages, tools) -> ToolResponse    |
|      Conversation with tool calls, for data modifications   |
+-------------------------------------------------------------+
```

**Build this first** - all AI features depend on it.

---

## Database Tables

### AiUsageLimits

```sql
CREATE TABLE AiUsageLimits (
    UserID INTEGER PRIMARY KEY,
    DailyTokenLimit INTEGER DEFAULT 100000,
    DailyTokensUsed INTEGER DEFAULT 0,
    LastResetDate TEXT,
    RequestsPerHour INTEGER DEFAULT 30,
    RequestsThisHour INTEGER DEFAULT 0,
    HourStartedAt TEXT,
    IsEnabled INTEGER DEFAULT 1,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
```

### AiUsageLog

```sql
CREATE TABLE AiUsageLog (
    LogID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID INTEGER,
    Timestamp TEXT,
    RequestType TEXT,
    InputTokens INTEGER,
    OutputTokens INTEGER,
    TotalTokens INTEGER,
    Success INTEGER,
    ErrorMessage TEXT,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
```

### Credentials.json Addition

```json
{
    "AzureConnectionString": "...",
    "AnthropicApiKey": "sk-ant-..."
}
```

---

## Feature 1: AI Error Assistant

**Purpose:** Transform cryptic error messages into plain English with fix steps.

### Approach
- Add "AI Translate" button to error dialogs
- User clicks only when they need help understanding
- Shows original message + AI explanation side by side

### Key Decisions
- Button-on-demand, not auto-translate (controls cost, no delay)
- Works on any error, not just unhandled ones
- Requires centralizing errors through ErrorDisplayService

### Open Questions
- How many MessageBox.Show calls need routing to central service?
- Expand-in-place or new dialog for AI explanation?

### Implementation
- Method: `AiAssistants.TranslateErrorAsync(string errorMessage)`
- Returns: Plain English explanation + suggested fix steps
- Cost estimate: $0.001-0.003 per use

---

## Feature 2: AI Description Analysis

**Purpose:** Standardize inconsistent activity descriptions. Critical foundation for REQit material requisition migration.

### Tiers

| Tier | Feature | AI Required |
|------|---------|-------------|
| 1 | Single description cleanup | Yes |
| 2 | Batch standardization (select multiple, review before/after) | Yes |
| 3 | Similarity detection (group likely duplicates) | Yes |
| 4 | Real-time duplicate warning on entry | Yes |

### Key Decisions
- Store abbreviation dictionary in DescriptionAbbreviations table
- User reviews suggestions before applying
- Start with Tier 1, expand as proven

### Open Questions
- Where in UI? Context menu? Toolbar? Column header icon?
- Which columns beyond Description?

### Implementation
- Method: `AiAssistants.AnalyzeDescriptionsAsync(List<string> descriptions)`
- Returns: Standardized versions with before/after comparison
- Cost estimate: $0.001-0.002 (single), $0.01-0.02 (batch of 50)

---

## Feature 3: Metadata Consistency Analysis

**Purpose:** Flag inconsistent categorical values (ROCStep, CompType, PhaseCategory, PhaseCode, UDF18) that fragment dashboard aggregations.

### Approach
- AI analyzes patterns, groups similar values, flags outliers
- User decides: Valid, Standardize To, or Ignore
- Decisions sync to Azure by ProjectID

### Tiers

| Tier | Feature | AI Required |
|------|---------|-------------|
| 1 | Frequency report (GROUP BY) | No |
| 2 | Pattern matching (fuzzy string grouping) | No |
| 3 | AI-enhanced analysis (typo detection, pattern recognition) | Yes |
| 4 | Auto-suggest on entry | Yes |

### Key Decisions
- Flag, don't block - users can create new values as needed
- MetadataDecisions table syncs to Azure, scoped by ProjectID
- Trigger: Button click primary, lightweight badge on sync/submit (non-blocking)

### Open Questions
- Threshold for flagging outliers?
- Admin lock on decisions?

### Implementation
- Method: `AiAssistants.ValidateMetadataAsync(string fieldName, List<string> values)`
- Returns: Grouped values with suggestions
- Cost estimate: $0.02-0.05 (500 values)

---

## Feature 4: AI MissedReason Assistant

**Purpose:** Standardize MissedStartReason/MissedFinishReason fields. Prompt for missing details (ETA, PO#, predecessor status) so schedulers don't chase down info.

### Standard Format
`[CATEGORY]: [Details] - [Resolution/ETA if applicable]`

### Categories
MATERIAL, PREDECESSOR, WEATHER, MANPOWER, ENGINEERING, CLIENT, EQUIPMENT, ACCESS, SAFETY, QC/REWORK, CHANGE ORDER, OTHER

### Triggers

| Trigger | Behavior |
|---------|----------|
| Focus on field | Greyed suggestion appears (Tab to accept, continue editing) |
| AI button in cell | Standardization dialog with optional detail prompts |
| Bulk "Analyze All" | Grid-wide review of all missed reasons |

### Tiers

| Tier | Feature | AI Required |
|------|---------|-------------|
| 1 | Category dropdown (no AI) | No |
| 2 | Greyed suggestion on focus | Yes |
| 3 | Standardization dialog with detail prompts | Yes |
| 4 | Bulk analysis | Yes |
| 5 | Historical lookup from ProgressSnapshots | Yes |

### Key Decisions
- Free-form always allowed (categories are suggestions)
- Detail prompts are optional (user provides what they know)
- Trust user's accepted text
- Historical patterns stored in MissedReasonPatterns table

### Open Questions
- Cache suggestions per SchedActNO?
- Pattern extraction frequency?

### Implementation
- Method: `AiAssistants.SuggestMissedReasonAsync(string activityContext, string currentText)`
- Returns: Standardized suggestion with category
- Cost estimate: $0.001-0.002 (single), $0.02-0.05 (batch of 50)

---

## Feature 5: AI Schedule Analysis

**Purpose:** Flag relationship violations, sequence logic errors, progress anomalies, and schedule risks.

**Status:** Deferred until Features 1-4 proven.

### Prerequisites
- Import P6 relationships (ScheduleRelationships table)
- Construction rules engine (ConstructionRules table)
- Proven AI infrastructure from simpler features

### Preliminary Scope
- Predecessor not complete but successor started
- Painting complete before welding (implicit sequence logic)
- 0% to 100% in one day (anomaly detection)
- Critical path activities behind schedule

### Implementation
- Method: `AiAssistants.AnalyzeScheduleAsync(ScheduleContext context)`
- Returns: List of flagged issues with severity and suggestions
- Cost estimate: TBD based on context size

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

## Files to Create

| File | Purpose |
|------|---------|
| `Services/ClaudeApiService.cs` | Core API communication |
| `Services/AiAssistants.cs` | Task-specific assistant methods |
| `Models/AiUsageLimit.cs` | Usage limit model |
| `Dialogs/AdminAiLimitsDialog.xaml` | Admin UI for limits |

---

## Cost Summary

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

## Dependencies

```xml
<PackageReference Include="Anthropic.SDK" Version="..." />
```

Or direct HTTP if SDK is problematic:
```xml
<PackageReference Include="System.Net.Http.Json" Version="..." />
```

---

## Notes

- AI never has direct database access - all operations go through existing repositories
- Usage tracking persists for billing/monitoring purposes
- Task-specific assistants don't count against rate limits as heavily (configurable)
- All AI features are opt-in via button click, never automatic
