"""
Multimodal-Smoke-Test: Gemma 4 mit Audio-Input (STT-Ersatz) + Vision.

Sendet eine Audio-Datei und optional ein Bild an Gemma, laesst es als
Jarvis antworten.
"""
from __future__ import annotations

import argparse
import time
from pathlib import Path

import librosa
import torch
from PIL import Image
from transformers import (
    AutoModelForCausalLM,
    AutoProcessor,
    BitsAndBytesConfig,
)

HERE = Path(__file__).parent
PROMPT_FILE = HERE / "prompts" / "jarvis.md"
MODEL_ID = "google/gemma-4-E4B-it"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--audio", default="/mnt/c/git/MetaMove/ai-services/tts-qwen3/samples/jarvis_de_ref.wav",
                        help="Audio file to send to Gemma.")
    parser.add_argument("--image", default=None, help="Optional image path.")
    parser.add_argument("--question", default="Please transcribe what you hear, then briefly describe the speaker's tone.",
                        help="Prompt text to accompany the audio.")
    parser.add_argument("--max_tokens", type=int, default=300)
    args = parser.parse_args()

    system_prompt = PROMPT_FILE.read_text(encoding="utf-8")

    print(f"[load] {MODEL_ID}")
    bnb = BitsAndBytesConfig(
        load_in_4bit=True,
        bnb_4bit_quant_type="nf4",
        bnb_4bit_compute_dtype=torch.bfloat16,
        bnb_4bit_use_double_quant=True,
        llm_int8_skip_modules=[
            "model.audio_tower",
            "model.vision_tower",
            "model.embed_audio",
            "model.embed_vision",
            "lm_head",
        ],
    )
    model = AutoModelForCausalLM.from_pretrained(
        MODEL_ID,
        device_map="cuda:0",
        quantization_config=bnb,
        attn_implementation="sdpa",
    )
    processor = AutoProcessor.from_pretrained(MODEL_ID)

    # Load audio as float32 array at 16 kHz (typical ASR SR)
    print(f"[audio] loading {args.audio}")
    audio_array, sr = librosa.load(args.audio, sr=16000, mono=True)
    print(f"        {len(audio_array)/sr:.2f}s @ {sr} Hz")

    # Build multimodal message
    user_content = [{"type": "text", "text": args.question}]
    user_content.append({"type": "audio", "audio": audio_array})
    image_obj = None
    if args.image:
        print(f"[image] loading {args.image}")
        image_obj = Image.open(args.image).convert("RGB")
        user_content.append({"type": "image", "image": image_obj})

    messages = [
        {"role": "system", "content": [{"type": "text", "text": system_prompt}]},
        {"role": "user", "content": user_content},
    ]

    # Apply chat template + process audio/images
    inputs = processor.apply_chat_template(
        messages,
        add_generation_prompt=True,
        tokenize=True,
        return_dict=True,
        return_tensors="pt",
    ).to("cuda:0")

    print(f"[gen] input keys: {list(inputs.keys())}")
    t0 = time.time()
    with torch.inference_mode():
        out = model.generate(
            **inputs,
            max_new_tokens=args.max_tokens,
            do_sample=True,
            temperature=0.6,
            top_p=0.9,
            repetition_penalty=1.08,
            pad_token_id=processor.tokenizer.eos_token_id,
        )
    dt = time.time() - t0

    response = processor.tokenizer.decode(
        out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True,
    ).strip()

    print(f"\n=== response ({dt:.2f}s) ===")
    print(response)


if __name__ == "__main__":
    main()
