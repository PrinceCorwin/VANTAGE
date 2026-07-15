# narrate_draft.ps1 - DRAFT narration using the free offline Windows SAPI voice.
# This is a stand-in so you can preview pacing before wiring up Azure Neural TTS.
# It parses script.md: each "## seg_NN" header + its "> narration" line -> seg_NN.wav
#
# Usage:
#   powershell -File narrate_draft.ps1 -ScriptMd <path\script.md> -OutDir <path\audio> [-VoiceName "Microsoft Zira Desktop"] [-Rate -1]

param(
    [Parameter(Mandatory = $true)][string]$ScriptMd,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$VoiceName = "Microsoft David Desktop",
    [int]$Rate = -1
)

Add-Type -AssemblyName System.Speech
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

$lines = Get-Content -LiteralPath $ScriptMd
$currentId = $null
$pendingNarration = $false
$map = [ordered]@{}

foreach ($line in $lines) {
    $h = [regex]::Match($line, '^##\s+(seg_\d+)')
    if ($h.Success) { $currentId = $h.Groups[1].Value; $pendingNarration = $false; continue }
    if ($line -match '^\*\*Narration:\*\*') { $pendingNarration = $true; continue }
    if ($pendingNarration -and $line -match '^\s*>\s?(.*)$') {
        $text = $Matches[1].Trim()
        if ($currentId -and $text) { $map[$currentId] = $text }
        $pendingNarration = $false
    }
}

if ($map.Count -eq 0) { Write-Error "No 'seg_NN' + '> narration' pairs found in $ScriptMd"; exit 1 }

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
try { $synth.SelectVoice($VoiceName) } catch { Write-Warning "Voice '$VoiceName' not found; using default." }
$synth.Rate = $Rate

foreach ($id in $map.Keys) {
    $wav = Join-Path $OutDir "$id.wav"
    $synth.SetOutputToWaveFile($wav)
    $synth.Speak($map[$id])
    Write-Host "wrote $wav  <-  $($map[$id])"
}
$synth.SetOutputToNull()
$synth.Dispose()
Write-Host "Draft narration complete: $($map.Count) clips."
