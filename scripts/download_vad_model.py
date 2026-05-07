#!/usr/bin/env python3
"""预下载 Silero VAD 模型到指定缓存目录。模型约 50MB，首次运行需联网。"""
import os
import sys
import argparse
import io
import warnings

warnings.filterwarnings("ignore")


def main():
    parser = argparse.ArgumentParser(description="预下载 Silero VAD 模型")
    parser.add_argument("--cache-dir", required=True, help="torch 缓存目录（对应 TORCH_HOME）")
    args = parser.parse_args()

    os.makedirs(args.cache_dir, exist_ok=True)
    os.environ["TORCH_HOME"] = args.cache_dir

    print("[VAD] 开始加载 Silero VAD 模型（首次运行将下载模型，约 50MB）...")

    import torch

    buf = io.StringIO()
    old_stderr = sys.stderr
    sys.stderr = buf
    try:
        model, _ = torch.hub.load(
            repo_or_dir="snakers4/silero-vad",
            model="silero_vad",
            force_reload=False,
            trust_repo=True,
        )
    finally:
        sys.stderr = old_stderr
        captured = buf.getvalue()

    print("[VAD] 模型加载完成，已验证可用。")
    print(f"[VAD] 缓存目录: {args.cache_dir}")
    if captured:
        print(f"[VAD] 模型日志: {captured.strip()}")


if __name__ == "__main__":
    main()
