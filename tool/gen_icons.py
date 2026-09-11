"""Generates the ClipSyncAI launcher icon, adaptive layers and splash mark.

Run from the project root:  python tool/gen_icons.py

The glyph is Material's content_paste_rounded, rendered from the same icon font
Flutter ships, so the launcher icon and the AppMark widget inside the app are
the same mark rather than two drawings of one idea. The plate is the same
gradient squircle with the same specular edge AppMark paints, for the same
reason.
"""

import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
RES = os.path.join(ROOT, 'android', 'app', 'src', 'main', 'res')
FONT = ('C:/src/flutter/bin/cache/artifacts/material_fonts/'
        'materialicons-regular.otf')
GLYPH = '\uf66f'  # Icons.content_paste_rounded

# ── Brand ───────────────────────────────────────────────────────────────────
# Rose quartz and its companion, straight out of lib/ui/design_tokens.dart.
ROSE = (0xDF, 0xA5, 0xB4)
ROSE_DEEP = (0xC9, 0x8F, 0xA8)
INK = (0xF7, 0xF5, 0xF2)      # warm off-white, never pure white

PLATE_RADIUS = 0.30       # of the plate side, matching AppMark
PLATE_INSET = 0.055       # of the canvas, the legacy icon's margin
GLYPH_HEIGHT = 0.52       # of the plate side
SAFE = 72.0 / 108.0       # the share of an adaptive layer a launcher shows

LEGACY = ((192, 'xxxhdpi'), (144, 'xxhdpi'), (96, 'xhdpi'), (72, 'hdpi'),
          (48, 'mdpi'))
ADAPTIVE = ((432, 'xxxhdpi'), (324, 'xxhdpi'), (216, 'xhdpi'), (162, 'hdpi'),
            (108, 'mdpi'))


def glyph_mask(box, height):
    """The icon glyph as an alpha mask on a square canvas, centred and `height`
    pixels tall. Measured and cropped rather than trusted to the font's own
    metrics, which carry side bearings the mark should not inherit."""
    probe = max(16, round(height * 1.6))
    font = ImageFont.truetype(FONT, probe)
    tmp = Image.new('L', (probe * 2, probe * 2), 0)
    ImageDraw.Draw(tmp).text((probe // 2, probe // 2), GLYPH, font=font,
                             fill=255)
    tmp = tmp.crop(tmp.getbbox())
    tmp = tmp.resize((max(1, round(tmp.width * height / tmp.height)),
                      max(1, round(height))), Image.LANCZOS)
    mask = Image.new('L', (box, box), 0)
    mask.paste(tmp, ((box - tmp.width) // 2, (box - tmp.height) // 2))
    return mask


def diagonal_gradient(size):
    """Top left to bottom right ramp, built small and scaled up so it is smooth
    without a per pixel loop over a megapixel."""
    small = Image.new('RGB', (96, 96))
    px = small.load()
    for y in range(96):
        for x in range(96):
            t = (x + y) / 190.0
            px[x, y] = tuple(round(a + (b - a) * t)
                             for a, b in zip(ROSE, ROSE_DEEP))
    return small.resize((size, size), Image.BILINEAR)


def specular(box, inset, side):
    """The soft light along the upper edge that AppMark paints, as a mask. A
    fading band, not a glow: it stays inside the plate and never blooms past
    the rim. The band's own outline is blurred, because at launcher size a hard
    edged highlight reads as a second shape sitting on the plate."""
    band = max(2, round(side * 0.28))
    shape = Image.new('L', (box, box), 0)
    ImageDraw.Draw(shape).rounded_rectangle(
        (inset + round(side * 0.20), inset + round(side * 0.03),
         box - inset - round(side * 0.20), inset + band),
        radius=round(band * 0.5), fill=255)
    shape = shape.filter(ImageFilter.GaussianBlur(max(1, side * 0.045)))
    ramp = Image.new('L', (1, box), 0)
    rpx = ramp.load()
    for y in range(box):
        t = (y - inset) / band
        rpx[0, y] = 0 if t < 0 or t > 1 else round(72 * (1 - t) ** 2.2)
    return Image.composite(ramp.resize((box, box)),
                           Image.new('L', (box, box), 0), shape)


def composed_mark(box, ss=2):
    """Plate plus glyph, filling the canvas with a small margin. Used for the
    legacy launcher icon and for the pre Android 12 splash window."""
    s = box * ss
    inset = round(s * PLATE_INSET)
    side = s - inset * 2
    mask = Image.new('L', (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (inset, inset, s - inset - 1, s - inset - 1),
        radius=round(side * PLATE_RADIUS), fill=255)

    out = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    out.paste(diagonal_gradient(s), (0, 0), mask)
    out.paste(Image.new('RGBA', (s, s), (255, 255, 255, 255)), (0, 0),
              Image.composite(specular(s, inset, side),
                              Image.new('L', (s, s), 0), mask))
    out.paste(Image.new('RGBA', (s, s), INK + (255,)), (0, 0),
              glyph_mask(s, side * GLYPH_HEIGHT))
    return out.resize((box, box), Image.LANCZOS)


def foreground(box, colour, ss=2):
    """An adaptive icon foreground: the glyph alone, sized against the 72 unit
    region a launcher is guaranteed to show rather than the full 108."""
    s = box * ss
    out = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    out.paste(Image.new('RGBA', (s, s), colour + (255,)), (0, 0),
              glyph_mask(s, s * SAFE * GLYPH_HEIGHT))
    return out.resize((box, box), Image.LANCZOS)


# ── Output ──────────────────────────────────────────────────────────────────

ADAPTIVE_ICON = '''<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by tool/gen_icons.py. Edit the script, not this file. -->
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@drawable/ic_launcher_background" />
    <foreground android:drawable="@mipmap/ic_launcher_foreground" />
    <monochrome android:drawable="@mipmap/ic_launcher_monochrome" />
</adaptive-icon>
'''

BACKGROUND = '''<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by tool/gen_icons.py. Edit the script, not this file. -->
<!-- Full bleed, so a launcher can mask it to whatever shape it likes. -->
<vector xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:aapt="http://schemas.android.com/aapt"
    android:width="108dp"
    android:height="108dp"
    android:viewportWidth="108"
    android:viewportHeight="108">
    <path android:pathData="M0,0h108v108h-108z">
        <aapt:attr name="android:fillColor">
            <gradient
                android:type="linear"
                android:startX="0"
                android:startY="0"
                android:endX="108"
                android:endY="108">
                <item android:offset="0" android:color="#FFDFA5B4" />
                <item android:offset="1" android:color="#FFC98FA8" />
            </gradient>
        </aapt:attr>
    </path>
</vector>
'''


def save(image, path):
    full = os.path.join(RES, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    image.save(full, optimize=True)
    print('wrote', path)


def write(path, body):
    full = os.path.join(RES, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, 'w', encoding='utf-8', newline='\n') as f:
        f.write(body)
    print('wrote', path)


def drop(path):
    full = os.path.join(RES, path)
    if os.path.exists(full):
        os.remove(full)
        print('removed', path)


def ladder(master, sizes, name):
    """Writes one image down a density ladder, largest first so every smaller
    size is a filtered reduction of the same render."""
    for size, density in sizes:
        image = master if size == master.width else master.resize(
            (size, size), Image.LANCZOS)
        save(image, 'mipmap-%s/%s.png' % (density, name))


def main():
    ladder(composed_mark(LEGACY[0][0]), LEGACY, 'ic_launcher')
    ladder(foreground(ADAPTIVE[0][0], INK), ADAPTIVE, 'ic_launcher_foreground')
    ladder(foreground(ADAPTIVE[0][0], (255, 255, 255)), ADAPTIVE,
           'ic_launcher_monochrome')

    # The composed mark again, at a fixed size, for the layer list the launch
    # window uses before Android 12 hands over to its own splash screen.
    save(composed_mark(384), 'drawable-nodpi/splash_mark.png')

    write('mipmap-anydpi-v26/ic_launcher.xml', ADAPTIVE_ICON)
    write('drawable/ic_launcher_background.xml', BACKGROUND)

    # Superseded by the raster foreground and the launch_background layer list.
    for stale in ('drawable/ic_launcher_foreground.xml',
                  'drawable/ic_launcher_monochrome.xml',
                  'drawable/splash_mark.xml',
                  'drawable-v21/launch_background.xml'):
        drop(stale)


if __name__ == '__main__':
    main()
