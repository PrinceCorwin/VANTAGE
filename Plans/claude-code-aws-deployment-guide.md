# Guide: AWS Deployment & Terminal Tasks

For Claude Code to handle AWS operations on the Summit Takeoff pipeline.

---

## Operating principles

These aren't optional. They come from real mistakes that have cost hours.

### 1. Verify every change with a direct CLI call
Don't assume a command succeeded because it didn't error. Capture the before state, run the change, capture the after state, compare. For Lambda deploys this means SHA256 comparison. For S3 uploads it means `head-object` to confirm ContentLength and ETag changed. **ECR pushes have silently failed with 403s while `update-function-code` still succeeded by reusing the old image.** SHA verification is non-negotiable.

### 2. Run commands one at a time, paste output back
Steve pastes terminal output after each command. Do not batch multiple commands into one block unless he asks for it. Wait for output before issuing the next command. This catches failures immediately instead of letting them cascade.

### 3. PowerShell, not bash
All AWS CLI work runs in PowerShell on Steve's work PC (`G:\My Drive\...`) or personal PC (`C:\Users\steve\My Drive\...`). PowerShell-isms apply:
- Variables: `$OldSha = aws ...`
- String interpolation: `"$env:USERPROFILE\..."`
- Never use `>` for JSON redirection — produces UTF-16 which breaks AWS CLI. Use `[System.IO.File]::WriteAllText()` instead.

### 4. Never assume file locations
Ask or verify. Paths differ between work PC (`G:\`) and personal PC (`C:\Users\steve\`). Files named `lambda_function.py` exist in multiple folders (extraction, aggregation) — confirm which one before editing.

### 5. Do not propose fixes without proof
If something is wrong, gather data (CloudWatch logs, failure markers, head-object output) before proposing a change. Don't guess root causes. Don't say "this should work" — show the evidence.

### 6. Fix things you mention
If you identify an issue, fix it or don't mention it. Do not say "minor, not worth touching" when the fix is trivial. The analysis is the expensive part; the edit is cheap.

---

## Standard deployment patterns

### Pattern A — Prompt deploy (S3 only)

Fastest deploy. No Lambda redeploy needed because the extraction Lambda reads the prompt fresh from S3 on every invocation.

```powershell
# Optional: backup current live version (Steve often skips this once he trusts the change)
aws s3 cp s3://summit-takeoff-config/extraction_prompt.txt "$env:USERPROFILE\extraction_prompt.backup-$(Get-Date -Format yyyyMMdd-HHmmss).txt" --region us-east-1

# Upload
aws s3 cp "<LOCAL_PATH>\extraction_prompt.txt" s3://summit-takeoff-config/extraction_prompt.txt --region us-east-1

# Verify
aws s3api head-object --bucket summit-takeoff-config --key extraction_prompt.txt --region us-east-1
```

Confirm the returned `ContentLength` matches the local file's size and `LastModified` is within the last minute.

Same pattern for `CompRefTable.xlsx` and `MatRefTable.xlsx` — swap filename and content-type is handled automatically.

### Pattern B — Aggregation Lambda deploy (zip)

The aggregation Lambda is deployed as a zip. Its folder contains both the Python code and bundled dependencies (`openpyxl`, `et_xmlfile`, and their `.dist-info` folders). All files in that folder get zipped and uploaded.

```powershell
# Capture current SHA for comparison
$OldSha = aws lambda get-function --function-name summit-takeoff-aggregate --region us-east-1 --query "Configuration.CodeSha256" --output text
Write-Host "Old SHA256: $OldSha"

# Build zip
cd "<AGGREGATE_DEPLOY_FOLDER>"
Remove-Item aggregate-deploy.zip -ErrorAction SilentlyContinue
Compress-Archive -Path * -DestinationPath aggregate-deploy.zip -Force
Get-Item aggregate-deploy.zip | Select-Object Name, Length, LastWriteTime

# Push to Lambda
aws lambda update-function-code --function-name summit-takeoff-aggregate --zip-file "fileb://aggregate-deploy.zip" --region us-east-1

# Verify SHA changed and status is Successful
Start-Sleep -Seconds 10
$NewSha = aws lambda get-function --function-name summit-takeoff-aggregate --region us-east-1 --query "Configuration.CodeSha256" --output text
$UpdateStatus = aws lambda get-function --function-name summit-takeoff-aggregate --region us-east-1 --query "Configuration.LastUpdateStatus" --output text
Write-Host "Old SHA256: $OldSha"
Write-Host "New SHA256: $NewSha"
Write-Host "LastUpdateStatus: $UpdateStatus"
if ($OldSha -eq $NewSha) { Write-Host "!!! DEPLOY FAILED — SHA unchanged" -ForegroundColor Red } else { Write-Host "Deploy confirmed." -ForegroundColor Green }
```

### Pattern C — Extraction Lambda deploy (container)

This is the riskiest deploy. Docker build, ECR push, Lambda image update. Verify Docker Desktop is running before starting.

```powershell
# Capture current SHA
$OldSha = aws lambda get-function --function-name summit-takeoff-poc --region us-east-1 --query "Configuration.CodeSha256" --output text
Write-Host "Old SHA256: $OldSha"

# ECR login
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 430392373397.dkr.ecr.us-east-1.amazonaws.com

# Build (--provenance=false is required; --no-cache avoids stale-layer problems)
cd "<EXTRACTION_LAMBDA_FOLDER>"
docker build --provenance=false --no-cache -t summit-takeoff-poc .

# Tag and push
docker tag summit-takeoff-poc:latest 430392373397.dkr.ecr.us-east-1.amazonaws.com/summit-takeoff-poc:latest
docker push 430392373397.dkr.ecr.us-east-1.amazonaws.com/summit-takeoff-poc:latest

# Update Lambda
aws lambda update-function-code --function-name summit-takeoff-poc --image-uri 430392373397.dkr.ecr.us-east-1.amazonaws.com/summit-takeoff-poc:latest --region us-east-1

# Wait longer for container deploys (30s vs 10s for zip)
Start-Sleep -Seconds 30
$NewSha = aws lambda get-function --function-name summit-takeoff-poc --region us-east-1 --query "Configuration.CodeSha256" --output text
$UpdateStatus = aws lambda get-function --function-name summit-takeoff-poc --region us-east-1 --query "Configuration.LastUpdateStatus" --output text
Write-Host "New SHA256: $NewSha"
Write-Host "LastUpdateStatus: $UpdateStatus"
```

**Known failure modes:**
- Base image fetch from `public.ecr.aws/lambda/python:3.12` occasionally EOFs. Retry the same command — usually works on second attempt.
- `docker push` can return 403 Forbidden and exit with success, letting `update-function-code` reuse the old image. That's why SHA verification after deploy is mandatory — catch silent failures.
- Google Drive-mounted folders can serve stale files to Docker. If rebuild seems to have no effect, copy build context to `$env:USERPROFILE\takeoff-build` first. Personal PC typically doesn't hit this; work PC has hit it before.

---

## Debugging production failures

### When a batch fails or returns wrong data

**Step 1 — Identify the batch.** Get the batch_id from Vantage's "Previous Batches" list. Format: `AwsDwgTakeoff-YYYYMMDD-HHMMSS`.

**Step 2 — List the batch contents.**
```powershell
aws s3 ls s3://summit-takeoff-processing/batches/<BATCH_ID>/ --recursive --region us-east-1
```
Look for:
- `extractions/*.json` — successful extractions
- `failures/*.json` — failed drawings with error details
- `output/*.xlsx` — final Excel (may exist even if some drawings failed)
- `metadata.json` — batch metadata

**Step 3 — Pull failure markers.**
```powershell
aws s3 cp s3://summit-takeoff-processing/batches/<BATCH_ID>/failures/<DRAWING>.json - --region us-east-1
```
The `-` at the end streams to stdout for immediate viewing. The failure JSON contains `error`, `source_key`, `drawing_name`, `timestamp`.

**Step 4 — Pull extraction JSONs for successful-but-wrong drawings.**
```powershell
aws s3 cp s3://summit-takeoff-processing/batches/<BATCH_ID>/extractions/<DRAWING>.json "$env:USERPROFILE\extraction.json" --region us-east-1
type "$env:USERPROFILE\extraction.json"
```
Look at the raw JSON that came back from Claude — especially `confidence`, `flag`, and `extraction_notes` fields for clues.

**Step 5 — CloudWatch for deeper debugging.**
```powershell
aws logs tail /aws/lambda/summit-takeoff-poc --since 10m --region us-east-1
```
`--since` accepts `5m`, `1h`, `1d`. Key log lines to look for:
- `Processing drawing:` — start of each invocation
- `Bedrock usage — input tokens: X, output tokens: Y` — confirms Bedrock responded
- `Raw response (first 2000 chars):` — dumped on JSON parse failure, shows what Claude actually returned
- `TRUNCATED_RESPONSE` — Claude hit max_tokens; increase `MAX_TOKENS` in Lambda code

Known issue: CloudWatch output containing `→` or other non-ASCII characters truncates PowerShell output. Filter with keyword patterns if that happens.

---

## Verifying a config change landed

Pattern for any S3-backed config:

```powershell
aws s3api head-object --bucket summit-takeoff-config --key <FILENAME> --region us-east-1
```

Check `LastModified` is within the last few minutes and `ContentLength` matches the uploaded file. Every S3 upload should be verified this way.

---

## Current production constants (April 23, 2026)

- **AWS account**: `430392373397`
- **Region**: `us-east-1`
- **Model ID**: `us.anthropic.claude-sonnet-4-6`
- **Extraction Lambda**: `summit-takeoff-poc` (container)
- **Aggregation Lambda**: `summit-takeoff-aggregate` (zip)
- **Step Functions**: `summit-takeoff-orchestrator`, MaxConcurrency=3
- **ECR repo**: `430392373397.dkr.ecr.us-east-1.amazonaws.com/summit-takeoff-poc`

### S3 buckets
- `summit-takeoff-config` — prompts, reference tables, client configs
- `summit-takeoff-drawings` — uploaded PDFs (deleted after processing)
- `summit-takeoff-processing` — per-batch extractions, failures, output Excel

### Config file paths in S3
- `s3://summit-takeoff-config/extraction_prompt.txt`
- `s3://summit-takeoff-config/CompRefTable.xlsx`
- `s3://summit-takeoff-config/MatRefTable.xlsx`
- `s3://summit-takeoff-config/clients/{user}/{project}.json`

### IAM
- `vantage-takeoff-user` — scoped to Step Functions + 3 takeoff S3 buckets. Policy name `TakeoffAppAccess`. Used by Vantage app (CLI profile `takeoff-app`).
- `steveAmalfitano` — full dev access. Used for admin/deploy work (CLI profile `default`).

---

## Communication with Steve

- **Be direct.** No hedging, no "should work", no "I think." State what the data shows and what you'll do.
- **No apologies for accumulated failures.** If something failed, state the root cause and the fix. One sentence.
- **Don't re-explain what he just told you.** If he describes a problem, acknowledge it and move to the fix. Don't restate it back.
- **Don't circle back on decided topics.** If a direction has been chosen, execute. Don't offer it as a "still-an-option" in the next message.
- **Ask, don't guess, on file paths and machine context.** Steve has two PCs with different layouts. Ask which machine before assuming paths.
- **One command at a time, wait for output.** Don't batch commands. Paste output gets pasted back, you evaluate, then you issue the next command.
- **If you mention an issue, fix it or don't mention it.** "Not worth fixing" when the fix is trivial is not an option.
- **Use the `SIZE_` style log keyword when adding new warning categories** — patterns already in use: `CLASS_RATING_CONFLICT`, `SIZE_NORMALIZATION_FAILED`, `CLASS_RATING_BACKFILL`, `SIZE_TRUNCATED`. All-caps + underscore prefix makes them scannable in CloudWatch.

---

## Rollback references

Every deploy captures the prior SHA256 / ETag. Keep those in the conversation or in a scratchpad so a rollback is always one command away.

**S3 config rollback** (prompt, CompRefTable, MatRefTable):
```powershell
aws s3 cp "<BACKUP_LOCAL_PATH>" s3://summit-takeoff-config/<FILENAME> --region us-east-1
aws s3api head-object --bucket summit-takeoff-config --key <FILENAME> --region us-east-1
```

**Lambda rollback**: the previous image/zip must be rebuildable from the prior commit. There's no automatic version alias pointing to a known-good state. If a deploy breaks production, the fastest rollback is to redeploy from the last good local state (i.e., build/push again from the pre-change code).

---

## Utilities

### `inspect-crop-local.py`
Reproduces the exact BOM crop the extraction Lambda sends to Bedrock, using a local PDF file and the client config from S3. Lives in `summit-takeoff-poc/` folder. Useful when debugging "why did Claude fail on this drawing" — the output PNG is ground truth for what the model actually saw.

```powershell
python inspect-crop-local.py <client_id> <project_id> "<path-to-local-pdf>"
```

Outputs two PNGs: `<drawing>_full.png` and `<drawing>_BOM.png`.

Requires `boto3`, `PyMuPDF`, `Pillow`. Install with `python -m pip install boto3 PyMuPDF Pillow` if missing.

---

## Things NOT to do

- Don't modify CompRefTable in Lambda code — it's pulled from S3 at runtime. Changes go to the .xlsx file, upload to S3.
- Don't add normalization back to the aggregation Lambda for size or quantity. C# is the single normalization path. Lambda is pass-through.
- Don't use `Copy-Item` in deploy scripts to move files between folders — it's how wrong-version files end up getting zipped. Work in the deploy folder directly or explicitly copy then confirm hashes.
- Don't deploy to the extraction Lambda without capturing the old SHA first. Silent ECR reuse has cost hours of confusion.
- Don't use `conversation_search` or `recent_chats` tools to reconstruct context — use the handoff documents Steve provides or ask him directly. Past conversations contain stale decisions.
