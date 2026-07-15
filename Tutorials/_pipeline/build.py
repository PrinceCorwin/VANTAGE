#!/usr/bin/env python3
# build.py - assemble a slideshow video from slide PNGs + per-segment narration.
#
# For each slide id (order from slides.json) it pairs:
#     <assets>/<id>.png   with   <audio>/<id>.wav
# builds a segment with a slow Ken Burns zoom + fade in/out, sized to the
# narration length (+ tail padding), then concatenates all segments to one MP4.
#
# Usage:
#   python build.py --slides <slides.json> --assets <dir> --audio <dir> --out <file.mp4>
#                   [--fps 30] [--tail 0.7] [--fade 0.4]
#
# ffmpeg/ffprobe are auto-located (PATH, then the winget install dir).

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import glob


def find_tool(name):
    p = shutil.which(name)
    if p:
        return p
    local = os.environ.get("LOCALAPPDATA", "")
    pattern = os.path.join(local, "Microsoft", "WinGet", "Packages",
                           "Gyan.FFmpeg*", "**", name + ".exe")
    hits = glob.glob(pattern, recursive=True)
    if hits:
        return hits[0]
    sys.exit(f"ERROR: {name} not found on PATH or in the winget package dir. "
             f"Open a fresh terminal (winget updated PATH) or install FFmpeg.")


FFMPEG = find_tool("ffmpeg")
FFPROBE = find_tool("ffprobe")


def probe_duration(path):
    out = subprocess.run(
        [FFPROBE, "-v", "error", "-show_entries", "format=duration",
         "-of", "default=noprint_wrappers=1:nokey=1", path],
        capture_output=True, text=True, check=True)
    return float(out.stdout.strip())


def build_segment(png, wav, dur, fps, fade, out_path):
    frames = int(round(dur * fps))
    # scale up 2x before zoompan to reduce integer-rounding jitter, then output 1080p
    vf = (
        f"scale=3840:2160,setsar=1,"
        f"zoompan=z='min(zoom+0.0006,1.10)':d={frames}:"
        f"x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1920x1080:fps={fps},"
        f"fade=t=in:st=0:d={fade},fade=t=out:st={dur - fade:.3f}:d={fade}"
    )
    cmd = [FFMPEG, "-y", "-loop", "1", "-i", png]
    if wav:
        cmd += ["-i", wav]
    cmd += ["-t", f"{dur:.3f}", "-vf", vf,
            "-c:v", "libx264", "-preset", "medium", "-crf", "18",
            "-pix_fmt", "yuv420p", "-r", str(fps)]
    if wav:
        cmd += ["-c:a", "aac", "-b:a", "192k", "-map", "0:v", "-map", "1:a"]
    else:
        cmd += ["-an"]
    cmd += [out_path]
    subprocess.run(cmd, check=True, capture_output=True, text=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--slides", required=True)
    ap.add_argument("--assets", required=True)
    ap.add_argument("--audio", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--tail", type=float, default=0.7)
    ap.add_argument("--fade", type=float, default=0.4)
    args = ap.parse_args()

    with open(args.slides, "r", encoding="utf-8") as fh:
        spec = json.load(fh)
    ids = [s["id"] for s in spec["slides"]]

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    tmp = tempfile.mkdtemp(prefix="vantage_build_")
    seg_files = []
    try:
        for i, sid in enumerate(ids):
            png = os.path.join(args.assets, sid + ".png")
            wav = os.path.join(args.audio, sid + ".wav")
            if not os.path.exists(png):
                sys.exit(f"ERROR: missing slide image {png}")
            has_wav = os.path.exists(wav)
            dur = (probe_duration(wav) + args.tail) if has_wav else 3.5
            seg_out = os.path.join(tmp, f"seg_{i:03d}.mp4")
            print(f"[{i+1}/{len(ids)}] {sid}: dur={dur:.2f}s "
                  f"{'(narrated)' if has_wav else '(silent)'}")
            build_segment(png, wav if has_wav else None, dur, args.fps, args.fade, seg_out)
            seg_files.append(seg_out)

        listfile = os.path.join(tmp, "concat.txt")
        with open(listfile, "w", encoding="utf-8") as fh:
            for s in seg_files:
                fh.write(f"file '{s.replace(chr(92), '/')}'\n")

        subprocess.run(
            [FFMPEG, "-y", "-f", "concat", "-safe", "0", "-i", listfile,
             "-c", "copy", args.out],
            check=True, capture_output=True, text=True)
        dur = probe_duration(args.out)
        print(f"\nDONE -> {args.out}  ({dur:.1f}s, {len(seg_files)} segments)")
    except subprocess.CalledProcessError as e:
        sys.stderr.write(e.stderr or "")
        raise
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
