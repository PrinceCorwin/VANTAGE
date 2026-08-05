# Email Service Migration — Azure ACS → AWS SES

**Goal:** Move VANTAGE's transactional email off the current provider (Azure Communication Services, running on Steve's personal Azure account) onto AWS SES, sending from a company-owned domain. Reusable by other in-house apps (REQit, etc.) on the same company AWS account.

**Status (2026-08-04): BUILT AND VERIFIED, THEN REVERTED — PARKED pending Comfort IT.** The SES side is fully stood up and sending is proven end-to-end. The code swap was applied, tested, and **reverted**; the app is back on the original Azure ACS email (personal account). The blocker is *delivery*, not sending — see below. Flip to SES when Comfort agrees.

---

## BLOCKER — why we reverted (2026-08-04)
`summit.us` mailboxes receive through **Mimecast**. A **brand-new sending domain** (`summitapps.net`, registered today) is quarantined by Mimecast by default — not because authentication fails (DKIM/SPF/DMARC all pass) but because a hours-old domain has **no sending reputation**. The fix is a **single org-wide Mimecast allowlist / permitted-sender entry** for `summitapps.net`, which only **Comfort IT** can make (estimated **months**).

This is the same Comfort dependency the migration was trying to route around: **every** company-domain path — a new domain OR a `summit.us` subdomain — ultimately needs Comfort to trust the sender at the Mimecast gateway. There is no truly Comfort-free path to company-branded mail. **Decision: stay on personal ACS until Comfort allowlists `summitapps.net`, then flip** (a ~10-minute, pre-written config swap).

The old ACS sender (`…azurecomm.net`) "just worked" only because it rode Microsoft's already-trusted infrastructure — which is also exactly why it wasn't ours to depend on.

## Why the migration is still worth doing
The ACS email runs on **Steve's personal Azure account** — a live all-users production feature on personal infrastructure is a single point of failure (billing lapse, lockout, personnel change) outside company control. AWS SES on a company-owned domain in the company AWS account (`430392373397`) removes that. The work is done and parked, not abandoned.

## What's built and verified in AWS (all intact — leave in place)
- **Domain `summitapps.net`** — Route 53, company asset (registrant Chris Aberly / caberly@summit.us, Summit Industrial), 1yr + auto-renew, WHOIS privacy. Hosted zone `Z01697882XFI70LHLYFEE`.
  - ⚠️ **ICANN registrant-email verification still PENDING** — the verify email to caberly@summit.us is stuck in Mimecast; must be clicked within 15 days of 2026-08-04 or the domain suspends. (Alternative: switch the registrant email off Mimecast via a contact update, which re-triggers verification.)
- **SES identity `summitapps.net`** (us-east-1) — Easy DKIM (RSA 2048) + custom MAIL FROM `mail.summitapps.net`. **Verified: DKIM / SPF / DMARC / MAIL-FROM all SUCCESS.** Records live in the Route 53 zone.
  - DKIM tokens: `5rxnrs34slpewyn6brdtdkozpsdf4ihg`, `y7bqbbnc6jsztamawq6grbxf3fsr7zpz`, `vzce6vbexk53nyubzneruvspoigvh5gt`
- **IAM user `vantage-email-user`** — send-only; policy `SesSendOnly` = `ses:SendEmail` + `ses:SendRawEmail` on `arn:aws:ses:us-east-1:430392373397:identity/*`. (Broadened from the two sending identities because **SES sandbox also authorizes the send against the *recipient* identity** — a scoped-to-sender policy fails with a not-authorized error on the recipient.) Access key lives only in local `appsettings.json`, never committed.
- **Sending proven end-to-end:** test emails sent successfully *as `vantage-email-user`* from `DoNotReply@summitapps.net` (MessageIds returned). They land in Mimecast quarantine on the summit.us side — expected until allowlisted.
- **SES production access:** requested 2026-08-04, pending (<24h typical). Until granted, sandbox = verified-recipients-only (200/day).
- **Parked SES identity `summit.us`** — for a later switch to `@summit.us`; needs its 3 DKIM CNAMEs added to Comfort-managed DNS. Tokens: `rbahedzetspi25zgaz5bwszt57fju336`, `u4j5sasybzv5kkvkxrfpdzw653v5ugkr`, `ofxbrvnmslc35qwqqnpnwhmklkv7zg5c`.

## Current app state — REVERTED to ACS (2026-08-04)
- **Work PC:** `Utilities/EmailService.cs`, `Models/AppConfig.cs`, `Utilities/CredentialService.cs`, `VANTAGE.csproj`, and `appsettings.json` all reverted to the ACS versions. Builds clean, 0 errors. The home PC's config was never changed.
- The SES rewrite of `EmailService.cs` is preserved at **`Plans/EmailService_SES_draft.txt`** (SES v2, same 4 public signatures, MimeKit raw-MIME attachments) — re-apply when going live.

## Go-live checklist (when Comfort agrees)
1. **Comfort IT allowlists `summitapps.net` in Mimecast** (one org-wide permitted-sender entry). ← the gate. The already-quarantined test messages help IT find the sender.
2. **SES production access** granted (already requested).
3. **ICANN verify** clicked by Chris (or registrant email switched off Mimecast).
4. **Re-apply the code swap** from `Plans/EmailService_SES_draft.txt`: replace `EmailService.cs`; add `AccessKey`/`SecretKey`/`Region` to `EmailConfig` + `CredentialService.Email*`; add NuGets `AWSSDK.SimpleEmailV2` + `MimeKit`; remove `Azure.Communication.Email`.
5. Paste the `vantage-email-user` key + secret + `DoNotReply@summitapps.net` + region `us-east-1` into `appsettings.json` on **both PCs** (cross-machine cred rule); `dotnet build`; test; `/publisher`.
6. On go-live: add a Decisions.md fact entry (email via SES from summitapps.net) and update the README tech stack.

## Future — switch the sender to `@summit.us` (later, optional)
When Comfort blesses it: add the 3 parked `summit.us` DKIM CNAMEs to the summit.us Azure DNS zone → SES auto-verifies → change the sender config from `DoNotReply@summitapps.net` to the summit.us address. No code change beyond the sender string. Both identities coexist in SES.

## Multi-app reuse
Any in-house app on account `430392373397` / us-east-1 can send through these identities — no new DNS. It needs a send-only key and to send from an address on the verified domain. Production access is account-wide.
