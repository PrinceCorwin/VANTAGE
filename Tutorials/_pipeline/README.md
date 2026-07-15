# VANTAGE Tutorial Video Pipeline

Scripted, repeatable production for VANTAGE tutorial videos: title/concept
**slides + narration** generated from source files, assembled with FFmpeg into an
MP4. When the app or the script changes, re-run — no manual video re-editing.

Final videos are distributed via **Google Drive**, not committed. `raw/`,
`audio/`, `out/`, and `frames/` are gitignored; scripts, `script.md`, `slides.json`,
and `assets/` are tracked.

## Per-video folder layout
```
<video-name>/
  script.md      Narration + on-screen notes. Single source of truth for wording.
                 Each "## seg_NN" header + its "> ..." line = one narrated clip.
  slides.json    Slide spec (title/bullets cards) keyed by the same seg_NN ids.
  assets/        Generated slide PNGs (+ any hand-made graphics). Tracked.
  raw/           Your screen recordings drop here. Gitignored.
  audio/         Generated narration WAVs (seg_NN.wav). Gitignored.
  out/           Final MP4 + captions. Gitignored.
```

## Tools (in _pipeline/)
| Script | What it does |
|---|---|
| `slides.py` | Render 1920x1080 branded slide PNGs from `slides.json` (Pillow). |
| `narrate.py` | FINAL narration via **Azure Neural TTS**. Reads `script.md`. |
| `narrate_draft.ps1` | DRAFT narration via free offline Windows voice (pacing preview only). |
| `build.py` | Assemble slides + narration into an MP4 (Ken Burns zoom, fades, synced timing). FFmpeg. |

## Build a video

1. **Slides:** `python _pipeline/slides.py <video>/slides.json <video>/assets`
2. **Narration** (final):
   - one-time: `pip install azure-cognitiveservices-speech`, set `AZURE_SPEECH_KEY` and `AZURE_SPEECH_REGION`
   - `python _pipeline/narrate.py --script <video>/script.md --out <video>/audio --voice en-US-BrianNeural`
   - (draft alternative: `powershell -File _pipeline/narrate_draft.ps1 -ScriptMd <video>/script.md -OutDir <video>/audio`)
3. **Assemble:** `python _pipeline/build.py --slides <video>/slides.json --assets <video>/assets --audio <video>/audio --out <video>/out/<name>.mp4`

## Adding screen-recorded workflow footage
The proof-of-concept is slides-only. To fold in real VANTAGE footage: record the
workflow (OBS or Win+G), drop the clip in `raw/`, and `build.py` gains a step to
splice recorded segments between slides (zoom-to-cursor, highlight callouts,
captions). That extension lands when we build the first workflow-style tutorial.

## Notes
- FFmpeg is auto-located (PATH, then the winget install dir).
- All generated text files use CRLF to keep Visual Studio quiet.
