import base64
import io
import os
import sys
import urllib.parse
import urllib.request
import zipfile

import lameenc
import numpy as np
import pyloudnorm
import soundfile
from scipy import signal

SAMPLE_RATE = 48000
HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, ".cache")
DEFAULT_OUTPUT = os.path.normpath(os.path.join(HERE, "..", "..", "src", "Aetherphone", "Sounds"))

MATERIAL_ROOT = "https://archive.org/download/material-design-sound-resources/material_product_sounds/wav/"
AOSP_COMMIT = "1cdfff555f4a21f71ccc978290e2e212e2f8b168"
AOSP_ROOT = f"https://android.googlesource.com/platform/frameworks/base/+/{AOSP_COMMIT}/data/sounds/"
KENNEY_CASINO = "https://kenney.nl/media/pages/assets/casino-audio/2472606a04-1721639069/kenney_casino-audio.zip"
KENNEY_INTERFACE = "https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip"

MATERIAL_FOLDERS = {
    "hero": "01 Hero Sounds",
    "alert": "02 Alerts and Notifications",
    "primary": "03 Primary System Sounds",
}

LOUDNESS_TARGETS = {
    "tick": -24.0,
    "key": -23.0,
    "air": -27.0,
    "event": -20.0,
    "chime": -19.0,
    "game": -19.0,
    "loop": -24.0,
    "notification": -15.0,
}

RINGTONE_LUFS = -16.0
PARTIAL_LENGTH = 4.0
PEAK_CEILING = 10 ** (-1.0 / 20)


def fetch(url, name):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        with urllib.request.urlopen(url, timeout=120) as response:
            data = response.read()
        if url.endswith("?format=TEXT"):
            data = base64.b64decode(data)
        with open(path, "wb") as handle:
            handle.write(data)
    return path


def read_audio(path, start=None, length=None):
    data, rate = soundfile.read(path, always_2d=True, dtype="float64")
    if rate != SAMPLE_RATE:
        data = signal.resample_poly(data, SAMPLE_RATE, rate, axis=0)
    if start is not None:
        data = data[int(start * SAMPLE_RATE):]
    if length is not None:
        data = data[:int(length * SAMPLE_RATE)]
    return data


def material(group, name):
    folder = urllib.parse.quote(MATERIAL_FOLDERS[group])
    return read_audio(fetch(f"{MATERIAL_ROOT}{folder}/{name}.wav", f"material_{name}.wav"))


def aosp(folder, name):
    return read_audio(fetch(f"{AOSP_ROOT}{folder}/{name}.ogg?format=TEXT", f"aosp_{folder.replace('/', '_')}_{name}.ogg"))


def kenney(archive_url, archive_name, member, **window):
    archive = fetch(archive_url, archive_name)
    target = os.path.join(CACHE, archive_name[:-4] + "_" + member)
    if not os.path.exists(target):
        with zipfile.ZipFile(archive) as bundle:
            for entry in bundle.namelist():
                if entry.endswith("/" + member):
                    with open(target, "wb") as handle:
                        handle.write(bundle.read(entry))
                    break
            else:
                raise FileNotFoundError(member)
    return read_audio(target, **window)


def casino(member, **window):
    return kenney(KENNEY_CASINO, "kenney_casino.zip", member, **window)


def interface(member, **window):
    return kenney(KENNEY_INTERFACE, "kenney_interface.zip", member, **window)


def mono(data):
    return data.mean(axis=1) if data.ndim == 2 else data


def times(duration):
    return np.arange(int(duration * SAMPLE_RATE)) / SAMPLE_RATE


def silence(duration):
    return np.zeros(int(duration * SAMPLE_RATE))


def place(*layers):
    length = max(int(offset * SAMPLE_RATE) + len(layer) for offset, layer in layers)
    mix = np.zeros(length)
    for offset, layer in layers:
        start = int(offset * SAMPLE_RATE)
        mix[start:start + len(layer)] += layer
    return mix


def partial(frequency, decay, amplitude=1.0, duration=None, glide=1.0, glide_time=0.02, attack=0.0008, phase=0.0):
    duration = duration or PARTIAL_LENGTH
    time = times(duration)
    sweep = frequency * (1 + (glide - 1) * (1 - np.exp(-time / glide_time)))
    phase_curve = 2 * np.pi * np.cumsum(sweep) / SAMPLE_RATE + phase
    envelope = np.exp(-time / decay) * (1 - np.exp(-time / attack))
    return amplitude * np.sin(phase_curve) * envelope


def noise(duration, low, high, decay, amplitude=1.0, seed=0, attack=0.0004):
    generator = np.random.default_rng(seed)
    time = times(duration)
    raw = generator.standard_normal(len(time))
    sos = signal.butter(4, [low, min(high, SAMPLE_RATE / 2 - 100)], btype="bandpass", fs=SAMPLE_RATE, output="sos")
    shaped = signal.sosfilt(sos, raw)
    shaped /= np.max(np.abs(shaped)) + 1e-12
    envelope = np.exp(-time / decay) * (1 - np.exp(-time / attack))
    return amplitude * shaped * envelope


def swept_noise(duration, start_hz, end_hz, width_octaves, attack, seed):
    generator = np.random.default_rng(seed)
    raw = generator.standard_normal(int(duration * SAMPLE_RATE) + 4096)
    window = 1024
    frequencies, frames, spectrum = signal.stft(raw, fs=SAMPLE_RATE, nperseg=window, noverlap=window * 7 // 8)
    progress = np.clip(frames / duration, 0, 1)
    eased = progress * progress * (3 - 2 * progress)
    centers = start_hz * (end_hz / start_hz) ** eased
    octaves = np.log2(np.maximum(frequencies[:, None], 1.0) / centers[None, :])
    spectrum *= np.exp(-0.5 * (octaves / width_octaves) ** 2)
    _, shaped = signal.istft(spectrum, fs=SAMPLE_RATE, nperseg=window, noverlap=window * 7 // 8)
    shaped = shaped[:int(duration * SAMPLE_RATE)]
    time = times(duration)
    rise = np.clip(time / attack, 0, 1)
    envelope = np.sin(0.5 * np.pi * rise) ** 2 * np.cos(0.5 * np.pi * np.clip((time - attack) / (duration - attack), 0, 1)) ** 2
    shaped *= envelope
    return shaped / (np.max(np.abs(shaped)) + 1e-12)


def glide_tone(start_hz, end_hz, duration, harmonics=((1, 1.0),), curve=0.35, attack=0.5):
    time = times(duration)
    progress = time / duration
    sweep = start_hz * (end_hz / start_hz) ** (progress ** curve)
    phase = 2 * np.pi * np.cumsum(sweep) / SAMPLE_RATE
    body = sum(amplitude * np.sin(phase * ratio) for ratio, amplitude in harmonics)
    rise = np.sin(0.5 * np.pi * np.clip(progress / attack, 0, 1)) ** 2
    fall = np.cos(0.5 * np.pi * np.clip((progress - attack) / (1 - attack), 0, 1)) ** 2
    return body * rise * fall


def marimba(frequency, decay=0.13, brightness=1.0, mallet=0.25, seed=0):
    tone = (
        partial(frequency, decay, 1.0)
        + partial(frequency * 3.93, decay * 0.28, 0.32 * brightness)
        + partial(frequency * 9.24, decay * 0.09, 0.10 * brightness)
    )
    strike = noise(0.012, min(frequency * 2, 6000), 9000, 0.0015, mallet, seed)
    return place((0, tone), (0, strike))


def bell(frequency, decay=0.2, seed=0):
    tone = (
        partial(frequency, decay, 1.0)
        + partial(frequency * 2.76, decay * 0.55, 0.45)
        + partial(frequency * 5.40, decay * 0.32, 0.22)
        + partial(frequency * 8.93, decay * 0.18, 0.10)
    )
    strike = noise(0.006, 4000, 14000, 0.0008, 0.15, seed)
    return place((0, tone), (0, strike))


def room(clip, mix, decay=0.07, seed=7):
    generator = np.random.default_rng(seed)
    time = times(decay * 6)
    impulse = generator.standard_normal(len(time)) * np.exp(-time / decay)
    sos = signal.butter(2, [400, 9000], btype="bandpass", fs=SAMPLE_RATE, output="sos")
    impulse = signal.sosfilt(sos, impulse)
    impulse[:int(0.004 * SAMPLE_RATE)] = 0
    impulse /= np.sqrt(np.sum(impulse ** 2)) + 1e-12
    wet = signal.fftconvolve(clip, impulse)[:len(clip) + len(time)]
    dry = np.concatenate([clip, np.zeros(len(wet) - len(clip))])
    return dry + mix * wet * (np.max(np.abs(clip)) / (np.max(np.abs(wet)) + 1e-12))


def weighted(clip):
    high = signal.butter(2, 80, btype="highpass", fs=SAMPLE_RATE, output="sos")
    shelf_b, shelf_a = shelf_coefficients(1500, 4.0)
    return signal.lfilter(shelf_b, shelf_a, signal.sosfilt(high, clip, axis=0), axis=0)


def shelf_coefficients(frequency, gain_db):
    amplitude = 10 ** (gain_db / 40)
    omega = 2 * np.pi * frequency / SAMPLE_RATE
    alpha = np.sin(omega) / 2 * np.sqrt(2)
    cosine = np.cos(omega)
    root = 2 * np.sqrt(amplitude) * alpha
    b = [amplitude * ((amplitude + 1) + (amplitude - 1) * cosine + root),
         -2 * amplitude * ((amplitude - 1) + (amplitude + 1) * cosine),
         amplitude * ((amplitude + 1) + (amplitude - 1) * cosine - root)]
    a = [(amplitude + 1) - (amplitude - 1) * cosine + root,
         2 * ((amplitude - 1) - (amplitude + 1) * cosine),
         (amplitude + 1) - (amplitude - 1) * cosine - root]
    return np.array(b) / a[0], np.array(a) / a[0]


def short_loudness(clip):
    channel = mono(weighted(clip))
    window = int(0.03 * SAMPLE_RATE)
    if len(channel) <= window:
        return 20 * np.log10(np.sqrt(np.mean(channel ** 2)) + 1e-12)
    power = np.convolve(channel ** 2, np.ones(window) / window, mode="valid")
    return 10 * np.log10(np.max(power) + 1e-12)


def trim(clip, keep_tail=False, threshold_db=-30.0, tail_db=-54.0, preroll=0.003):
    level = np.abs(mono(clip))
    peak = np.max(level) + 1e-12
    above = np.where(level > peak * 10 ** (threshold_db / 20))[0]
    start = max(0, above[0] - int(preroll * SAMPLE_RATE)) if len(above) else 0
    end = len(level)
    if not keep_tail:
        audible = np.where(level > peak * 10 ** (tail_db / 20))[0]
        if len(audible):
            end = min(len(level), audible[-1] + int(0.004 * SAMPLE_RATE))
    return clip[start:end]


def fade(clip, fade_in=0.0008, fade_out=0.008):
    clip = clip.copy()
    count_in = min(len(clip) // 4, int(fade_in * SAMPLE_RATE))
    count_out = min(len(clip) // 3, int(fade_out * SAMPLE_RATE))
    if count_in > 0:
        ramp = np.sin(0.5 * np.pi * np.linspace(0, 1, count_in)) ** 2
        clip[:count_in] *= ramp if clip.ndim == 1 else ramp[:, None]
    if count_out > 0:
        ramp = np.cos(0.5 * np.pi * np.linspace(0, 1, count_out)) ** 2
        clip[-count_out:] *= ramp if clip.ndim == 1 else ramp[:, None]
    return clip


def master(clip, category, keep_tail=False, stereo=False):
    clip = np.asarray(clip, dtype=np.float64)
    if not stereo:
        clip = mono(clip)
    clip = fade(trim(clip, keep_tail=keep_tail))
    clip *= 10 ** ((LOUDNESS_TARGETS[category] - short_loudness(clip)) / 20)
    peak = np.max(np.abs(clip))
    if peak > PEAK_CEILING:
        clip *= PEAK_CEILING / peak
    return clip


def master_ringtone(clip):
    clip = fade(trim(clip, keep_tail=True), fade_out=0.02)
    meter = pyloudnorm.Meter(SAMPLE_RATE)
    clip = clip * 10 ** ((RINGTONE_LUFS - meter.integrated_loudness(clip)) / 20)
    peak = np.max(np.abs(clip))
    if peak > PEAK_CEILING:
        clip *= PEAK_CEILING / peak
    return clip


def dither(clip, seed):
    generator = np.random.default_rng(seed)
    lsb = 1 / 32768
    shape = clip.shape
    return clip + (generator.random(shape) - generator.random(shape)) * lsb


def write_wav(path, clip, seed):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    soundfile.write(path, np.clip(dither(clip, seed), -1, 1), SAMPLE_RATE, subtype="PCM_16")


def write_mp3(path, clip, seed):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if clip.ndim == 1:
        clip = np.stack([clip, clip], axis=1)
    pcm = (np.clip(dither(clip, seed), -1, 1) * 32767).astype("<i2")
    encoder = lameenc.Encoder()
    encoder.set_bit_rate(192)
    encoder.set_in_sample_rate(SAMPLE_RATE)
    encoder.set_channels(2)
    encoder.set_quality(2)
    with open(path, "wb") as handle:
        handle.write(encoder.encode(pcm.tobytes()) + encoder.flush())


def tick(scale=1.0, seed=0):
    body = (
        partial(2150 * scale, 0.0045, 1.0)
        + partial(4800 * scale, 0.0026, 0.45)
        + partial(7400 * scale, 0.0016, 0.2)
        + partial(950 * scale, 0.003, 0.25)
    )
    click = noise(0.004, 3000, 12000, 0.0006, 0.35, seed)
    return room(place((0, body), (0, click)), 0.05, decay=0.03, seed=seed)


def key_click(low, high, decay, thump_hz, seed, weight=1.0):
    snap = noise(0.02, low, high, decay, 3.0, seed, attack=0.0001)
    thump = partial(thump_hz, 0.004, 0.12 * weight, duration=0.06, attack=0.0002)
    return place((0, snap), (0, thump))


def switch_click(base, direction, seed):
    first = partial(base, 0.005, 1.0, glide=direction, glide_time=0.01) + partial(base * 2.3, 0.0028, 0.4)
    second = partial(base * (direction ** 2), 0.004, 0.55) + partial(base * 2.3 * direction, 0.002, 0.2)
    clicks = place((0, first), (0, noise(0.004, 2500, 11000, 0.0007, 0.3, seed)),
                   (0.018, second), (0.018, noise(0.003, 2500, 11000, 0.0006, 0.15, seed + 1)))
    return room(clicks, 0.06, decay=0.035, seed=seed)


def swish(start_hz, end_hz, duration, attack, seed, width=0.55):
    air = swept_noise(duration, start_hz, end_hz, width, attack, seed)
    return room(air, 0.12, decay=0.06, seed=seed)


def bloom(start_hz, end_hz, duration):
    return room(glide_tone(start_hz, end_hz, duration, ((1, 1.0), (2, 0.18), (3, 0.05))), 0.1, decay=0.05)


def lock_click():
    latch = (partial(1150, 0.006, 1.0) + partial(2750, 0.003, 0.5) + partial(190, 0.014, 0.35))
    catch = (partial(980, 0.005, 0.6) + partial(2300, 0.0025, 0.3))
    clicks = place((0, latch), (0, noise(0.005, 2000, 9000, 0.0012, 0.6, 31)),
                   (0.026, catch), (0.026, noise(0.004, 2000, 9000, 0.0009, 0.35, 32)))
    return room(clicks, 0.08, decay=0.04, seed=3)


def send_swoosh():
    air = swept_noise(0.24, 650, 6200, 0.5, 0.06, 41)
    whistle = 0.18 * glide_tone(900, 2500, 0.2, ((1, 1.0), (2, 0.1)), curve=0.6)
    return room(place((0, air), (0.02, whistle)), 0.14, decay=0.07, seed=4)


def receive_pop():
    pop = glide_tone(620, 1040, 0.11, ((1, 1.0), (2.76, 0.12)), curve=0.25, attack=0.2)
    ring = partial(1040, 0.06, 0.45, attack=0.004)
    return room(place((0, pop), (0.035, ring)), 0.1, decay=0.05, seed=5)


def two_note(maker, first, second, gap, second_level=0.85, **arguments):
    return room(place((0, maker(first, **arguments)), (gap, second_level * maker(second, **arguments))), 0.12, decay=0.08)


def arpeggio(maker, frequencies, gap, decay, rising_level=0.08):
    layers = []
    for index, frequency in enumerate(frequencies):
        layers.append((index * gap, (1 - rising_level * (len(frequencies) - 1 - index)) * maker(frequency, decay=decay, seed=index)))
    return room(place(*layers), 0.14, decay=0.09)


def muted_knock(frequency, seed):
    return marimba(frequency, decay=0.07, brightness=0.6, mallet=0.35, seed=seed)


def soft_hit(scale, seed):
    body = partial(480 * scale, 0.022, 1.0) + partial(1250 * scale, 0.008, 0.45) + partial(2900 * scale, 0.003, 0.18)
    return room(place((0, body), (0, noise(0.006, 1500, 8000, 0.001, 0.3, seed))), 0.06, decay=0.035, seed=seed)


def wood_knock(scale, seed):
    body = (partial(720 * scale, 0.03, 1.0) + partial(1850 * scale, 0.012, 0.5)
            + partial(3300 * scale, 0.005, 0.25) + partial(230 * scale, 0.02, 0.3))
    return room(place((0, body), (0, noise(0.008, 1800, 9000, 0.0013, 0.45, seed))), 0.07, decay=0.04, seed=seed)


def glass_break(scale, seed):
    generator = np.random.default_rng(seed)
    shards = [(0, partial(2600 * scale, 0.05, 0.8) + partial(5900 * scale, 0.02, 0.4))]
    for index in range(7):
        offset = 0.006 + generator.random() * 0.07
        frequency = (3000 + generator.random() * 5000) * scale
        shards.append((offset, partial(frequency, 0.02 + generator.random() * 0.03, 0.25 + generator.random() * 0.3)))
    shards.append((0, noise(0.12, 2500, 14000, 0.025, 0.55, seed)))
    return room(place(*shards), 0.1, decay=0.05, seed=seed)


def burst(scale, seed):
    thump = glide_tone(110 * scale, 42 * scale, 0.22, ((1, 1.0),), curve=0.5, attack=0.05)
    rumble = noise(0.55, 60, 1400, 0.12, 0.8, seed)
    crackle = noise(0.3, 2500, 9000, 0.05, 0.25, seed + 1)
    return room(place((0, thump), (0, rumble), (0.005, crackle)), 0.1, decay=0.1, seed=seed)


def bubble(base, seed):
    pop = glide_tone(base, base * 2.1, 0.07, ((1, 1.0), (2, 0.08)), curve=0.3, attack=0.15)
    return room(place((0, pop), (0, noise(0.004, 3000, 10000, 0.0006, 0.15, seed))), 0.08, decay=0.04, seed=seed)


def pew(start, seed):
    tone = glide_tone(start, start * 0.3, 0.11, ((1, 1.0), (2, 0.15)), curve=0.4, attack=0.08)
    return room(place((0, tone), (0, noise(0.03, 1500, 7000, 0.008, 0.15, seed))), 0.08, decay=0.04, seed=seed)


def spring(start, seed):
    return room(glide_tone(start, start * 2.2, 0.12, ((1, 1.0), (2, 0.12), (3, 0.04)), curve=0.5, attack=0.25), 0.08, decay=0.04, seed=seed)


def lowpass(clip, cutoff):
    return signal.sosfilt(signal.butter(2, cutoff, btype="lowpass", fs=SAMPLE_RATE, output="sos"), clip)


def band(clip, low, high):
    return signal.sosfilt(signal.butter(2, [low, high], btype="bandpass", fs=SAMPLE_RATE, output="sos"), clip)


def howl(pitch=1.0, length=0.55, seed=0):
    span = 2.6 * length
    time = times(span)
    progress = time / span
    contour = np.interp(progress, [0, 0.3, 0.65, 1.0], [250, 430, 460, 310]) * pitch
    vibrato = 1 + 0.012 * np.clip(progress / 0.6, 0, 1) * np.sin(2 * np.pi * 5 * time)
    voice = signal.sawtooth(2 * np.pi * np.cumsum(contour * vibrato) / SAMPLE_RATE)
    vowel = band(voice, 600, 1000) * (1 - progress) + band(voice, 350, 600) * progress + 0.35 * band(voice, 1050, 1400)
    breath = noise(span, 700, 1200, 10.0, 0.05, seed)
    envelope = np.interp(progress, [0, 0.025 / span, 0.25, 0.62, 1.0], [0, 0.3, 1, 1, 0])
    return room(lowpass((vowel + breath) * envelope, 1500), 0.35, decay=0.25, seed=seed)


def hoot(frequency, length, seed):
    tone = glide_tone(frequency, frequency * 0.93, length, ((1, 1.0), (2, 0.06)), curve=0.6, attack=0.3)
    breath = noise(length, frequency * 0.8, frequency * 2.5, length, 0.05, seed)
    return lowpass(tone + breath * tone, 1100)


def owl_hoot():
    calls = place((0, hoot(410, 0.3, 471)), (0.42, 0.7 * hoot(392, 0.16, 472)), (0.62, 0.62 * hoot(380, 0.24, 473)))
    return room(calls, 0.3, decay=0.2, seed=471)


def pack_chorus():
    return place((0, howl(1.0, 0.9, 401)), (0.8, 0.65 * howl(1.26, 0.8, 402)), (1.4, 0.45 * howl(0.84, 0.7, 403)))


def moon_chime(frequency, seed):
    tone = partial(frequency, 0.35, 1.0, attack=0.006) + partial(frequency * 2.76, 0.12, 0.12, attack=0.006)
    return room(tone, 0.22, decay=0.12, seed=seed)


def claw_swipe(seed):
    return room(swept_noise(0.14, 2600, 1400, 0.6, 0.012, seed), 0.08, decay=0.04, seed=seed)


def soft_knock(frequency, seed):
    body = partial(frequency, 0.03, 1.0, glide=0.64, glide_time=0.02, attack=0.004)
    return room(lowpass(body, 900), 0.05, decay=0.03, seed=seed)


def leaf_rustle(seed):
    generator = np.random.default_rng(seed)
    layers = []
    for index in range(4):
        offset = index * 0.045 + generator.random() * 0.02
        layers.append((offset, noise(0.07, 2400, 6000, 0.025, 0.6 + generator.random() * 0.4, seed + index)))
    return room(place(*layers), 0.1, decay=0.05, seed=seed)


def moon_rise():
    tone = partial(note("C6"), 0.25, 1.0, attack=0.006) + partial(note("C6") * 2.76, 0.08, 0.1, attack=0.006)
    return room(tone, 0.25, decay=0.12, seed=411)


def heartbeat():
    beat = lowpass(partial(62, 0.06, 1.0, duration=0.3, glide=0.65, glide_time=0.04, attack=0.008), 220)
    return place((0, beat), (0.17, 0.6 * beat))


def coffin_lid():
    body = partial(140, 0.04, 1.0, glide=0.63, glide_time=0.015, attack=0.004)
    wood = noise(0.1, 300, 550, 0.02, 0.5, 421)
    return room(place((0, body), (0, wood)), 0.08, decay=0.04, seed=421)


def bat_flutter(seed, beats=6, squeak=False):
    layers = []
    for index in range(beats):
        layers.append((index * 0.045, noise(0.022, 700, 1100, 0.012, 1.0 - index / (beats + 1), seed + index)))
    if squeak:
        layers.append((0.1, 0.25 * glide_tone(4200, 5200, 0.035, ((1, 1.0),), curve=0.5, attack=0.2)))
    return room(place(*layers), 0.12, decay=0.05, seed=seed)


def candle_ignite():
    return room(swept_noise(0.3, 600, 2400, 0.8, 0.03, 431), 0.12, decay=0.06, seed=431)


def organ_swell():
    span = 1.15
    time = times(span)
    chord = np.zeros(len(time))
    for index, name in enumerate(("D3", "F3", "A3", "D4")):
        frequency = note(name) * (1 + (index - 1.5) * 0.0012)
        phase = 2 * np.pi * frequency * time
        chord += np.sin(phase) + 0.33 * np.sin(3 * phase) + 0.2 * np.sin(5 * phase)
    envelope = np.interp(time, [0, 0.14, 0.39, span], [0, 1, 1, 0])
    return room(lowpass(chord * envelope, 900), 0.4, decay=0.3, seed=441)


def bat_swarm():
    layers = [(index * 0.18, bat_flutter(450 + index * 10, beats=8, squeak=True)) for index in range(3)]
    layers.append((0, 0.35 * swept_noise(1.3, 300, 1600, 0.9, 0.4, 461)))
    return place(*layers)


def spell_sparkle():
    layers = [(index * 0.04, (0.8 - index * 0.14) * partial(note(name), 0.1, 1.0, attack=0.003))
              for index, name in enumerate(("E6", "G#6", "B6", "E7"))]
    layers.append((0, noise(0.28, 5000, 11000, 0.09, 0.04, 481)))
    return room(place(*layers), 0.25, decay=0.12, seed=481)


def whisper():
    return room(swept_noise(0.32, 1800, 900, 0.7, 0.08, 491), 0.18, decay=0.08, seed=491)


def candle_flare():
    rush = swept_noise(0.55, 300, 1800, 0.9, 0.02, 501)
    warmth = lowpass(partial(note("A3"), 0.25, 0.5, duration=0.7, attack=0.05)
                     + partial(note("E4"), 0.22, 0.3, duration=0.7, attack=0.05), 900)
    crackle = place(*[(0.08 + index * 0.07, noise(0.012, 2000, 7000, 0.004, 0.25, 502 + index)) for index in range(4)])
    return room(place((0, rush), (0.04, warmth), (0, crackle)), 0.2, decay=0.1, seed=501)


def crystal_shimmer():
    span = 0.8
    time = times(span)
    base = note("A5")
    tone = sum(amplitude * np.sin(2 * np.pi * base * ratio * time) for ratio, amplitude in ((1, 1.0), (2.76, 0.25), (5.4, 0.08)))
    shimmer = 1 + 0.35 * np.sin(2 * np.pi * 7 * time)
    envelope = (1 - np.exp(-time / 0.01)) * np.exp(-time / 0.28)
    return room(tone * shimmer * envelope, 0.3, decay=0.15, seed=511)


def halloween_sounds():
    sounds = {}
    for index, frequency in enumerate((150, 158, 143), start=1):
        sounds[f"halloween_knock_{index}"] = (soft_knock(frequency, 300 + index), "tick")
    for index, frequency in enumerate((110, 116, 105), start=1):
        sounds[f"halloween_thump_{index}"] = (soft_knock(frequency, 310 + index), "tick")
    for index, name in enumerate(("G5", "A5", "C6", "D6"), start=1):
        sounds[f"halloween_chime_{index}"] = (moon_chime(note(name), 320 + index), "tick")
    sounds["halloween_claw"] = (claw_swipe(331), "air")
    sounds["halloween_rustle"] = (leaf_rustle(341), "air")
    sounds["halloween_rise"] = (moon_rise(), "tick")
    sounds["halloween_hoot"] = (owl_hoot(), "air")
    sounds["halloween_chorus"] = (pack_chorus(), "tick")
    sounds["halloween_heartbeat"] = (heartbeat(), "tick")
    sounds["halloween_coffin"] = (coffin_lid(), "tick")
    sounds["halloween_flutter"] = (bat_flutter(361), "air")
    sounds["halloween_ignite"] = (candle_ignite(), "air")
    sounds["halloween_organ"] = (organ_swell(), "air")
    sounds["halloween_swarm"] = (bat_swarm(), "tick")
    sounds["halloween_sparkle"] = (spell_sparkle(), "tick")
    sounds["halloween_whisper"] = (whisper(), "air")
    sounds["halloween_flare"] = (candle_flare(), "air")
    sounds["halloween_crystal"] = (crystal_shimmer(), "tick")
    return sounds


def ringback():
    tone_time = times(2.0)
    tone = 0.5 * (np.sin(2 * np.pi * 440 * tone_time) + np.sin(2 * np.pi * 480 * tone_time))
    tone = fade(tone, 0.012, 0.03)
    return np.concatenate([tone, silence(4.0)])


def note(name):
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + (octave - 4) * 12) / 12)


def ui_sounds(output):
    sounds = {"shutter": (read_audio(os.path.join(output, "Ui", "shutter.wav")), "event")}
    for index, scale in enumerate((1.0, 1.035, 0.97, 1.06, 0.945), start=1):
        sounds[f"tap_{index}"] = (tick(scale, seed=index), "tick")
    for index, scale in enumerate((1.0, 1.04, 0.965, 1.07, 0.94), start=1):
        sounds[f"type_{index}"] = (key_click(1600 * scale, 7500, 0.0009, 260 * scale, 320 + index), "key")
    sounds["type_delete"] = (key_click(1200, 6000, 0.0012, 220, 330, weight=1.4), "key")
    sounds["type_space"] = (key_click(900, 5000, 0.0015, 180, 331, weight=1.7), "key")
    sounds["toggle_on"] = (switch_click(2400, 1.08, 23), "event")
    sounds["toggle_off"] = (switch_click(2000, 0.93, 24), "event")
    sounds["refresh"] = (tick(1.18, seed=25), "tick")
    sounds["app_open"] = (swish(900, 3200, 0.2, 0.07, 26), "air")
    sounds["app_close"] = (swish(3000, 950, 0.18, 0.035, 27), "air")
    sounds["sheet_present"] = (swish(1500, 3000, 0.13, 0.05, 28, width=0.4), "air")
    sounds["sheet_dismiss"] = (swish(2900, 1500, 0.12, 0.03, 29, width=0.4), "air")
    sounds["island_expand"] = (bloom(700, 1080, 0.09), "air")
    sounds["island_collapse"] = (bloom(1080, 700, 0.09), "air")
    sounds["lock"] = (lock_click(), "event")
    sounds["send"] = (send_swoosh(), "event")
    sounds["receive"] = (receive_pop(), "event")
    sounds["success"] = (two_note(marimba, note("E6"), note("B6"), 0.09), "chime")
    sounds["coin"] = (two_note(bell, note("C7"), note("G7"), 0.07, decay=0.16), "chime")
    sounds["caution"] = (two_note(marimba, note("A5"), note("E5"), 0.11, brightness=0.8), "chime")
    sounds["blocked"] = (room(place((0, muted_knock(note("D4"), 51)), (0.09, 0.8 * muted_knock(note("D4"), 52))), 0.08), "event")
    sounds["call_connect"] = (two_note(marimba, note("C6"), note("G6"), 0.1, decay=0.12), "chime")
    sounds["call_end"] = (two_note(marimba, note("G5"), note("C5"), 0.1, decay=0.12), "chime")
    sounds["record_start"] = (bloom(820, 1320, 0.08), "event")
    sounds["record_cancel"] = (bloom(1320, 820, 0.08), "event")
    sounds["win"] = (material("hero", "hero_simple-celebration-01"), "chime")
    return sounds


def game_sounds():
    sounds = {}
    for index, scale in enumerate((1.0, 1.08, 0.93), start=1):
        sounds[f"hit_soft_{index}"] = (soft_hit(scale, 100 + index), "game")
        sounds[f"hit_wood_{index}"] = (wood_knock(scale, 110 + index), "game")
        sounds[f"break_{index}"] = (glass_break(scale, 120 + index), "game")
        sounds[f"jump_{index}"] = (spring(330 * scale, 130 + index), "game")
    for index, base in enumerate((480, 560, 640), start=1):
        sounds[f"pop_{index}"] = (bubble(base, 140 + index), "game")
    for index, scale in enumerate((1.0, 0.85), start=1):
        sounds[f"explosion_{index}"] = (burst(scale, 150 + index), "game")
    sounds["collect_1"] = (bell(note("C7"), decay=0.1, seed=161), "game")
    sounds["collect_2"] = (bell(note("E7"), decay=0.1, seed=162), "game")
    sounds["match_1"] = (two_note(marimba, note("G5"), note("D6"), 0.045, decay=0.11), "game")
    sounds["match_2"] = (two_note(marimba, note("A5"), note("E6"), 0.045, decay=0.11), "game")
    sounds["clear_1"] = (arpeggio(marimba, [note("C6"), note("E6"), note("G6"), note("C7")], 0.05, 0.14), "game")
    sounds["clear_2"] = (arpeggio(marimba, [note("D6"), note("F#6"), note("A6"), note("D7")], 0.05, 0.14), "game")
    sounds["powerup_1"] = (arpeggio(bell, [note("G5"), note("C6"), note("E6"), note("G6"), note("C7")], 0.04, 0.13), "game")
    sounds["powerup_2"] = (arpeggio(bell, [note("A5"), note("D6"), note("F#6"), note("A6"), note("D7")], 0.04, 0.13), "game")
    sounds["shoot_1"] = (pew(1700, 171), "game")
    sounds["shoot_2"] = (pew(1450, 172), "game")
    sounds["wrong_1"] = (room(place((0, muted_knock(note("E4"), 181)), (0.1, 0.85 * muted_knock(note("C4"), 182))), 0.08), "game")
    sounds["wrong_2"] = (room(muted_knock(note("C4"), 183), 0.08), "game")
    for index, name in enumerate(("E5", "A5", "C#6", "E6"), start=1):
        sounds[f"simon_{index}"] = (room(marimba(note(name), decay=0.2, brightness=0.8, seed=190 + index), 0.12, decay=0.08), "game")
    for index, member in enumerate(("card-place-2.ogg", "card-shove-4.ogg", "card-shove-2.ogg"), start=1):
        sounds[f"card_place_{index}"] = (casino(member), "game")
    for index, member in enumerate(("card-fan-1.ogg", "card-slide-1.ogg", "card-slide-6.ogg"), start=1):
        sounds[f"card_flip_{index}"] = (casino(member), "game")
    sounds["shuffle"] = (fade(trim(casino("card-shuffle.ogg"), keep_tail=True, threshold_db=-16.0)[:int(1.3 * SAMPLE_RATE)], fade_out=0.25), "game")
    for index, member in enumerate(("chip-lay-1.ogg", "chip-lay-2.ogg", "chip-lay-3.ogg"), start=1):
        sounds[f"piece_{index}"] = (casino(member), "game")
    for index, member in enumerate(("chips-stack-1.ogg", "chips-stack-3.ogg", "chips-stack-5.ogg"), start=1):
        sounds[f"chips_{index}"] = (casino(member), "game")
    for index, member in enumerate(("card-shove-1.ogg", "card-shove-2.ogg"), start=1):
        sounds[f"deal_{index}"] = (casino(member), "game")
    sounds["tick_1"] = (interface("tick_001.ogg"), "game")
    sounds["tick_2"] = (interface("tick_002.ogg"), "game")
    return sounds


NOTIFICATIONS = {
    "Chime": ("material", "alert", "notification_simple-01"),
    "Bloom": ("material", "alert", "notification_decorative-01"),
    "Note": ("material", "alert", "notification_simple-02"),
    "Glint": ("material", "alert", "notification_decorative-02"),
    "Beacon": ("material", "alert", "notification_high-intensity"),
    "Ping": ("material", "alert", "alert_simple"),
    "Ripple": ("aosp", "notifications/material/ogg", "Carme"),
    "Spark": ("aosp", "notifications/material/ogg", "Rhea"),
    "Drift": ("aosp", "notifications/material/ogg", "Io"),
    "Pulse": ("aosp", "notifications/material/ogg", "Europa"),
    "Halo": ("aosp", "notifications/material/ogg", "Tethys"),
    "Echo": ("aosp", "notifications/material/ogg", "Iapetus"),
}

ALARM_TONES = {
    "alarm": ("aosp", "alarms/material/ogg", "Carbon"),
    "timer": ("aosp", "alarms/material/ogg", "Timer"),
}

RINGTONES = {
    "Signal": ("material", "alert", "ringtone_minimal"),
    "Cascade": ("aosp", "ringtones/material/ogg", "Atria"),
    "Horizon": ("aosp", "ringtones/material/ogg", "Dione"),
    "Lumen": ("aosp", "ringtones/material/ogg", "Ganymede"),
    "Orbit": ("aosp", "ringtones/material/ogg", "Luna"),
    "Prism": ("aosp", "ringtones/material/ogg", "Phobos"),
    "Tide": ("aosp", "ringtones/material/ogg", "Sedna"),
    "Summit": ("aosp", "ringtones/material/ogg", "Umbriel"),
}


def load_source(source):
    kind, folder, name = source
    return material(folder, name) if kind == "material" else aosp(folder, name)


def main():
    output = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUTPUT
    seed = 0
    for folder, sounds in (("Ui", ui_sounds(output)), ("Games", game_sounds())):
        for name, (clip, category) in sounds.items():
            seed += 1
            write_wav(os.path.join(output, folder, name + ".wav"), master(clip, category), seed)
    write_wav(os.path.join(output, "Ui", "ringback.wav"), master(ringback(), "loop", keep_tail=True), 999)
    for index, (name, source) in enumerate(ALARM_TONES.items()):
        write_wav(os.path.join(output, "Ui", name + ".wav"), mono(master_ringtone(load_source(source))), 2000 + index)
    for name, source in NOTIFICATIONS.items():
        seed += 1
        write_mp3(os.path.join(output, "Notifications", name + ".mp3"), master(load_source(source), "notification", stereo=True), seed)
    for name, source in RINGTONES.items():
        seed += 1
        write_mp3(os.path.join(output, "Ringtones", name + ".mp3"), master_ringtone(load_source(source)), seed)
    for index, (name, (clip, category)) in enumerate(halloween_sounds().items()):
        write_wav(os.path.join(output, "Ui", name + ".wav"), master(clip, category), 3000 + index)


if __name__ == "__main__":
    main()
