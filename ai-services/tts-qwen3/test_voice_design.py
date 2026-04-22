"""
VoiceDesign Experiment: Jarvis-artige Stimme aus Text-Beschreibung.

Laedt Qwen3-TTS-VoiceDesign und erzeugt Test-Saetze in verschiedenen Sprachen
aus einem detaillierten Prompt. Fuer Sprachen ohne Referenz-Sample.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import soundfile as sf
import torch
from qwen_tts import Qwen3TTSModel

HERE = Path(__file__).parent
OUTPUTS = HERE / "outputs"

# Jarvis-Prompt im vom Qwen-Blog gezeigten strukturierten Stil
JARVIS_INSTRUCT = (
    "gender: Male. "
    "pitch: Mid-low baritone, very stable, minimal inflection. "
    "speed: Slow to moderate, deliberate cadence with precise pauses between clauses. "
    "volume: Calm conversational level, never raised. "
    "age: Late 40s. "
    "clarity: Extremely articulate and precise pronunciation. "
    "fluency: Highly fluent, no hesitation, no filler words. "
    "accent: British English, Received Pronunciation. "
    "texture: Warm but restrained, smooth, slightly nasal resonance, subtle electronic undertone. "
    "emotion: Detached, calm, minimal affect, with an undertone of dry amusement. "
    "tone: Formal, dignified, understated, almost butler-like. "
    "personality: Intellectually superior but polite, deadpan, subtly sarcastic, unflappable AI assistant."
)

# (language_code, qwen_language_name, out_prefix, sentences)
LANG_TESTS = {
    "en": ("English", "jarvis_vd", [
        "Welcome home, sir. All systems are operating within nominal parameters.",
        "Arc reactor output is holding at ninety-seven percent.",
        "Sir, I would strongly advise against that course of action.",
    ]),
    "ru": ("Russian", "jarvis_ru", [
        "Добро пожаловать домой, сэр. Все системы работают в пределах нормы.",
        "Мощность дугового реактора держится на девяносто семи процентах.",
        "Сэр, я настоятельно рекомендую воздержаться от этого действия.",
    ]),
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--langs", nargs="+", default=["en", "ru"])
    args = parser.parse_args()

    print("[load] Qwen3-TTS-12Hz-1.7B-VoiceDesign")
    model = Qwen3TTSModel.from_pretrained(
        "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign",
        device_map="cuda:0",
        dtype=torch.bfloat16,
        attn_implementation="eager",
    )

    OUTPUTS.mkdir(exist_ok=True)
    print(f"\n[prompt]\n{JARVIS_INSTRUCT}\n")

    for lang in args.langs:
        if lang not in LANG_TESTS:
            print(f"[skip] unknown lang '{lang}'")
            continue
        qwen_lang, prefix, sentences = LANG_TESTS[lang]
        print(f"\n[lang={lang} ({qwen_lang})]")
        for i, text in enumerate(sentences, 1):
            print(f"[gen] {text[:60]}...")
            wavs, sr = model.generate_voice_design(
                text=text,
                language=qwen_lang,
                instruct=JARVIS_INSTRUCT,
            )
            out = OUTPUTS / f"{prefix}_{i:02d}.wav"
            sf.write(str(out), wavs[0], sr)
            print(f"      -> {out.name}")

    print(f"\nDone. Listen in {OUTPUTS}/")


if __name__ == "__main__":
    main()
