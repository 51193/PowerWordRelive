#!/usr/bin/env python3
"""预下载 pyannote 说话人分离模型到指定缓存目录。模型约 600MB，首次运行需联网并需要 HuggingFace token。"""
import os
import sys
import argparse
import warnings

warnings.filterwarnings("ignore")


def main():
    parser = argparse.ArgumentParser(description="预下载 pyannote 说话人分离模型")
    parser.add_argument("--cache-dir", required=True, help="HuggingFace 缓存目录（对应 HF_HOME）")
    parser.add_argument("--hf-token", required=True, help="HuggingFace 访问令牌")
    args = parser.parse_args()

    os.makedirs(args.cache_dir, exist_ok=True)
    os.environ["HF_HOME"] = args.cache_dir

    print("[SpeakerSplit] 开始加载 pyannote 模型（首次运行将下载模型，约 600MB）...")

    from pyannote.audio import Pipeline

    pipeline = Pipeline.from_pretrained(
        "pyannote/speaker-diarization-3.1",
        token=args.hf_token,
    )

    print("[SpeakerSplit] 模型加载完成，已验证可用。")
    print(f"[SpeakerSplit] 缓存目录: {args.cache_dir}")


if __name__ == "__main__":
    main()
