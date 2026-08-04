# Email Service Migration — Azure ACS → AWS SES

**Goal:** Move VANTAGE's transactional email off the current provider onto AWS SES, sending from a company-owned domain. Reusable by other in-house apps (REQit, etc.) on the same AWS account.

**Status (2026-08-04): INFRASTRUCTURE STOOD UP; CODE NOT SWAPPED.** The live app still sends via the old provider (`Utilities/EmailService.cs` untouched). The SES sending domain is registered and configured; remaining work is production access, a send-only user, and the code swap. Nothing here affects production email until the code swap ships.

---

## Why this migration
The current email service (Azure Communication Services) runs on **Steve's personal Azure account** — it was never on company infrastructure. A live, all-users production feature depending on a personal account is a single point of failure (billing lapse, lockout, personnel change) outside company control. AWS SES on a **company-owned domain** in the company AWS account (`430392373397`, same account already used for S3/tutorials/AI-Takeoff) removes that dependency.

## Decisions settled
- **Provider = AWS SES v2**, company AWS account `430392373397`, region `us-east-1` (same as all existing AWS work).
- **Sending domain = `summitapps.net`** (company-owned, neutral so multiple in-house apps can share it). NOT Vantage-branded, NOT tied to a corporate mail domain.
- **Sender address = `DoNotReply@summitapps.net`** (label before the `@` is free-form; confirm final label at swap time).
- **`summit.us` is the eventual target too, but deferred ~2 months.** IT (the internal admin) approved sending from `summit.us`, but final Comfort Systems blessing (Comfort acquired Summit; `summit.us` DNS is in Comfort-managed Azure DNS) is ~2 months out. Plan per IT: run on `summitapps.net` now, switch the sender to `@summit.us` once blessed. A parked SES identity for `summit.us` already exists so the switch is a config change.
- **Send-only IAM user** (`vantage-email-user`, scoped to `ses:SendEmail`/`ses:SendRawEmail`) rather than extending `vantage-takeoff-user` — that key ships on every client machine; keeping email perms separate contains the blast radius (a leaked key can't also send company email).

## AWS resources created (2026-08-04)
- **Domain `summitapps.net`** — registered via Route 53, company asset (registrant: Chris Aberly / caberly@summit.us, Summit Industrial), 1yr + auto-renew ON, WHOIS privacy ON. Route 53 hosted zone `Z01697882XFI70LHLYFEE`.
  - ⚠️ **ICANN verification pending** — Chris must click the "verify your email" message sent to caberly@summit.us within 15 days or the domain is suspended. Likely held in **Mimecast quarantine** (summit.us runs Mimecast). Resent 2026-08-04.
- **SES identity `summitapps.net`** (us-east-1), Easy DKIM (RSA 2048). Custom MAIL FROM = `mail.summitapps.net`.
  - DKIM tokens: `5rxnrs34slpewyn6brdtdkozpsdf4ihg`, `y7bqbbnc6jsztamawq6grbxf3fsr7zpz`, `vzce6vbexk53nyubzneruvspoigvh5gt`
  - DNS records written into the Route 53 zone: 3 DKIM CNAMEs; root SPF TXT `v=spf1 include:amazonses.com -all`; DMARC TXT `_dmarc` = `v=DMARC1; p=none;`; MAIL FROM MX `mail` → `10 feedback-smtp.us-east-1.amazonses.com`; MAIL FROM SPF TXT `mail` → `v=spf1 include:amazonses.com ~all`.
- **SES identity `summit.us`** (us-east-1) — **PARKED** for the future Comfort switchover. DKIM tokens generated but the 3 CNAMEs are NOT yet in `summit.us` DNS (needs Comfort to add them to their Azure DNS zone, ~2mo).
  - DKIM tokens: `rbahedzetspi25zgaz5bwszt57fju336`, `u4j5sasybzv5kkvkxrfpdzw653v5ugkr`, `ofxbrvnmslc35qwqqnpnwhmklkv7zg5c`
  - Records for Comfort to add (root `summit.us` zone), each CNAME `<token>._domainkey.summit.us` → `<token>.dkim.amazonses.com`. No SPF edit required (DKIM alignment carries it; summit.us has no DMARC). This is safe/additive.

## Current email being replaced
`Utilities/EmailService.cs` — Azure Communication Services (`Azure.Communication.Email.EmailClient` on `CredentialService.AzureEmailConnectionString`, sender `AzureEmailSenderAddress`). Four public methods, all in use:
- `SendAssignmentNotificationAsync` — `Views/ProgressView.xaml.cs`
- `SendAccessRequestEmailAsync` — `App.xaml.cs`
- `SendEmailAsync` — `Dialogs/AdminUsersDialog.xaml.cs`, `Dialogs/FeedbackDialog.xaml.cs` (×2)
- `SendEmailWithAttachmentAsync` — `Dialogs/ExportLogsDialog.xaml.cs`, `Views/TakeoffView.xaml.cs`

## Pre-written rewrite (NOT applied)
`Plans/EmailService_SES_draft.txt` — the full SES v2 rewrite of `EmailService.cs`. Same 4 public signatures (zero call-site changes), identical email HTML/text, attachments via MimeKit raw-MIME. Drop-in at swap time. (It is a `.txt` on purpose so the SDK-style build does not compile it.)

## Remaining steps (ordered)
1. **Chris clicks the ICANN verification email** (15-day deadline; check Mimecast quarantine).
2. **Confirm SES DKIM verified** for `summitapps.net` (`aws sesv2 get-email-identity ...` → DkimAttributes.Status = SUCCESS; VerifiedForSendingStatus = true). Route 53-hosted, so usually verifies within minutes.
3. **Request SES production access** (account-wide, us-east-1; SES starts in sandbox = verified-recipients-only). ~<24h.
4. **Create `vantage-email-user`** — IAM user, send-only policy (`ses:SendEmail`, `ses:SendRawEmail`), generate access key.
5. **Add NuGets:** `AWSSDK.SimpleEmailV2`, `MimeKit`.
6. **Config plumbing:** add `EmailAccessKey` / `EmailSecretKey` / `EmailRegion` to `CredentialService` + `AppConfig.Email` (SenderAddress already exists); retire the ACS `AzureEmailConnectionString`. Paste the `vantage-email-user` key/secret, region `us-east-1`, sender `DoNotReply@summitapps.net` into `appsettings.json` (see Cross-Machine Credential Notes in Project_Status.md — mirror on both PCs before the next publish).
7. **Swap** `EmailService.cs` with the draft, `dotnet build`, test from Visual Studio.
8. **On go-live:** add a Decisions.md fact entry (email sends via AWS SES from summitapps.net) and update README tech stack. Update this doc's status.

## Future — Comfort-blessed switch to `@summit.us`
When Comfort blesses it: have them add the 3 parked `summit.us` DKIM CNAMEs (above) to the summit.us Azure DNS zone → SES auto-verifies → change the sender config from `DoNotReply@summitapps.net` to the summit.us address. No code change beyond the sender string. Both identities coexist in SES.

## Multi-app reuse
Any in-house app on account `430392373397` / us-east-1 can send through these identities — no new DNS. It needs its own (or the shared) send-only key and to send from an address on the verified domain. Production access is account-wide, so once granted every app benefits.
