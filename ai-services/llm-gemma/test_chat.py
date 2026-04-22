"""
Smoke-Test fuer Gemma 4 E4B-it mit Jarvis-Persona.

Laedt Modell in 4bit (~5 GB VRAM), feuert 3 Test-Anfragen in EN/DE/RU,
checkt Persona-Treue und Sprach-Switching.
"""
from __future__ import annotations

import argparse
import time
from pathlib import Path

import torch
from transformers import AutoModelForCausalLM, AutoProcessor, BitsAndBytesConfig

HERE = Path(__file__).parent
PROMPT_FILE = HERE / "prompts" / "jarvis.md"

TEST_TURNS = [
    ("en", "What's the status?"),
    ("en", "I want to move the end effector."),
    ("de", "Was siehst du hier gerade?"),
    ("de", "Hol bitte die rote Schraube."),
    ("ru", "Статус?"),
    # Simulated safety event:
    ("sys", "safety_event: proximity_warning"),
]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", default="google/gemma-4-E4B-it")
    parser.add_argument("--no_quantize", action="store_true",
                        help="Load in bfloat16 instead of 4bit.")
    args = parser.parse_args()

    system_prompt = PROMPT_FILE.read_text(encoding="utf-8")

    # Gemma 4 hat head_dim > 256, wird von flash-attn-v2 nicht unterstuetzt.
    # sdpa (torch's native SDP Attention) ist auf 3080 Ti fast genauso schnell.
    attn_impl = "sdpa"

    if args.no_quantize:
        print(f"[load] {args.model} bfloat16 ({attn_impl})")
        model = AutoModelForCausalLM.from_pretrained(
            args.model,
            device_map="cuda:0",
            torch_dtype=torch.bfloat16,
            attn_implementation=attn_impl,
        )
    else:
        print(f"[load] {args.model} 4bit nf4 ({attn_impl})")
        bnb = BitsAndBytesConfig(
            load_in_4bit=True,
            bnb_4bit_quant_type="nf4",
            bnb_4bit_compute_dtype=torch.bfloat16,
            bnb_4bit_use_double_quant=True,
        )
        model = AutoModelForCausalLM.from_pretrained(
            args.model,
            device_map="cuda:0",
            quantization_config=bnb,
            attn_implementation=attn_impl,
        )

    processor = AutoProcessor.from_pretrained(args.model)
    tokenizer = processor.tokenizer if hasattr(processor, "tokenizer") else processor

    print("\n=== Jarvis persona test ===\n")
    messages = [{"role": "system", "content": system_prompt}]

    for lang, text in TEST_TURNS:
        prefix = "[sys]" if lang == "sys" else f"[{lang}]"
        print(f"\n{prefix} user: {text}")
        messages.append({"role": "user", "content": text})

        prompt = tokenizer.apply_chat_template(
            messages, tokenize=False, add_generation_prompt=True,
        )
        inputs = tokenizer(prompt, return_tensors="pt").to("cuda:0")

        t0 = time.time()
        with torch.inference_mode():
            out = model.generate(
                **inputs,
                max_new_tokens=200,
                do_sample=True,
                temperature=0.6,
                top_p=0.9,
                repetition_penalty=1.08,
                pad_token_id=tokenizer.eos_token_id,
            )
        dt = time.time() - t0

        resp = tokenizer.decode(
            out[0][inputs.input_ids.shape[1]:], skip_special_tokens=True,
        ).strip()
        print(f"     jarvis: {resp}")
        print(f"     ({dt:.1f}s)")

        messages.append({"role": "assistant", "content": resp})

    print("\n=== done ===")


if __name__ == "__main__":
    main()
