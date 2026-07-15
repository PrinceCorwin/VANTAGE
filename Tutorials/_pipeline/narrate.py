#!/usr/bin/env python3
# narrate.py - FINAL narration via Azure Neural TTS (natural human-sounding voice).
# Parses script.md (same "## seg_NN" + "> narration" format the draft uses) and
# writes one WAV per segment into the audio dir, ready for build.py.
#
# Setup (one time):
#   pip install azure-cognitiveservices-speech
#   set AZURE_SPEECH_KEY=<your key>
#   set AZURE_SPEECH_REGION=<your region, e.g. eastus>
#
# Usage:
#   python narrate.py --script <script.md> --out <audio dir> [--voice en-US-BrianNeural] [--rate 0%]
#
# Audition voices by re-running with different --voice values:
#   en-US-BrianNeural, en-US-AndrewMultilingualNeural, en-US-AvaMultilingualNeural,
#   en-US-GuyNeural, en-US-JennyNeural

import argparse
import os
import re
import sys
import html

try:
    import azure.cognitiveservices.speech as speechsdk
except ImportError:
    sys.exit("Missing SDK. Run: pip install azure-cognitiveservices-speech")


def parse_segments(script_md):
    # returns ordered list of (seg_id, narration_text)
    with open(script_md, "r", encoding="utf-8") as fh:
        lines = fh.read().splitlines()
    segments, current, pending = [], None, False
    for line in lines:
        m = re.match(r"^##\s+(seg_\d+)", line)
        if m:
            current, pending = m.group(1), False
            continue
        if re.match(r"^\*\*Narration:\*\*", line):
            pending = True
            continue
        if pending:
            q = re.match(r"^\s*>\s?(.*)$", line)
            if q and current and q.group(1).strip():
                segments.append((current, q.group(1).strip()))
                pending = False
    return segments


def ssml(text, voice, rate):
    safe = html.escape(text)
    return (
        f'<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US">'
        f'<voice name="{voice}"><prosody rate="{rate}">{safe}</prosody></voice></speak>'
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--script", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--voice", default="en-US-BrianNeural")
    ap.add_argument("--rate", default="0%")  # e.g. "-8%" to slow down slightly
    args = ap.parse_args()

    key = os.environ.get("AZURE_SPEECH_KEY")
    region = os.environ.get("AZURE_SPEECH_REGION")
    if not key or not region:
        sys.exit("Set AZURE_SPEECH_KEY and AZURE_SPEECH_REGION environment variables.")

    os.makedirs(args.out, exist_ok=True)
    segments = parse_segments(args.script)
    if not segments:
        sys.exit(f"No segments found in {args.script}")

    speech_config = speechsdk.SpeechConfig(subscription=key, region=region)
    # 24kHz mono PCM WAV - clean and small, mixes fine in ffmpeg
    speech_config.set_speech_synthesis_output_format(
        speechsdk.SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm)

    for seg_id, text in segments:
        wav = os.path.join(args.out, seg_id + ".wav")
        audio_config = speechsdk.audio.AudioOutputConfig(filename=wav)
        synth = speechsdk.SpeechSynthesizer(speech_config=speech_config, audio_config=audio_config)
        result = synth.speak_ssml_async(ssml(text, args.voice, args.rate)).get()
        if result.reason == speechsdk.ResultReason.SynthesizingAudioCompleted:
            print(f"wrote {wav}")
        else:
            details = getattr(result, "cancellation_details", None)
            sys.exit(f"FAILED on {seg_id}: {result.reason} {details.error_details if details else ''}")

    print(f"Azure narration complete: {len(segments)} clips with voice {args.voice}.")


if __name__ == "__main__":
    main()
