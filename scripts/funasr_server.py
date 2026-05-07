#!/usr/bin/env python3
"""FunASR Paraformer 转录服务器 — 常驻进程，stdin/stdout JSON 行通信。"""
import json
import logging
import os
import re
import sys
import time
import wave
from pathlib import Path

logging.getLogger("transformers").setLevel(logging.ERROR)
logging.getLogger("modelscope").setLevel(logging.ERROR)


class _SuppressNoiseFilter(logging.Filter):
    """仅屏蔽已知无害的内部噪声，不拦截真正的 WARNING/ERROR。"""
    _noise = ["trust_remote_code"]

    def filter(self, record):
        msg = record.getMessage()
        return not any(kw in msg for kw in self._noise)


logging.getLogger().addFilter(_SuppressNoiseFilter())


def _clean_paraformer_text(text, timestamps):
    """Paraformer 输出带空格分隔的字符级文本，去除空格并重新对齐时间轴。"""
    clean_chars = []
    clean_ts = []
    ts_idx = 0
    for ch in text:
        if ch == " ":
            ts_idx += 1
            continue
        clean_chars.append(ch)
        if ts_idx < len(timestamps):
            clean_ts.append(timestamps[ts_idx])
        ts_idx += 1

    clean_text = "".join(clean_chars)
    return clean_text, clean_ts


def split_sentences(text):
    """按中文标点分句，无标点则不拆分。"""
    has_punc = any(ch in "。！？，；、" for ch in text)
    if not has_punc:
        return [(0, len(text), text)]

    sentences = []
    buf = ""
    buf_start = 0
    for i, ch in enumerate(text):
        buf += ch
        if ch in "。！？":
            sentences.append((buf_start, i + 1, buf.strip()))
            buf = ""
            buf_start = i + 1
        elif ch in "，；、":
            sentences.append((buf_start, i + 1, buf.strip()))
            buf = ""
            buf_start = i + 1
    if buf.strip():
        sentences.append((buf_start, len(text), buf.strip()))
    return sentences


def write_srt(segments, output_dir, stem, fallback_duration_ms):
    """将 FunASR 返回的 segments 写入 SRT 文件。"""
    srt_path = os.path.join(output_dir, f"{stem}.srt")

    entries = []
    for seg in segments:
        raw_text = seg.get("text", "")
        timestamps = seg.get("timestamp", [])

        if not raw_text or not timestamps:
            continue

        clean_text, clean_ts = _clean_paraformer_text(raw_text, timestamps)
        if not clean_text or not clean_ts:
            continue

        sentences = split_sentences(clean_text)
        if not sentences:
            continue

        n_ts = len(clean_ts)

        for s_start, s_end, sent in sentences:
            if n_ts == 0:
                ts_beg = 0
                ts_end = fallback_duration_ms
            else:
                idx_beg = min(s_start, n_ts - 1)
                idx_end = min(s_end - 1, n_ts - 1)
                ts_beg = clean_ts[idx_beg][0]
                ts_end = clean_ts[idx_end][1]

            entries.append((ts_beg, ts_end, sent))

    if not entries:
        return

    with open(srt_path, "w", encoding="utf-8") as f:
        for i, (beg_ms, end_ms, sent) in enumerate(entries, 1):
            f.write(f"{i}\n")
            f.write(f"{_ms_to_srt(beg_ms)} --> {_ms_to_srt(end_ms)}\n")
            f.write(f"{sent}\n\n")


def _ms_to_srt(ms):
    h = ms // 3600000
    m = (ms % 3600000) // 60000
    s = (ms % 60000) // 1000
    mi = ms % 1000
    return f"{h:02d}:{m:02d}:{s:02d},{mi:03d}"


def run_server():
    import argparse

    parser = argparse.ArgumentParser(description="FunASR Paraformer transcription server")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--model", default="paraformer-zh")
    parser.add_argument("--device", default="cuda")
    args = parser.parse_args()

    os.makedirs(args.output_dir, exist_ok=True)

    from funasr import AutoModel

    # FunASR 在初始化时会向 stdout 输出版本/下载信息，重定向到 /dev/null 避免污染 JSON 协议和日志。
    devnull = open(os.devnull, "w")
    old_stdout = sys.stdout
    sys.stdout = devnull
    try:
        model = AutoModel(
            model=args.model,
            vad_model="fsmn-vad",
            vad_kwargs={"max_single_segment_time": 60000},
            device=args.device,
            disable_pbar=True,
            disable_update=True,
        )
    finally:
        sys.stdout = old_stdout
        devnull.close()

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
            res = model.generate(input=input_path)
        except Exception as e:
            resp = {"error": str(e)}
            print(json.dumps(resp, ensure_ascii=False), flush=True)
            continue

        input_stem = Path(input_path).stem
        if input_stem.endswith(".wav"):
            input_stem = input_stem[:-4]

        try:
            with wave.open(input_path, "rb") as wf:
                audio_duration = wf.getnframes() / wf.getframerate()
        except Exception:
            audio_duration = 0

        fallback_ms = int(audio_duration * 1000)
        write_srt(res, args.output_dir, input_stem, fallback_ms)

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
