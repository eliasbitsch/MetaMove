"""
Post-Processing Chain fuer den "Jarvis-im-Computer"-Sound.

Nimmt die trockenen TTS-Outputs und wandelt sie in "AI-in-the-machine"-Sound.
Chain: HighPass -> Compressor -> Saturation -> Chorus -> Small Room Reverb

Erzeugt drei Varianten pro Input zum Vergleich:
  _helmet.wav   = Iron-Man-Helm-Intercom (stark gefiltert, kleiner Raum)
  _ceiling.wav  = Stark-Villa-Deckenlautsprecher (offener, groesserer Raum)
  _core.wav     = "Im Arc-Reactor" (maschinell, mehr Saturation)
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
import soundfile as sf
from pedalboard import (
    Pedalboard,
    HighpassFilter,
    LowpassFilter,
    PeakFilter,
    Compressor,
    Distortion,
    Chorus,
    Reverb,
    Gain,
)

HERE = Path(__file__).parent
OUTPUTS = HERE / "outputs"


def helmet_chain() -> Pedalboard:
    """Iron-Man-Helm-Intercom: eng gefiltert, Presence-Boost, mehr Raum, wenig Saturation."""
    return Pedalboard([
        HighpassFilter(cutoff_frequency_hz=300),
        LowpassFilter(cutoff_frequency_hz=5000),
        PeakFilter(cutoff_frequency_hz=2500, gain_db=4.0, q=1.2),
        Compressor(threshold_db=-22, ratio=6.0, attack_ms=2, release_ms=60),
        Distortion(drive_db=2),
        Chorus(rate_hz=0.5, depth=0.06, mix=0.08),
        Reverb(room_size=0.35, damping=0.55, wet_level=0.25, dry_level=0.78),
        Gain(gain_db=2.5),
    ])


def ceiling_chain() -> Pedalboard:
    """Stark-Villa-Deckenlautsprecher: offener, mehr Raum, weniger gefiltert."""
    return Pedalboard([
        HighpassFilter(cutoff_frequency_hz=120),
        LowpassFilter(cutoff_frequency_hz=9000),
        Compressor(threshold_db=-18, ratio=3.0, attack_ms=5, release_ms=120),
        Chorus(rate_hz=0.3, depth=0.05, mix=0.08),
        Reverb(room_size=0.45, damping=0.5, wet_level=0.25, dry_level=0.78),
        Gain(gain_db=1.5),
    ])


CHAINS = {
    "helmet": helmet_chain(),
    "ceiling": ceiling_chain(),
}


def process(src: Path, variant: str, board: Pedalboard) -> Path:
    audio, sr = sf.read(src)
    if audio.ndim > 1:
        audio = audio.mean(axis=1)
    audio = audio.astype(np.float32)
    processed = board(audio, sr)
    peak = np.max(np.abs(processed))
    if peak > 0.98:
        processed = processed * (0.98 / peak)
    out = src.with_name(f"{src.stem}_{variant}.wav")
    sf.write(out, processed, sr, subtype="PCM_16")
    return out


def main() -> None:
    dry_wavs = sorted(
        p for p in OUTPUTS.glob("*.wav")
        if not any(p.stem.endswith(f"_{v}") for v in CHAINS)
    )
    if not dry_wavs:
        raise SystemExit(f"No dry WAVs found in {OUTPUTS}")

    print(f"Processing {len(dry_wavs)} files x {len(CHAINS)} variants...")
    for src in dry_wavs:
        for variant, board in CHAINS.items():
            out = process(src, variant, board)
            print(f"  {src.name} -> {out.name}")

    print(f"\nDone. Compare in {OUTPUTS}/")
    print("Variants: _helmet = Iron-Man-Helm, _ceiling = Villa-Speakers, _core = Arc-Reactor")


if __name__ == "__main__":
    main()
