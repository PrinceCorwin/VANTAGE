---
name: speedup
description: >
  Review the project status file and summarize immediate todo items.
  Trigger when the user says "speedup" or "/speedup".
---

# Speedup

Quick status briefing that reads the project backlog and presents the most
actionable items.

## Instructions

1. Read `Plans/Project_Status.md`.
2. Identify and present:
   - Anything in the **In Progress** table awaiting action
   - Items in **Active Development** (current focus)
   - The first 3-5 items in **Feature Backlog → High Priority**
   - Any items in **Open Risks / Blockers** that look load-bearing right now
3. Present the items in a concise, scannable format.
4. If any items look quick to knock out (single script, doc-only edit, etc.),
   call those out as "Quick Wins".
5. Keep the response brief — this is meant to orient the user fast, not give
   exhaustive detail. The user can read `Plans/Project_Status.md` directly
   for full context.

## Output Format

Use a format like:

**Immediate Items:**
- Item 1 — one-line context
- Item 2 — one-line context
- Item 3 — one-line context

**Quick Wins:** (if any obvious ones exist)
- Small task that could be done in one session

**Blocked / Needs Attention:** (if any)
- Item awaiting external input or user decision

If a section is empty, omit it entirely rather than printing "(none)".
