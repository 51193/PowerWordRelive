#!/usr/bin/env python3
"""Silero VAD segmenter — reads PCM16 from stdin, writes segmented WAV files."""
import sys
import os
import io
import wave
import signal
import argparse
from datetime import datetime, timezone

signal.signal(signal.SIGPIPE, signal.SIG_DFL)

try:
    import torch
    import numpy as np
except ImportError as e:
    print(f"MISSING_DEPENDENCY: {e}", file=sys.stderr)
    sys.exit(1)

SAMPLE_RATE = 16000
FRAME_SAMPLES = 512
BYTES_PER_SAMPLE = 2
FRAME_BYTES = FRAME_SAMPLES * BYTES_PER_SAMPLE
FRAME_DURATION_MS = FRAME_SAMPLES / SAMPLE_RATE * 1000  # 32ms
VAD_THRESHOLD = 0.5


def safe_print(msg: str):
    try:
        print(msg, flush=True)
    except BrokenPipeError:
        os._exit(0)


def load_vad_model():
    buf = io.StringIO()
    old_stderr = sys.stderr
    sys.stderr = buf
    try:
        model, _ = torch.hub.load(
            repo_or_dir="snakers4/silero-vad",
            model="silero_vad",
            force_reload=False,
            trust_repo=True
        )
    finally:
        sys.stderr = old_stderr

    captured = buf.getvalue()
    if captured and ("error" in captured.lower() or "traceback" in captured.lower()):
        print(captured, file=sys.stderr)

    return model


def main():
    parser = argparse.ArgumentParser(description="Silero VAD segmenter")
    parser.add_argument("--output-dir", type=str, required=True)
    parser.add_argument("--silence-ms", type=int, default=800)
    parser.add_argument("--max-sec", type=int, default=120)
    parser.add_argument("--min-speech-ms", type=int, default=500)
    parser.add_argument("--no-speech-timeout", type=int, default=30)
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    model = load_vad_model()
    model.eval()

    silence_frames_needed = int(args.silence_ms / FRAME_DURATION_MS)
    min_speech_frames = int(args.min_speech_ms / FRAME_DURATION_MS)
    max_speech_frames = int(args.max_sec * 1000 / FRAME_DURATION_MS)

    speech_active = False
    speech_frames = 0
    silence_frames = 0
    total_frames = 0
    wav_file = None
    wav_path = None

    while True:
        raw = sys.stdin.buffer.read(FRAME_BYTES)
        if len(raw) < FRAME_BYTES:
            break

        frame = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
        tensor = torch.from_numpy(frame)
        prob = model(tensor, SAMPLE_RATE).item()

        is_speech = prob > VAD_THRESHOLD
        total_frames += 1

        if is_speech:
            silence_frames = 0
            if not speech_active:
                speech_active = True
                speech_frames = 0
                ts = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S_%f")[:22]
                wav_path = os.path.join(args.output_dir, f"{ts}.wav.tmp")
                wav_file = wave.open(wav_path, "wb")
                wav_file.setnchannels(1)
                wav_file.setsampwidth(BYTES_PER_SAMPLE)
                wav_file.setframerate(SAMPLE_RATE)

            if wav_file:
                wav_file.writeframes(raw)
            speech_frames += 1

            if speech_frames >= max_speech_frames:
                if wav_file:
                    wav_file.close()
                    wav_file = None
                safe_print(f"SEGMENT_COMPLETE {wav_path}")
                speech_active = False
                speech_frames = 0
                wav_path = None
        else:
            if speech_active:
                silence_frames += 1
                if wav_file:
                    wav_file.writeframes(raw)

                if silence_frames >= silence_frames_needed:
                    if wav_file:
                        wav_file.close()
                        wav_file = None

                    if speech_frames >= min_speech_frames:
                        safe_print(f"SEGMENT_COMPLETE {wav_path}")
                    else:
                        os.remove(wav_path)
                        safe_print(f"SEGMENT_TOO_SHORT {wav_path}")

                    speech_active = False
                    speech_frames = 0
                    total_frames = 0
                    wav_path = None

        if not speech_active and total_frames * FRAME_DURATION_MS > args.no_speech_timeout * 1000:
            safe_print("SILENCE_TIMEOUT")
            total_frames = 0

    if wav_file:
        wav_file.close()


if __name__ == "__main__":
    main()
