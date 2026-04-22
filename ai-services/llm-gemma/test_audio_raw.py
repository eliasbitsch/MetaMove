"""Minimaler Audio-Input Test ohne Jarvis-Prompt, um zu verifizieren dass Gemma 4 Audio tatsaechlich verarbeitet."""
from __future__ import annotations

import time

import librosa
import torch
from transformers import AutoModelForCausalLM, AutoProcessor, BitsAndBytesConfig

MODEL_ID = "google/gemma-4-E4B-it"
AUDIO = "/mnt/c/git/MetaMove/ai-services/tts-qwen3/samples/jarvis_de_ref.wav"

bnb = BitsAndBytesConfig(
    load_in_4bit=True, bnb_4bit_quant_type="nf4",
    bnb_4bit_compute_dtype=torch.bfloat16, bnb_4bit_use_double_quant=True,
    llm_int8_skip_modules=["model.audio_tower", "model.vision_tower",
                           "model.embed_audio", "model.embed_vision", "lm_head"],
)
model = AutoModelForCausalLM.from_pretrained(
    MODEL_ID, device_map="cuda:0", quantization_config=bnb, attn_implementation="sdpa",
)
processor = AutoProcessor.from_pretrained(MODEL_ID)

audio, sr = librosa.load(AUDIO, sr=16000, mono=True)
print(f"audio: {len(audio)/sr:.2f}s")

PROMPTS = [
    "Transcribe this audio verbatim.",
    "What language is being spoken in this audio?",
    "What is being said in this audio clip?",
    "Summarize the content of the audio.",
]

for p in PROMPTS:
    messages = [{
        "role": "user",
        "content": [
            {"type": "audio", "audio": audio},
            {"type": "text", "text": p},
        ],
    }]
    inputs = processor.apply_chat_template(
        messages, add_generation_prompt=True, tokenize=True,
        return_dict=True, return_tensors="pt",
    ).to("cuda:0")
    t0 = time.time()
    with torch.inference_mode():
        out = model.generate(
            **inputs, max_new_tokens=200, do_sample=False,
            pad_token_id=processor.tokenizer.eos_token_id,
        )
    resp = processor.tokenizer.decode(
        out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True,
    ).strip()
    print(f"\n[Q] {p}")
    print(f"[A] ({time.time()-t0:.1f}s) {resp}")
