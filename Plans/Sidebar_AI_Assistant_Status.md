# Sidebar AI Assistant Tab Status

## Last Updated: January 7, 2026

---

## Summary

| Phase | Status | Progress |
|-------|--------|----------|
| Chat UI | Not Started | 0% |
| Conversation Management | Not Started | 0% |
| Tool Definitions | Not Started | 0% |
| Tool Execution | Not Started | 0% |

**Prerequisite:** Requires `ClaudeApiService` from In-Code AI infrastructure (not started).

---

## Phase 1: Chat UI

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| ChatMessage model | Not Started | Conversation history model |
| Update SidePanelView chat UI | Not Started | Replace placeholder with real chat interface |
| Message input field | Not Started | Text input at bottom |
| Message history display | Not Started | Scrollable, styled messages |
| Loading indicator | Not Started | Show during API calls |
| Error state display | Not Started | Handle API failures |

---

## Phase 2: Conversation Management

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| SidePanelViewModel chat logic | Not Started | Message handling, API calls |
| AiContextProvider | Not Started | Gather current module, filters, selection |
| SendChatAsync method | Not Started | Add to ClaudeApiService |
| Conversation history (session) | Not Started | Maintain context during session |
| System prompt construction | Not Started | Include current context |

---

## Phase 3: Tool Definitions

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| AiToolDefinitions class | Not Started | JSON schemas for all tools |
| AiToolCall model | Not Started | Tool request/response models |
| search_activities schema | Not Started | Query activities |
| update_activities schema | Not Started | Batch updates |
| apply_filter schema | Not Started | Set view filters |
| get_schedule_status schema | Not Started | Schedule queries |

---

## Phase 4: Tool Execution

### Remaining Items

| Item | Status | Notes |
|------|--------|-------|
| SendWithToolsAsync method | Not Started | Add to ClaudeApiService |
| AiActionExecutor | Not Started | Execute confirmed actions |
| AiConfirmationDialog | Not Started | Scrollable preview UI |
| search_activities implementation | Not Started | Query and return results |
| update_activities implementation | Not Started | Batch update with confirmation |
| apply_filter implementation | Not Started | Apply to current view |
| get_schedule_status implementation | Not Started | Return schedule data |

---

## Known Issues

*None currently*

---

## Notes & Decisions

| Date | Decision |
|------|----------|
| 2026-01-07 | AI modifications limited to current user's records only |
| 2026-01-07 | AI batch operations require user confirmation with scrollable preview |
| 2026-01-07 | Continue on error for batch operations, report failures |
| 2026-01-07 | Conversation history is session-only (not persisted) |
| 2026-01-07 | Read-only tools execute immediately; write tools require confirmation |

---

## Files to Create

| File | Status | Notes |
|------|--------|-------|
| `Services/AiContextProvider.cs` | Not Started | Gather current app context |
| `Services/AiActionExecutor.cs` | Not Started | Execute confirmed actions |
| `Services/AiToolDefinitions.cs` | Not Started | Tool JSON schemas |
| `Models/ChatMessage.cs` | Not Started | Conversation message model |
| `Models/AiToolCall.cs` | Not Started | Tool call/response models |
| `Dialogs/AiConfirmationDialog.xaml` | Not Started | Batch confirmation UI |
