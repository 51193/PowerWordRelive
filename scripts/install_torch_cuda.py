#!/usr/bin/env python3
"""检测 CUDA 驱动版本并安装匹配的 PyTorch（含 CUDA 支持）。"""
import argparse
import re
import subprocess
import sys


def get_cuda_driver_version() -> str | None:
    try:
        out = subprocess.check_output(["nvidia-smi"], text=True, stderr=subprocess.DEVNULL)
    except (FileNotFoundError, subprocess.CalledProcessError):
        return None
    m = re.search(r"CUDA Version: (\d+\.\d+)", out)
    return m.group(1) if m else None


def map_to_torch_index(cuda_ver: str) -> str:
    """将 CUDA 驱动版本映射到 PyTorch wheel 索引后缀。"""
    try:
        major, minor = cuda_ver.split(".")
        major, minor = int(major), int(minor)
    except ValueError:
        sys.exit(f"Failed to parse CUDA version: {cuda_ver}")

    # 从高版本 CUDA 开始尝试，驱动向下兼容
    candidates = [
        (12, 8, "cu128"),
        (12, 6, "cu126"),
        (12, 4, "cu124"),
        (12, 1, "cu121"),
        (11, 8, "cu118"),
    ]
    for cu_major, cu_minor, index in candidates:
        if major > cu_major or (major == cu_major and minor >= cu_minor):
            return index

    sys.exit(f"No matching PyTorch CUDA index for CUDA {cuda_ver}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Install PyTorch with CUDA support matching the host GPU driver"
    )
    parser.add_argument(
        "--extra-packages",
        nargs="*",
        default=[],
        help="Additional pip packages to install after torch",
    )
    args = parser.parse_args()

    cuda_ver = get_cuda_driver_version()
    if cuda_ver is None:
        sys.exit("No NVIDIA GPU or nvidia-smi not found. Cannot install CUDA torch.")

    idx = map_to_torch_index(cuda_ver)
    index_url = f"https://download.pytorch.org/whl/{idx}"

    print(f"CUDA driver: {cuda_ver} -> PyTorch index: {idx}")

    cmd = [
        sys.executable, "-m", "pip", "install",
        "torch", "torchaudio",
        "--index-url", index_url,
    ]
    subprocess.check_call(cmd)

    if args.extra_packages:
        subprocess.check_call(
            [sys.executable, "-m", "pip", "install"] + args.extra_packages
        )


if __name__ == "__main__":
    main()
