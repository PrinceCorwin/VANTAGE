# PRD: Rev Bubble Takeoff Filter — Vantage + Step Functions Integration

## Background

The Summit Takeoff pipeline extracts BOM data from piping ISO drawings via AWS Lambda + Step Functions. A new feature has been added to the extraction Lambda that allows filtering extraction to only items inside revision bubbles (cloud-shaped annotation borders). The Lambda change is already deployed and validated.

This PRD covers the remaining work: Vantage WPF UI changes and the Step Functions definition update to pass the flag through.

## Current State

- **Extraction Lambda**: Deployed. Accepts `rev_bubble_only` (bool) in its event. When `true`, only BOM items inside revision bubbles are extracted. Defaults to `false`.
- **Step Functions**: Does NOT yet pass `rev_bubble_only` through to Lambda. The Map state's Parameters block must be updated.
- **Vantage WPF app**: Does NOT yet include `rev_bubble_only` in the Step Functions input JSON.

## Requirements

### 1. Vantage C# — Add `rev_bubble_only` to Step Functions Input

Find where Vantage builds the Step Functions `StartExecution` input JSON. It currently looks something like:

```json
{
    "config_path": "clients/steve/lillytest.json",
    "bucket": "summit-takeoff-drawings",
    "drawing_keys": ["steve/lilly-test/drawing1.pdf", ...],
    "rev_bubble_only": false
}
```

Add `"rev_bubble_only"` to that JSON object. Value comes from the new UI checkbox (see below). **This must be done BEFORE the Step Functions definition is updated, otherwise existing Vantage batches will fail.**

### 2. Vantage C# — Add UI Checkbox

Add a checkbox labeled **"Rev Bubble Items Only"** to the batch execution screen, near the existing controls (e.g., near the "Run Takeoff" button or in the config area).

- Default state: **unchecked** (false)
- When checked, `rev_bubble_only` is set to `true` in the Step Functions input
- When unchecked, `rev_bubble_only` is set to `false`
- No tooltip needed — the label is self-explanatory

### 3. Step Functions Definition Update

After Vantage is updated and confirmed sending the field, update the Step Functions state machine definition.

In the `ProcessDrawings` Map state, the `Parameters` block currently has:

```json
"Parameters": {
    "config_path.$": "$.config_path",
    "drawing_key.$": "$$.Map.Item.Value",
    "bucket.$": "$.bucket",
    "batch_id.$": "$$.Execution.Name"
}
```

Add one line:

```json
"Parameters": {
    "config_path.$": "$.config_path",
    "drawing_key.$": "$$.Map.Item.Value",
    "bucket.$": "$.bucket",
    "batch_id.$": "$$.Execution.Name",
    "rev_bubble_only.$": "$.rev_bubble_only"
}
```

PowerShell command to apply:

```powershell
$def = aws stepfunctions describe-state-machine --state-machine-arn arn:aws:states:us-east-1:430392373397:stateMachine:summit-takeoff-orchestrator --region us-east-1 --query "definition" --output text

$def = $def.Replace('"batch_id.$":"$$.Execution.Name"', '"batch_id.$":"$$.Execution.Name","rev_bubble_only.$":"$.rev_bubble_only"')

[System.IO.File]::WriteAllText("$env:USERPROFILE\sfn-definition.json", $def)

aws stepfunctions update-state-machine --state-machine-arn arn:aws:states:us-east-1:430392373397:stateMachine:summit-takeoff-orchestrator --definition "file://$env:USERPROFILE\sfn-definition.json" --region us-east-1
```

## Order of Operations (Critical)

1. **First**: Update Vantage C# to always include `"rev_bubble_only": false` (or `true` based on checkbox) in the Step Functions input JSON
2. **Second**: Update the Step Functions definition to pass the field through
3. If done out of order, all Vantage batch submissions will fail with `States.ParameterPathFailure`

## Constraints

- C# comments: `//` style only, never `///` XML doc tags
- The checkbox state does not need to persist between sessions — default unchecked every time
- No changes needed to the aggregation Lambda or extraction prompt
- No changes needed to the Excel output schema

## Validation

After both changes are applied:
1. Run a batch with checkbox **unchecked** — should extract all BOM items (normal behavior)
2. Run a batch with checkbox **checked** on drawings with rev bubbles — should extract only items inside rev bubbles
3. Run a batch with checkbox **checked** on drawings without rev bubbles — should return 0 BOM items per drawing
