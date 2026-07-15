#!/usr/bin/env python3
# slides.py - render branded 1920x1080 slide PNGs from a slides.json spec.
# Pure Pillow, no network. Repeatable: edit the JSON, re-run, slides regenerate.
#
# Usage:
#   python slides.py <slides.json> <output_dir>
#
# Output: <output_dir>/<slide.id>.png  (e.g. seg_01.png)

import json
import sys
import os
from PIL import Image, ImageDraw, ImageFont

W, H = 1920, 1080
FONT_DIR = r"C:\Windows\Fonts"


def font(name, size):
    return ImageFont.truetype(os.path.join(FONT_DIR, name), size)


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def gradient_bg(top, bottom):
    top, bottom = hex_rgb(top), hex_rgb(bottom)
    base = Image.new("RGB", (W, H), top)
    draw = ImageDraw.Draw(base)
    for y in range(H):
        t = y / (H - 1)
        col = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        draw.line([(0, y), (W, y)], fill=col)
    return base


def draw_wordmark(draw, theme):
    f = font("segoeui.ttf", 26)
    draw.text((110, H - 80), theme["wordmark"], font=f, fill=hex_rgb(theme["muted"]))


def draw_accent_bar(draw, theme, x, y, w=140, h=8):
    draw.rounded_rectangle([x, y, x + w, y + h], radius=h // 2, fill=hex_rgb(theme["accent"]))


def render_title(img, draw, theme, slide):
    accent = hex_rgb(theme["accent"])
    text = hex_rgb(theme["text"])
    muted = hex_rgb(theme["muted"])
    draw_accent_bar(draw, theme, 110, 430)
    tf = font("segoeuib.ttf", 96)
    draw.text((110, 470), slide["title"], font=tf, fill=text)
    if slide.get("subtitle"):
        sf = font("segoeuil.ttf", 46)
        draw.text((114, 600), slide["subtitle"], font=sf, fill=muted)
    # accent dot flourish top-right
    draw.ellipse([W - 260, 150, W - 180, 230], fill=accent)
    draw.ellipse([W - 175, 190, W - 135, 230], fill=hex_rgb(theme["accent2"]))


def render_bullets(img, draw, theme, slide):
    accent = hex_rgb(theme["accent"])
    text = hex_rgb(theme["text"])
    draw_accent_bar(draw, theme, 110, 210)
    tf = font("segoeuib.ttf", 72)
    draw.text((110, 250), slide["title"], font=tf, fill=text)
    bf = font("segoeui.ttf", 52)
    y = 430
    for b in slide["bullets"]:
        draw.rounded_rectangle([116, y + 18, 140, y + 42], radius=6, fill=accent)
        draw.text((180, y), b, font=bf, fill=text)
        y += 108


def main():
    spec_path, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    with open(spec_path, "r", encoding="utf-8") as fh:
        spec = json.load(fh)
    theme = spec["theme"]
    for slide in spec["slides"]:
        img = gradient_bg(theme["bg_top"], theme["bg_bottom"])
        draw = ImageDraw.Draw(img)
        if slide["type"] == "title":
            render_title(img, draw, theme, slide)
        else:
            render_bullets(img, draw, theme, slide)
        draw_wordmark(draw, theme)
        out = os.path.join(out_dir, slide["id"] + ".png")
        img.save(out)
        print("wrote", out)


if __name__ == "__main__":
    main()
