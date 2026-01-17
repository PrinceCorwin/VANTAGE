# User Access Request Feature Plan

**Date:** January 16, 2026
**Status:** Complete

---

## Summary

When a user is not found in the system at startup, provide a "Request Access" option that allows them to submit their information via email to all administrators.

---

## Current Behavior

- App gets Windows username via `Environment.UserName`
- Queries local SQLite `Users` table (mirrored from Azure)
- If not found → Shows MessageBox "Access Denied" → App shuts down
- No way for user to request access from within the app

---

## New Behavior

1. Keep "Access Denied" message with "contact administrator" text
2. Add "Request Access" button to the dialog
3. Button opens dialog with:
   - Username (auto-populated, read-only/greyed out)
   - Full Name (text input, required)
   - Email (text input, required)
   - Send / Cancel buttons
4. On Send: Email all admins using existing EmailService (Azure Communication Services)
5. Show confirmation message, then app shuts down

---

## Implementation

### 1. Create AccessRequestDialog

**Files:** `Dialogs/AccessRequestDialog.xaml`, `Dialogs/AccessRequestDialog.xaml.cs`

Simple dialog with:
- TextBlock showing Windows username (greyed out)
- TextBox for Full Name (required)
- TextBox for Email (required, basic validation)
- Send / Cancel buttons
- Returns user info on success

### 2. Create SendAccessRequestEmail Method

**File:** `Services/EmailService.cs`

Add method `SendAccessRequestEmailAsync(string username, string fullName, string email)`:
- Query Azure Admins table to get all admin emails
- Build email body with user's request details
- Use existing Azure Communication Services to send
- Return success/failure

### 3. Modify Access Denied Flow

**File:** `App.xaml.cs`

Replace simple MessageBox with custom dialog or MessageBox with custom buttons:
- Show access denied message
- Add "Request Access" button alongside "OK"
- If Request Access clicked:
  - Show AccessRequestDialog
  - On submit, call SendAccessRequestEmailAsync
  - Show success/failure message
- App shuts down after either path

### 4. Handle Offline Scenario

If Azure is unavailable when user clicks "Request Access":
- Show message that request cannot be sent while offline
- Suggest trying again later or contacting admin directly

---

## Files to Modify/Create

| File | Action |
|------|--------|
| `Dialogs/AccessRequestDialog.xaml` | Create |
| `Dialogs/AccessRequestDialog.xaml.cs` | Create |
| `Services/EmailService.cs` | Add SendAccessRequestEmailAsync method |
| `App.xaml.cs` | Modify user-not-found handling |

---

## Email Content Template

```
Subject: MILESTONE Access Request - {Username}

A user is requesting access to MILESTONE:

Windows Username: {Username}
Full Name: {FullName}
Email: {Email}

Please add this user to the Users table in MILESTONE Admin if approved.
```

---

## Verification

1. Remove yourself from local Users table (or use test username)
2. Launch app - should see Access Denied with Request Access button
3. Click Request Access - dialog should appear with username pre-filled
4. Enter Full Name and Email, click Send
5. Verify email received by admins
6. Test Cancel button - should close dialog, app shuts down
7. Test offline scenario - should show appropriate message
