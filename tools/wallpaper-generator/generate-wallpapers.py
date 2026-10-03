import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageFilter

WIDTH = 1290
HEIGHT = 2796
WORK_SCALE = 4
QUALITY = 88

PALETTES = {
    "Bloom": {
        "Light": {"base": ("#F6E3EE", "#E6D4F3"), "blobs": ["#F48FB1", "#B39DEB", "#FFC3A0", "#9ED0F6"], "ribbons": ["#FF9BC4", "#C8A6FF"], "gloss": 0.10},
        "Dark": {"base": ("#3B1A48", "#140A20"), "blobs": ["#C44A8E", "#7A4FD6", "#FF6F91", "#3F7BD9"], "ribbons": ["#FF7DB0", "#9C6CFF"], "gloss": 0.06},
    },
    "Current": {
        "Light": {"base": ("#DCEFFA", "#C4DCF4"), "blobs": ["#6FC3F0", "#5FD3C9", "#9EC9FF", "#86B0F0"], "ribbons": ["#7FE3FF", "#6BD6C3"], "gloss": 0.10},
        "Dark": {"base": ("#0F2B47", "#050E1C"), "blobs": ["#2A8DE0", "#1FBFB8", "#3B63D8", "#0FA3D1"], "ribbons": ["#4FC8FF", "#2EE6D0"], "gloss": 0.06},
    },
    "Ember": {
        "Light": {"base": ("#FDE6D3", "#F8CDB0"), "blobs": ["#FF9F6B", "#F6B07F", "#F27B8C", "#FFC46B"], "ribbons": ["#FFB36B", "#FF8C9C"], "gloss": 0.10},
        "Dark": {"base": ("#3A1512", "#150606"), "blobs": ["#E0532A", "#F2902F", "#B02F52", "#FF7A3C"], "ribbons": ["#FFA24A", "#FF5C7A"], "gloss": 0.06},
    },
    "Prism": {
        "Light": {"base": ("#E3F3EF", "#DEE0F7"), "blobs": ["#8FE9CC", "#B3A9F5", "#9CD3FF", "#FFB7DD"], "ribbons": ["#A6F0FF", "#D0B4FF"], "gloss": 0.10},
        "Dark": {"base": ("#1A2347", "#090B1C"), "blobs": ["#3A8DE0", "#8F55E3", "#22B3AA", "#D14A98"], "ribbons": ["#5EC8FF", "#B07CFF"], "gloss": 0.06},
    },
    "Crystal": {
        "Light": {"base": ("#E4ECFA", "#D6E4F6"), "blobs": ["#8FB4F5", "#A7E6E0", "#C2B6F7", "#7FD0F2"], "ribbons": ["#B9E8FF", "#9FB8FF"], "gloss": 0.12},
        "Dark": {"base": ("#141E46", "#060A1E"), "blobs": ["#3C5FD6", "#24A6A0", "#6A4FD0", "#2C8FD8"], "ribbons": ["#6FD2FF", "#7E8CFF"], "gloss": 0.06},
    },
    "Frost": {
        "Light": {"base": ("#EEF2F7", "#D9E2EE"), "blobs": ["#B4C7E0", "#CFE3F2", "#A9B9D8", "#DCE6F5"], "ribbons": ["#FFFFFF", "#C4D8F0"], "gloss": 0.14},
        "Dark": {"base": ("#16203A", "#05080F"), "blobs": ["#2B4470", "#3E6A9A", "#232F5C", "#4F7DB0"], "ribbons": ["#9CC4F0", "#5F86C4"], "gloss": 0.05},
    },
    "Grove": {
        "Light": {"base": ("#E6F2E4", "#D6E8D8"), "blobs": ["#9ED69A", "#C8E39A", "#7FCBB2", "#E6D99A"], "ribbons": ["#D4F0A8", "#9FE0C6"], "gloss": 0.10},
        "Dark": {"base": ("#132A1E", "#050F0A"), "blobs": ["#2E8A4E", "#7A9E2E", "#1F7A6A", "#A08A2E"], "ribbons": ["#8FD66B", "#4FC9A0"], "gloss": 0.06},
    },
    "Sunset": {
        "Light": {"base": ("#FCE9D6", "#F3D3E2"), "blobs": ["#FFC46B", "#F08DB4", "#7FD3D0", "#FFA38A"], "ribbons": ["#FFD58A", "#F7A8D0"], "gloss": 0.10},
        "Dark": {"base": ("#3A1838", "#0E0614"), "blobs": ["#E08A2A", "#C23A7A", "#1F8E96", "#E05A4A"], "ribbons": ["#FFB84A", "#FF6FA8"], "gloss": 0.06},
    },
}


def hex_to_rgb(value):
    value = value.lstrip("#")
    return np.array([int(value[index:index + 2], 16) for index in (0, 2, 4)], dtype=np.float32) / 255.0


def vertical_gradient(width, height, top, bottom):
    column = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None, None]
    return top[None, None, :] * (1.0 - column) + bottom[None, None, :] * column + np.zeros((height, width, 3), dtype=np.float32)


def blob(width, height, center_x, center_y, radius_x, radius_y, angle):
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    dx = xs - center_x
    dy = ys - center_y
    cos_a = math.cos(angle)
    sin_a = math.sin(angle)
    local_x = (dx * cos_a + dy * sin_a) / radius_x
    local_y = (-dx * sin_a + dy * cos_a) / radius_y
    distance = np.sqrt(local_x * local_x + local_y * local_y)
    falloff = np.clip(1.0 - distance, 0.0, 1.0)
    return falloff * falloff * (3.0 - 2.0 * falloff)


def gloss_band(width, height, strength):
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    diagonal = (xs / width) * 0.55 + (ys / height) * 0.45
    band = np.exp(-((diagonal - 0.38) ** 2) / (2.0 * 0.07 ** 2))
    return band[:, :, None] * strength


def render(name, variant, spec, seed):
    rng = random.Random(seed)
    width = WIDTH // WORK_SCALE
    height = HEIGHT // WORK_SCALE
    top, bottom = (hex_to_rgb(value) for value in spec["base"])
    canvas = vertical_gradient(width, height, top, bottom)
    colors = [hex_to_rgb(value) for value in spec["blobs"]]
    for index, color in enumerate(colors):
        center_x = width * rng.uniform(0.05, 0.95)
        center_y = height * rng.uniform(0.10, 0.90)
        radius_x = width * rng.uniform(0.5, 0.9)
        radius_y = height * rng.uniform(0.18, 0.32)
        angle = rng.uniform(-1.0, 1.0)
        mask = blob(width, height, center_x, center_y, radius_x, radius_y, angle)[:, :, None]
        weight = 1.0 if index < 2 else 0.85
        canvas = canvas * (1.0 - mask * weight) + color[None, None, :] * (mask * weight)
    canvas = np.clip(canvas, 0.0, 1.0)
    image = Image.fromarray((canvas * 255.0).astype(np.uint8), "RGB")
    image = image.filter(ImageFilter.GaussianBlur(radius=width * 0.045))
    field = np.asarray(image).astype(np.float32) / 255.0
    ribbons = field.copy()
    coverage = np.zeros((height, width, 1), dtype=np.float32)
    for color_hex in spec["ribbons"]:
        color = hex_to_rgb(color_hex)
        center_x = width * rng.uniform(0.2, 0.8)
        center_y = height * rng.uniform(0.2, 0.8)
        mask = blob(width, height, center_x, center_y, width * rng.uniform(0.9, 1.4), height * rng.uniform(0.05, 0.09), rng.uniform(-0.75, 0.75))[:, :, None]
        ribbons = ribbons * (1.0 - mask) + color[None, None, :] * mask
        coverage = np.maximum(coverage, mask)
    ribbon_image = Image.fromarray((np.clip(ribbons, 0.0, 1.0) * 255.0).astype(np.uint8), "RGB").filter(ImageFilter.GaussianBlur(radius=width * 0.012))
    coverage_image = Image.fromarray((np.clip(coverage[:, :, 0], 0.0, 1.0) * 255.0).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius=width * 0.012))
    ribbon_field = np.asarray(ribbon_image).astype(np.float32) / 255.0
    ribbon_mask = (np.asarray(coverage_image).astype(np.float32) / 255.0)[:, :, None] * 0.7
    field = field * (1.0 - ribbon_mask) + ribbon_field * ribbon_mask
    field = field + gloss_band(width, height, spec["gloss"]) * (1.0 - field)
    field = np.clip(field, 0.0, 1.0)
    image = Image.fromarray((field * 255.0).astype(np.uint8), "RGB")
    image = image.resize((WIDTH, HEIGHT), Image.BICUBIC)
    image = image.filter(ImageFilter.GaussianBlur(radius=4))
    return image


def main(argv):
    output_directory = argv[1] if len(argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "src", "Aetherphone", "Wallpapers")
    names = argv[2:] if len(argv) > 2 else list(PALETTES.keys())
    for name in names:
        for variant_index, (variant, spec) in enumerate(PALETTES[name].items()):
            seed = sum(ord(character) for character in name) * 7 + variant_index
            image = render(name, variant, spec, seed)
            path = os.path.join(output_directory, f"{name}{variant}.jpg")
            image.save(path, "JPEG", quality=QUALITY, optimize=True, progressive=True, subsampling=1)
            print(path, os.path.getsize(path) // 1024, "KB")


if __name__ == "__main__":
    main(sys.argv)
