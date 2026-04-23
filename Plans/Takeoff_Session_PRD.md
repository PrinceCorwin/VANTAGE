# AI Takeoff — Survive Tab Navigation (PRD)

**Status:** Approved, deferred — return after critical bug triage
**Priority:** Medium-High (UX-critical for long-running takeoffs)
**Source plan (detailed):** `C:\Users\Steve.Amalfitano\.claude\plans\write-up-a-plan-memoized-dolphin.md`

## Problem

When a user starts a batch on the Takeoffs tab and navigates to any other tab, the takeoff *appears* to be lost. `MainWindow.BtnTakeoff_Click` destroys and recreates the `TakeoffView` instance on each navigation, so:

- The status panel resets to "Loaded N config(s)…" (initial state).
- Batch name, selected files, and config selection all clear.
- The polling loop technically continues in the background (rooted by the orphaned async state machine) but writes to a UI element the user can no longer see.
- A future `SaveFileDialog` may pop from the orphaned view, surprising the user.
- The AWS Step Functions / Lambda execution is *not* cancelled — it runs to completion regardless.

There is also no app-close guard for an active batch, no cross-tab signal that a takeoff is in flight, and no way to know a long-running batch finished if the user is elsewhere when it completes.

## Goal

Lift takeoff lifecycle out of `TakeoffView` into an app-level `TakeoffSession` singleton. Navigating away and returning preserves the elapsed counter, current status, and disabled-input state. A bottom-status-bar indicator shows takeoff state from any tab.

## Out of Scope

- Multiple concurrent takeoffs (still one at a time).
- Resuming a takeoff after app restart (session is in-memory only).
- Toast / popup notifications on completion (bottom-bar label is the cross-tab signal).
- Refactoring `TakeoffService` itself.

## Functional Requirements

1. **Survives tab navigation.** Starting a batch, navigating away, and returning shows the in-progress UI (current status, elapsed timer, disabled inputs, Cancel button visible) — not a reset view.
2. **Cancel works from any return.** Cancel on a returned-to view stops the Step Functions execution and the local polling loop.
3. **Auto-download on return.** If the batch completes while the user is on another tab, returning to Takeoffs immediately opens the `SaveFileDialog`. Cancelling the dialog falls back to the existing Previous Batches button as the recovery path.
4. **Cross-tab status indicator.** MainWindow bottom status bar shows `Takeoff: Not Running` / `Takeoff: Running` / `Takeoff: Complete` at all times. "Complete" is sticky for the app session once set. Cancellation does not set the sticky flag.
5. **App-close protection.** If a takeoff is running and the user attempts to close the app, the existing `LongRunningOps`-based close guard prompts the user.
6. **Single-session semantics.** Starting a new batch overwrites any prior session's pending download. The previous batch remains recoverable via Previous Batches.

## Design Decisions (user-confirmed)

| Decision | Choice | Rationale |
|---|---|---|
| State location | New `TakeoffSession` singleton on `App.CurrentTakeoff` | Mirrors `App.CurrentUser` pattern; lifts state above view lifecycle |
| Save dialog timing on return | Open `SaveFileDialog` directly, no inline confirm | User preference — minimal friction |
| Cross-tab indicator | Bottom-bar text label | Concise, persistent, non-intrusive |
| "Complete" stickiness | Sticky until app close | Confirms a takeoff happened; user can verify even after download |
| App-close guard | Wrap session run in `using (LongRunningOps.Begin())` | Reuses existing pattern |
| Navigation guard | None | User wants free navigation |

## Implementation Outline

Full plan in `.claude/plans/write-up-a-plan-memoized-dolphin.md`. Summary:

1. **New `Services/AI/TakeoffSession.cs`** — owns polling loop. Properties: `BatchId`, `ConfigKey`, `ConfigDisplayName`, `SubmittedFiles`, `RevBubbleOnly`, `StartedUtc`, `LastStatus`, `IsRunning`, `IsCompleted`, `CompletedSuccessfully`, `PendingDownloadBatchId`, `Elapsed`. Events: `StatusChanged`, `RunningChanged`, `Completed`. Methods: `Start()`, `CancelAsync()`, `ClearPendingDownload()`.
2. **`App.xaml.cs`** — add `CurrentTakeoff`, `HasCompletedTakeoffSinceStartup`, `CurrentTakeoffChanged` event, `SetCurrentTakeoff(session)` setter.
3. **`Views/TakeoffView.xaml.cs`** — shrink. `BtnProcess_Click` becomes ~30 lines (validate → create session → subscribe → start). Add `Loaded` restore-from-session logic, `Unloaded` unsubscribe. Auto-open `SaveFileDialog` on Load if `PendingDownloadBatchId` set.
4. **`MainWindow.xaml`** — add 5th column to status bar (lines 465-503) with `txtTakeoffStatus` TextBlock; renumber existing columns (spacer 2→3, LastSync 3→4).
5. **`MainWindow.xaml.cs`** — subscribe to `App.CurrentTakeoffChanged`; `RefreshTakeoffStatusLabel()` helper updates the bottom-bar text on `RunningChanged` / `Completed`.

## Acceptance Criteria

1. **Happy-path nav-and-return:** Start batch (5 drawings), switch to Schedule for 30s, return — status, elapsed, disabled inputs all intact. Wait for completion — `SaveFileDialog` appears, save, Excel opens.
2. **Completed-while-away:** Start batch (1 drawing), switch to Schedule. Bottom bar flips Running → Complete. Return to Takeoffs — `SaveFileDialog` opens immediately.
3. **Cancel after nav:** Start, navigate to Schedule, navigate back, Cancel — execution stops, no Excel produced.
4. **Bottom-bar state machine:** Startup → Not Running. Run → Running. Complete → Complete. Second run → Running → Complete. Cancel before any complete → Not Running.
5. **App-close guard:** Start batch, attempt to close app — existing `LongRunningOps` prompt fires.
6. **Build clean:** `dotnet build` with zero new warnings.

## Open Considerations (revisit at implementation time)

- Foreground color for the "Running" state — `AccentColor` vs. `StatusGreenBgBtn` brush.
- Whether to disable Previous Batches and Recalc Excel buttons while a batch is in flight (they each construct their own `TakeoffService` so should be safe; might confuse users to leave enabled).
- The 1-second `DispatcherTimer` driving elapsed-time refresh should stop on `Unloaded` to avoid leaks across nav cycles.
