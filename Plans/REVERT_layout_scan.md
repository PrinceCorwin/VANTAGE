# Summit Takeoff — Revert to Pre-Layout-Scan Stable State

Saved: March 11, 2026

Both Lambdas and the extraction prompt were snapshotted before any
layout scan experimentation began. Run the commands below to restore.

---

## Stable Snapshot Reference

| Resource | Stable Identifier |
|---|---|
| Extraction Lambda version | **1** |
| Extraction Lambda ARN | `arn:aws:lambda:us-east-1:430392373397:function:summit-takeoff-poc:1` |
| Extraction Lambda CodeSha256 | `f5354ccac024deb2e9a098b220fc9e79dd38a5c0d14f5f7cb35c263f65aa0571` |
| Aggregation Lambda version | **1** |
| Aggregation Lambda ARN | `arn:aws:lambda:us-east-1:430392373397:function:summit-takeoff-aggregate:1` |
| Aggregation Lambda CodeSha256 | `ygYbEMZpGilLCqVq02wa7MKzQNVe2cx0kUqVA9L2zZc=` |
| Prompt backup | `s3://summit-takeoff-config/extraction_prompt_stable.txt` |

---

## Revert — Run These in Order

### 1. Restore the extraction prompt

```powershell
aws s3 cp `
  s3://summit-takeoff-config/extraction_prompt_stable.txt `
  s3://summit-takeoff-config/extraction_prompt.txt `
  --region us-east-1
```

### 2. Revert the Extraction Lambda (container)

The extraction Lambda is a container deployment. Get the image URI from
the stable version, then push it back to $LATEST:

```powershell
# Step 2a — get the stable image URI
aws lambda get-function `
  --function-name summit-takeoff-poc `
  --qualifier 1 `
  --region us-east-1 `
  --query "Code.ImageUri"
```

```powershell
# Step 2b — redeploy $LATEST from that image URI
# Replace {IMAGE_URI} with the value returned above
aws lambda update-function-code `
  --function-name summit-takeoff-poc `
  --image-uri {IMAGE_URI} `
  --region us-east-1
```

### 3. Revert the Aggregation Lambda (zip)

The aggregation Lambda source lives at:
`G:\My Drive\Conversion\aggregate-deploy\`

That folder has not been modified as part of layout scan work. Redeploy
it directly — this restores $LATEST to match the stable Version 1 zip
(CodeSha256: `ygYbEMZpGilLCqVq02wa7MKzQNVe2cx0kUqVA9L2zZc=`):

```powershell
cd "G:\My Drive\Conversion\aggregate-deploy"
Remove-Item aggregate-deploy.zip -ErrorAction SilentlyContinue
Compress-Archive -Path * -DestinationPath aggregate-deploy.zip -Force
aws lambda update-function-code `
  --function-name summit-takeoff-aggregate `
  --zip-file "fileb://aggregate-deploy.zip" `
  --region us-east-1
```

### 4. Verify

Run a test batch against a known-good drawing and confirm output matches
pre-experiment results.

---

## Notes

- Lambda published versions are immutable. Versions 1/1 will remain
  intact in AWS regardless of how many times $LATEST is redeployed.
- The prompt backup `extraction_prompt_stable.txt` is a separate S3
  object — it is unaffected by any prompt edits to `extraction_prompt.txt`.
- Client crop configs are not affected by Lambda or prompt changes.
  If a `legend_pdf_key` or `legend_image_keys` field was added to a
  config JSON during experimentation, remove those fields from the
  config and re-save to fully revert that config.
- Step Functions orchestrator definition does not change for the layout
  scan experiment — no revert needed there.
