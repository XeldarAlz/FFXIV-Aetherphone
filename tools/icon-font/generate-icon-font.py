"""Builds src/Aetherphone/Fonts/TablerIcons.ttf from Tabler Icons (MIT).

The phone bakes icon glyphs through FontService.BuildIconHandle, which merges
this font on top of FontAwesome and only learns codepoints inside
[FirstIconCodepoint, LastIconCodepoint]. Tabler ships outline and filled as two
fonts whose native codepoints overlap FontAwesome (filled sits at U+F669..U+FECF),
so every glyph is remapped into BASE.. below, a gap above FontAwesome 6's
U+E0xx-U+E5xx additions and below its classic U+F000 block.

Run: python3 generate-icon-font.py   (needs fonttools)
"""

import io
import os
import re
import sys
import tarfile
import urllib.request
from fontTools import subset
from fontTools.merge import Merger
from fontTools.ttLib import TTFont

VERSION = "3.46.0"
BASE = 0xE600
TARBALL = (
    "https://registry.npmjs.org/@tabler/icons-webfont/-/"
    f"icons-webfont-{VERSION}.tgz"
)
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_FONT = os.path.join(ROOT, "src", "Aetherphone", "Fonts", "TablerIcons.ttf")
OUT_CS = os.path.join(ROOT, "src", "Aetherphone", "Windows", "Components", "Primitives", "PhoneIcons.cs")

OUTLINE = [
    ("AdjustmentsHorizontal", "adjustments-horizontal"),
    ("Bell", "bell"),
    ("Camera", "camera"),
    ("Check", "check"),
    ("ChevronLeft", "chevron-left"),
    ("ChevronRight", "chevron-right"),
    ("Clock", "clock"),
    ("Dots", "dots"),
    ("EyeOff", "eye-off"),
    ("Feather", "feather"),
    ("Heart", "heart"),
    ("Home", "home"),
    ("MessageCircle", "message-circle"),
    ("MoodPlus", "mood-plus"),
    ("MoodSmile", "mood-smile"),
    ("Photo", "photo"),
    ("Pin", "pin"),
    ("Plus", "plus"),
    ("Quote", "quote"),
    ("Refresh", "refresh"),
    ("Repeat", "repeat"),
    ("Search", "search"),
    ("Share", "share-3"),
    ("User", "user"),
    ("X", "x"),
    ("Send", "send"),
    ("Bookmark", "bookmark"),
    ("Menu", "menu-2"),
    ("UserSquareRounded", "user-square-rounded"),
    ("ChevronDown", "chevron-down"),
    ("Lock", "lock"),
    ("Edit", "edit"),
    ("UserPlus", "user-plus"),
    ("Settings", "settings"),
    ("Copy", "copy"),
    ("SquareRoundedPlus", "square-rounded-plus"),
    ("Trash", "trash"),
    ("Flag", "flag"),
    ("Ban", "ban"),
    ("Language", "language"),
    ("World", "world"),
    ("LockOpen", "lock-open"),
]

FILLED = [
    ("BellFilled", "bell"),
    ("HeartFilled", "heart"),
    ("HomeFilled", "home"),
    ("PinFilled", "pin"),
    ("UserFilled", "user"),
    ("BookmarkFilled", "bookmark"),
    ("MessageCircleFilled", "message-circle"),
    ("SendFilled", "send"),
]


_archive = None


def fetch(path):
    global _archive
    if _archive is None:
        with urllib.request.urlopen(TARBALL, timeout=180) as response:
            _archive = tarfile.open(fileobj=io.BytesIO(response.read()), mode="r:gz")

    member = _archive.extractfile(f"package/dist/{path}")
    if member is None:
        raise SystemExit(f"not in tarball: package/dist/{path}")

    return member.read()


def codepoints(css_name):
    css = fetch(f"{css_name}.css").decode("utf-8")
    return {
        name: int(value, 16)
        for name, value in re.findall(
            r"\.ti-([a-z0-9-]+):before\s*\{\s*content:\s*\"\\([0-9a-fA-F]+)\"", css
        )
    }


def build_part(font_name, css_name, entries, assigned, work):
    table = codepoints(css_name)
    missing = [icon for _, icon in entries if icon not in table]
    if missing:
        raise SystemExit(f"{css_name}: missing {missing}")

    source = os.path.join(work, f"{font_name}.ttf")
    with open(source, "wb") as handle:
        handle.write(fetch(f"fonts/{font_name}.ttf"))

    wanted = [table[icon] for _, icon in entries]
    options = subset.Options()
    options.name_IDs = ["*"]
    options.notdef_outline = True
    options.recalc_bounds = True
    font = subset.load_font(source, options)
    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=wanted)
    subsetter.subset(font)

    reverse = {table[icon]: assigned[label] for label, icon in entries}
    for table_entry in font["cmap"].tables:
        table_entry.cmap = {
            reverse[old]: glyph
            for old, glyph in table_entry.cmap.items()
            if old in reverse
        }

    target = os.path.join(work, f"{font_name}-remapped.ttf")
    subset.save_font(font, target, options)
    return target


def main():
    work = os.path.join(HERE, "build")
    os.makedirs(work, exist_ok=True)
    ordered = [label for label, _ in OUTLINE] + [label for label, _ in FILLED]
    assigned = {label: BASE + index for index, label in enumerate(ordered)}

    outline = build_part("tabler-icons", "tabler-icons", OUTLINE, assigned, work)
    filled = build_part("tabler-icons-filled", "tabler-icons-filled", FILLED, assigned, work)

    merger = Merger()
    merged = merger.merge([outline, filled])
    merged["name"].setName("Tabler Icons Subset", 1, 3, 1, 0x409)
    merged["name"].setName("Regular", 2, 3, 1, 0x409)
    os.makedirs(os.path.dirname(OUT_FONT), exist_ok=True)
    merged.save(OUT_FONT)

    lines = [
        "namespace Aetherphone.Windows.Components;",
        "",
        "internal static class PhoneIcons",
        "{",
    ]
    for label, _ in OUTLINE + FILLED:
        point = format(assigned[label], "04X")
        lines.append("    public const string " + label + " = \"\\u" + point + "\";")
    lines.append("}")
    with io.open(OUT_CS, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")

    size = os.path.getsize(OUT_FONT)
    print(f"wrote {OUT_FONT} ({size / 1024:.1f} KB, {len(ordered)} glyphs)")
    print(f"wrote {OUT_CS}")
    print(f"range U+{BASE:04X}..U+{BASE + len(ordered) - 1:04X}")


if __name__ == "__main__":
    sys.exit(main())
