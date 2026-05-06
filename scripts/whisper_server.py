#!/usr/bin/env python3
"""Whisper 转录服务器 — 常驻进程，stdin/stdout JSON 行通信。"""
import json
import os
import sys
import time
import warnings
import wave
from pathlib import Path

warnings.filterwarnings("ignore")

import tqdm

tqdm.tqdm.disable = True


def _suppress_stderr():
    """临时重定向 stderr 以屏蔽 whisper 内部的 tqdm 进度条输出。"""
    devnull = open(os.devnull, "w")
    old_stderr = sys.stderr
    sys.stderr = devnull
    return old_stderr, devnull


def _restore_stderr(old_stderr, devnull):
    sys.stderr = old_stderr
    devnull.close()


def run_server():
    import argparse

    parser = argparse.ArgumentParser(description="Whisper transcription server")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--model", default="turbo")
    parser.add_argument("--device", default="cuda")
    parser.add_argument("--initial-prompt", default="简体中文")
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    import torch

    device = args.device
    if device == "cuda" and not torch.cuda.is_available():
        print("CUDA not available, falling back to CPU", file=sys.stderr)
        device = "cpu"

    import whisper

    model = whisper.load_model(args.model, device=device)

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        try:
            req = json.loads(line)
        except json.JSONDecodeError:
            continue

        input_path = req["input"]
        t_start = time.time()

        try:
            old_stderr, devnull = _suppress_stderr()
            try:
                result = model.transcribe(
                    input_path,
                    language="zh",
                    initial_prompt=args.initial_prompt,
                    verbose=False,
                )
            finally:
                _restore_stderr(old_stderr, devnull)
        except Exception as e:
            resp = {"error": str(e)}
            print(json.dumps(resp, ensure_ascii=False), flush=True)
            continue

        input_stem = Path(input_path).stem
        if input_stem.endswith(".wav"):
            input_stem = input_stem[:-4]

        writer = whisper.utils.get_writer("srt", args.output_dir)
        writer(result, input_stem)

        with wave.open(input_path, "rb") as wf:
            audio_duration = wf.getnframes() / wf.getframerate()

        elapsed = time.time() - t_start
        resp = {
            "status": "ok",
            "audio_duration_s": round(audio_duration, 3),
            "elapsed_s": round(elapsed, 3),
            "speed": round(audio_duration / elapsed, 2) if elapsed > 0 else 0.0,
        }
        print(json.dumps(resp, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    run_server()
