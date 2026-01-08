# Sidebar AI Assistant Tab Implementation Plan

## Overview

The AI Assistant tab in the sidebar provides a conversational interface for users to query data, request analysis, and perform batch operations on their assigned activities. Uses Claude API with tool definitions for structured actions.

**Prerequisite:** Requires `ClaudeApiService` from InCode_AI infrastructure.

---

## Architecture

### Chatbot Components

| Component | Purpose |
|-----------|---------|
| `ClaudeApiService` | Handles all API calls to Anthropic (shared with In-Code AI) |
| `AiContextProvider` | Gathers current state (module, filters, selected data) |
| `AiToolDefinitions` | Describes available actions to Claude (JSON schema) |
| `AiActionExecutor` | Executes confirmed actions on database |
| `AiConfirmationDialog` | Shows batch preview, user approves/rejects |
| `ChatMessage` | Model for conversation history |

---

## Data Flow

```
User types: "Complete all piping activities for TA105"
                    |
+-----------------------------------------------------------+
|  SidePanelView                                            |
|  - Captures user input                                    |
|  - Adds to conversation history                           |
|  - Calls SidePanelViewModel.SendMessageAsync()            |
+-----------------------------------------------------------+
                    |
+-----------------------------------------------------------+
|  SidePanelViewModel                                       |
|  - Gets context from AiContextProvider                    |
|  - Builds messages array with system prompt               |
|  - Calls ClaudeApiService.SendWithToolsAsync()            |
+-----------------------------------------------------------+
                    |
+-----------------------------------------------------------+
|  ClaudeApiService                                         |
|  - Checks rate limits                                     |
|  - Sends request to Anthropic API                         |
|  - Tracks token usage                                     |
|  - Returns response (text or tool call)                   |
+-----------------------------------------------------------+
                    |
+-----------------------------------------------------------+
|  If tool call returned:                                   |
|  - AiActionExecutor.PrepareActionAsync()                  |
|  - Enforces "current user only" constraint                |
|  - Queries affected records                               |
|  - Shows AiConfirmationDialog with scrollable preview     |
+-----------------------------------------------------------+
                    |
+-----------------------------------------------------------+
|  User clicks Confirm                                      |
|  - AiActionExecutor.ExecuteAsync()                        |
|  - Runs update, continues on error                        |
|  - Returns success/failure count                          |
|  - Chat shows result message                              |
+-----------------------------------------------------------+
```

---

## Security & Guardrails

### Hard Constraints (Enforced in Code)

| Rule | Enforcement |
|------|-------------|
| Current user records only | `AiActionExecutor` always adds `AssignedTo='{currentUser}'` to any modification query |
| User confirmation required | All write operations go through `AiConfirmationDialog` |
| Rate limiting | `ClaudeApiService` checks limits before each call |
| Token budgets | Configurable per-user in `AiUsageLimits` table |

### Soft Constraints (System Prompt)

Claude is instructed to:
- Only modify activities assigned to current user
- Always explain what it's about to do
- Ask for clarification if request is ambiguous
- Warn about large batch operations

**Note:** Soft constraints guide behavior but are not security. Hard constraints in code are the actual guardrails.

---

## Tool Definitions

### search_activities

```json
{
    "name": "search_activities",
    "description": "Search for activities matching criteria. Returns matching records.",
    "parameters": {
        "filter": "SQL-like filter expression (e.g., Module='TA105' AND CompType='Piping')",
        "fields": "Comma-separated fields to return",
        "limit": "Maximum records to return (default 100)"
    }
}
```

### update_activities

```json
{
    "name": "update_activities",
    "description": "Update activities matching criteria. Requires user confirmation.",
    "parameters": {
        "filter": "SQL-like filter expression",
        "field": "Field name to update",
        "value": "New value"
    }
}
```

### apply_filter

```json
{
    "name": "apply_filter",
    "description": "Apply filter to current view. Does not modify data.",
    "parameters": {
        "filter": "Filter expression to apply to current module view"
    }
}
```

### get_schedule_status

```json
{
    "name": "get_schedule_status",
    "description": "Get schedule status for activities, including missed reasons.",
    "parameters": {
        "filter": "Filter expression",
        "week_ending": "Optional week ending date"
    }
}
```

---

## System Prompt

```
You are an AI assistant integrated into MILESTONE, a construction project management application used by Field Engineers.

CURRENT CONTEXT:
- User: {username}
- Module: {current_module}
- Active Filters: {active_filters}
- Selected Records: {selected_count}

RULES:
1. You can only modify activities assigned to the current user ({username})
2. All data modifications require user confirmation - use appropriate tools
3. When asked to update records, always state how many will be affected
4. If a request is ambiguous, ask for clarification
5. For large batches (>50 records), warn the user before proceeding
6. Speak plainly - avoid technical jargon, these are construction professionals

AVAILABLE TOOLS:
{tool_definitions}

GUIDELINES:
- Be concise and direct
- When showing data, format it clearly
- If you can't do something, explain why
- Suggest alternatives when a request isn't possible
```

---

## UI Components

### Chat Interface

- Message input field at bottom
- Scrollable message history
- User messages right-aligned, AI responses left-aligned
- Loading indicator during API calls
- Error state display

### AiConfirmationDialog

- Title showing action type
- Scrollable preview of affected records
- Record count summary
- Confirm and Cancel buttons
- Warning for large batches

---

## Models

### ChatMessage

```csharp
public class ChatMessage
{
    public string Role { get; set; }      // "user" or "assistant"
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### AiToolCall

```csharp
public class AiToolCall
{
    public string ToolName { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
}

public class AiToolResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
}
```

---

## Build Order

1. Create `ChatMessage` model
2. Update `SidePanelView` with real chat interface (replace placeholder)
3. Update `SidePanelViewModel` with conversation management
4. Implement `AiContextProvider`
5. Add `SendChatAsync` to `ClaudeApiService`
6. Test conversation flow (text only, no tools)
7. Define tool JSON schemas (`AiToolDefinitions`)
8. Add `SendWithToolsAsync` to `ClaudeApiService`
9. Build `AiActionExecutor` with guardrails
10. Create `AiConfirmationDialog` with scrollable preview
11. Implement `search_activities` tool
12. Implement `update_activities` tool
13. Implement `apply_filter` tool
14. Implement `get_schedule_status` tool

---

## Cost Estimates

Based on Claude 3.5 Sonnet pricing ($3/1M input, $15/1M output):

| Operation | Est. Tokens | Est. Cost |
|-----------|-------------|-----------|
| Chat message (with context) | ~2,000 | ~$0.01 |
| Tool call + execution | ~3,000 | ~$0.015 |

---

## Files to Create

| File | Purpose |
|------|---------|
| `Services/AiContextProvider.cs` | Gather current app context |
| `Services/AiActionExecutor.cs` | Execute confirmed actions |
| `Services/AiToolDefinitions.cs` | Tool JSON schemas |
| `Models/ChatMessage.cs` | Conversation message model |
| `Models/AiToolCall.cs` | Tool call/response models |
| `Dialogs/AiConfirmationDialog.xaml` | Batch confirmation UI |

---

## Notes

- AI never has direct database access - all operations go through existing repositories
- Conversation history is session-only (not persisted to database)
- Tool execution always requires user confirmation for write operations
- Read-only operations (search, filter) execute immediately
