#!/usr/bin/env python3
"""Speaker diarization — splits WAV by speaker, matches persisted embeddings."""

import argparse
import json
import os
import shutil
import subprocess
import sys
import warnings
from pathlib import Path

import numpy as np

warnings.filterwarnings("ignore")

MAX_EMBEDDINGS_PER_SPEAKER = 5
REF_MIN_SIMILARITY = 0.30
REF_NORM_RATIO = 1.5


def cosine_similarity(a: np.ndarray, b: np.ndarray) -> float:
    na = np.linalg.norm(a)
    nb = np.linalg.norm(b)
    if na < 1e-10 or nb < 1e-10:
        return 0.0
    return float(np.dot(a, b) / (na * nb))


def load_embeddings(emb_dir: str) -> tuple[dict[str, list[np.ndarray]], int]:
    known: dict[str, list[np.ndarray]] = {}
    if not emb_dir or not os.path.isdir(emb_dir):
        return known, 0

    for npy_file in sorted(Path(emb_dir).glob("speaker_*.npy")):
        try:
            emb = np.load(str(npy_file))
            speaker_id = npy_file.stem
            known[speaker_id] = [emb]
        except Exception:
            pass

    next_id = 0
    if known:
        ids = [int(sid.split("_")[-1]) for sid in known]
        next_id = max(ids) + 1

    return known, next_id


def match_speaker(
    embedding: np.ndarray,
    known: dict[str, list[np.ndarray]],
    next_id: int,
    threshold: float,
) -> tuple[str, int]:
    emb_norm = float(np.linalg.norm(embedding))
    if emb_norm < 0.01 or np.isnan(emb_norm):
        new_id = f"speaker_{next_id}"
        return new_id, next_id + 1

    best_speaker = None
    best_score = -1.0

    for speaker_id, ref_list in list(known.items()):
        if not ref_list:
            continue
        max_s = max(cosine_similarity(embedding, ref) for ref in ref_list)
        if max_s > best_score:
            best_score = max_s
            best_speaker = speaker_id

    if best_speaker is not None and best_score >= threshold:
        refs = known[best_speaker]
        refs.append(embedding)
        if len(refs) > MAX_EMBEDDINGS_PER_SPEAKER:
            refs.pop(0)
        return best_speaker, next_id

    if best_score >= REF_MIN_SIMILARITY and best_speaker is not None:
        refs = known[best_speaker]
        ref_norms = [float(np.linalg.norm(r)) for r in refs]
        avg_ref_norm = sum(ref_norms) / len(ref_norms) if ref_norms else 0.0
        if emb_norm > avg_ref_norm * REF_NORM_RATIO:
            known[best_speaker] = [embedding]
            return best_speaker, next_id

    new_id = f"speaker_{next_id}"
    return new_id, next_id + 1


def persist_embedding(speaker_id: str, embedding: np.ndarray, emb_dir: str):
    if not emb_dir:
        return
    os.makedirs(emb_dir, exist_ok=True)
    npy_path = os.path.join(emb_dir, f"{speaker_id}.npy")
    try:
        np.save(npy_path, embedding)
    except Exception as e:
        print(f"Failed to persist embedding: {e}", file=sys.stderr)


def extract_audio(input_path: str, output_path: str, start: float, end: float):
    duration = end - start
    cmd = [
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-ss", str(start), "-i", input_path,
        "-t", str(duration), "-c", "copy",
        output_path
    ]
    subprocess.run(cmd, check=True)


def main():
    parser = argparse.ArgumentParser(description="Speaker diarization")
    parser.add_argument("--input", required=True, help="Input WAV file")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--embeddings-dir", required=True)
    parser.add_argument("--hf-token", default="")
    parser.add_argument("--match-threshold", type=float, default=0.55)
    parser.add_argument("--device", default="cpu")
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    from pyannote.audio import Pipeline

    pipeline = Pipeline.from_pretrained(
        "pyannote/speaker-diarization-3.1",
        token=args.hf_token if args.hf_token else None
    )

    if args.device != "cpu":
        import torch
        pipeline.to(torch.device(args.device))

    output = pipeline(args.input)
    diarization = output.speaker_diarization
    exclusive = output.exclusive_speaker_diarization
    embeddings = output.speaker_embeddings

    known, next_id = load_embeddings(args.embeddings_dir)

    labels = diarization.labels()
    emb_map: dict[str, np.ndarray] = {}
    if embeddings is not None and len(embeddings) > 0 and len(labels) > 0:
        for s_idx, label in enumerate(labels):
            if s_idx < embeddings.shape[0]:
                emb_map[label] = embeddings[s_idx]

    label_to_persistent: dict[str, str] = {}
    for label in labels:
        emb = emb_map.get(label)
        if emb is not None:
            speaker_id, next_id = match_speaker(
                np.array(emb), known, next_id, args.match_threshold)
            if speaker_id not in known:
                known[speaker_id] = [np.array(emb)]
                persist_embedding(speaker_id, np.array(emb), args.embeddings_dir)
            else:
                avg_emb = np.mean(known[speaker_id], axis=0)
                persist_embedding(speaker_id, avg_emb, args.embeddings_dir)
        else:
            speaker_id = f"speaker_{next_id}"
            next_id += 1
        label_to_persistent[label] = speaker_id

    input_stem = Path(args.input).stem
    if input_stem.endswith(".wav"):
        input_stem = input_stem[:-4]
    unique_speakers = set(label_to_persistent.values())

    segments = []
    for segment, _, label in exclusive.itertracks(yield_label=True):
        speaker_id = label_to_persistent[label]
        offset_ms = int(segment.start * 1000)
        output_filename = f"{input_stem}+{offset_ms:05d}+{speaker_id}.wav"
        output_path = os.path.join(args.output_dir, output_filename)
        extract_audio(args.input, output_path, segment.start, segment.end)
        segments.append({
            "file": output_filename,
            "speaker": speaker_id,
            "start": round(segment.start, 3),
            "end": round(segment.end, 3),
            "offset_ms": offset_ms
        })

    if len(unique_speakers) == 1 and len(segments) >= 1:
        speaker_id = segments[0]["speaker"]
        new_name = f"{input_stem}+{speaker_id}.wav"
        new_path = os.path.join(args.output_dir, new_name)

        shutil.copy2(args.input, new_path)

        for seg in segments:
            seg_path = os.path.join(args.output_dir, seg["file"])
            if os.path.exists(seg_path):
                os.remove(seg_path)
        segments = [{"file": new_name, "speaker": speaker_id, "start": 0.0, "end": -1.0}]

    print(json.dumps(segments, ensure_ascii=False))


if __name__ == "__main__":
    main()
