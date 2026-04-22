"""
Qwen3-TTS Voice Cloning — per-language reference mapping.

Nutzt pro Sprache ein eigenes Ref-Audio (Zero-Shot Clone statt Cross-Lingual),
um Akzent-Artefakte zu vermeiden.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import soundfile as sf
import torch
from qwen_tts import Qwen3TTSModel

HERE = Path(__file__).parent
SAMPLES = HERE / "samples"
OUTPUTS = HERE / "outputs"

# Language -> (ref_audio, ref_text_file, qwen_language_code, out_prefix)
LANG_CONFIG = {
    "en": (SAMPLES / "jarvis_ref.wav",    SAMPLES / "jarvis_ref.txt",    "English", "jarvis_en"),
    "de": (SAMPLES / "jarvis_de_ref.wav", SAMPLES / "jarvis_de_ref.txt", "German",  "jarvis_de"),
}

TEST_SENTENCES = {
    "en": [
        "Welcome home, sir. All systems are operating within nominal parameters.",
        "Arc reactor output is holding at ninety-seven percent.",
        "Sir, I would strongly advise against that course of action.",
    ],
    "de": [
        "Willkommen zu Hause, Sir. Alle Systeme arbeiten im Normalbereich.",
        "Der Bogenreaktor laeuft bei siebenundneunzig Prozent.",
        "Sir, ich muss Ihnen dringend von diesem Vorgehen abraten.",
    ],
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--model", default="Qwen/Qwen3-TTS-12Hz-1.7B-Base",
        help="HF model id. Use 0.6B variant if VRAM-tight.",
    )
    parser.add_argument("--no_flash_attn", action="store_true")
    parser.add_argument(
        "--langs", nargs="+", default=["en", "de"],
        help="Which languages to generate (default: en de).",
    )
    args = parser.parse_args()

    OUTPUTS.mkdir(exist_ok=True)

    print(f"[load] {args.model}")
    model = Qwen3TTSModel.from_pretrained(
        args.model,
        device_map="cuda:0",
        dtype=torch.bfloat16,
        attn_implementation="eager" if args.no_flash_attn else "flash_attention_2",
    )

    for lang in args.langs:
        if lang not in LANG_CONFIG:
            print(f"[skip] unknown language '{lang}'")
            continue
        ref_audio, ref_text_file, qwen_lang, prefix = LANG_CONFIG[lang]
        if not ref_audio.exists() or not ref_text_file.exists():
            print(f"[skip] missing files for '{lang}': {ref_audio}, {ref_text_file}")
            continue
        ref_text = ref_text_file.read_text(encoding="utf-8").strip()
        print(f"\n[lang={lang}] ref={ref_audio.name} ({qwen_lang})")

        for i, sentence in enumerate(TEST_SENTENCES[lang], 1):
            print(f"[gen] {sentence[:60]}...")
            wavs, sr = model.generate_voice_clone(
                text=sentence, language=qwen_lang,
                ref_audio=str(ref_audio), ref_text=ref_text,
            )
            out = OUTPUTS / f"{prefix}_{i:02d}.wav"
            sf.write(str(out), wavs[0], sr)
            print(f"      -> {out.name}")

    print(f"\nDone. Listen in {OUTPUTS}/")


if __name__ == "__main__":
    main()
