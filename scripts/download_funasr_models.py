#!/usr/bin/env python3
"""预下载 FunASR 模型到指定缓存目录。模型约 1GB，首次运行需联网。"""
import os
import sys
import argparse


def main():
    parser = argparse.ArgumentParser(description="预下载 FunASR 模型")
    parser.add_argument("--cache-dir", required=True, help="模型缓存目录（对应 MODELSCOPE_CACHE）")
    args = parser.parse_args()

    os.makedirs(args.cache_dir, exist_ok=True)
    os.environ["MODELSCOPE_CACHE"] = args.cache_dir

    print("[FunASR] 开始加载模型（首次运行将下载模型，约 1GB，请耐心等待）...")

    from funasr import AutoModel

    model = AutoModel(
        model="paraformer-zh",
        vad_model="fsmn-vad",
        vad_kwargs={"max_single_segment_time": 60000},
        disable_pbar=True,
        disable_update=True,
    )

    print("[FunASR] 模型加载完成，已验证可用。")
    print(f"[FunASR] 缓存目录: {args.cache_dir}")


if __name__ == "__main__":
    main()
