import io
from typing import Optional

import torch
from fastapi import FastAPI, File, Form, UploadFile
from PIL import Image
from pydantic import BaseModel
from transformers import AutoModelForZeroShotObjectDetection, AutoProcessor

MODEL_ID = "IDEA-Research/grounding-dino-tiny"
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

app = FastAPI(title="GoHolo Perception", version="0.1.0")

processor = AutoProcessor.from_pretrained(MODEL_ID)
model = AutoModelForZeroShotObjectDetection.from_pretrained(MODEL_ID).to(DEVICE).eval()


class Box(BaseModel):
    label: str
    score: float
    xmin: float
    ymin: float
    xmax: float
    ymax: float


class GroundResult(BaseModel):
    image_width: int
    image_height: int
    boxes: list[Box]
    latency_ms: float


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "device": DEVICE, "model": MODEL_ID}


@app.post("/ground", response_model=GroundResult)
async def ground(
    image: UploadFile = File(...),
    query: str = Form(...),
    box_threshold: float = Form(0.35),
    text_threshold: float = Form(0.25),
) -> GroundResult:
    import time

    t0 = time.perf_counter()
    img = Image.open(io.BytesIO(await image.read())).convert("RGB")

    prompts = [q.strip().lower().rstrip(".") + "." for q in query.split(",") if q.strip()]
    prompt_text = " ".join(prompts)

    inputs = processor(images=img, text=prompt_text, return_tensors="pt").to(DEVICE)
    with torch.no_grad():
        outputs = model(**inputs)

    results = processor.post_process_grounded_object_detection(
        outputs,
        inputs.input_ids,
        box_threshold=box_threshold,
        text_threshold=text_threshold,
        target_sizes=[img.size[::-1]],
    )[0]

    boxes = [
        Box(
            label=label,
            score=float(score),
            xmin=float(b[0]),
            ymin=float(b[1]),
            xmax=float(b[2]),
            ymax=float(b[3]),
        )
        for score, label, b in zip(results["scores"], results["labels"], results["boxes"])
    ]

    return GroundResult(
        image_width=img.width,
        image_height=img.height,
        boxes=boxes,
        latency_ms=(time.perf_counter() - t0) * 1000,
    )
