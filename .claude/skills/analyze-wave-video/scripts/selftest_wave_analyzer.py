#!/usr/bin/env python3
"""Regression check: run the analyzer on synthetic clips whose answers are known.

The analyzer reports many numbers with confidence labels, and a plausible-looking number
is the failure mode that costs the most downstream. These clips have exact ground truth,
so a wrong estimate fails loudly instead of arriving as a confident shader input.

    python selftest_wave_analyzer.py [--keep <directory>]
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import cv2
import numpy as np

WIDTH, HEIGHT = 480, 270
FPS = 30.0
SECONDS = 12.0
FRAME_COUNT = int(FPS * SECONDS)
SCRIPT = Path(__file__).with_name("analyze_wave_video.py")


def base_texture() -> np.ndarray:
    generator = np.random.default_rng(7)
    texture = generator.normal(0.0, 1.0, (HEIGHT, WIDTH)).astype(np.float32)
    texture = cv2.GaussianBlur(texture, (0, 0), 1.6)
    return (texture - texture.min()) / max(1e-6, float(np.ptp(texture)))


TEXTURE = base_texture()


def colorize(field: np.ndarray) -> np.ndarray:
    deep = np.array([95.0, 60.0, 30.0])
    bright = np.array([215.0, 190.0, 150.0])
    image = deep[None, None, :] + field[:, :, None] * (bright - deep)[None, None, :]
    return np.clip(image, 0, 255).astype(np.uint8)


def open_writer(path: Path) -> cv2.VideoWriter:
    writer = cv2.VideoWriter(str(path), cv2.VideoWriter_fourcc(*"mp4v"), FPS, (WIDTH, HEIGHT))
    if not writer.isOpened():
        raise SystemExit(f"could not open a video writer for {path}")
    return writer


def write_traveling_wave(path: Path, wavelength: float, period: float, shake: float) -> None:
    """Crests travel down +y at wavelength/period px/s, with micro detail advected along."""
    writer = open_writer(path)
    ys, xs = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float32)
    wave_number = 2.0 * np.pi / wavelength
    angular_frequency = 2.0 * np.pi / period
    speed = wavelength / period
    shake_generator = np.random.default_rng(11)
    for index in range(FRAME_COUNT):
        time = index / FPS
        detail = cv2.remap(
            TEXTURE, xs, np.float32((ys - speed * time) % HEIGHT),
            cv2.INTER_LINEAR, borderMode=cv2.BORDER_WRAP,
        )
        field = np.clip(
            0.5 + 0.42 * np.sin(wave_number * ys - angular_frequency * time)
            + 0.10 * (detail - 0.5),
            0.0, 1.0,
        )
        image = colorize(field)
        if shake > 0.0:
            dx, dy = shake_generator.normal(0.0, shake, 2)
            image = cv2.warpAffine(
                image, np.float32([[1, 0, dx], [0, 1, dy]]), (WIDTH, HEIGHT),
                borderMode=cv2.BORDER_REFLECT,
            )
        writer.write(image)
    writer.release()


def write_foam_pulse(path: Path, period: float, persistence: float) -> None:
    """A locked camera and a bright band that holds for `persistence` of every cycle."""
    writer = open_writer(path)
    flat = colorize(np.full((HEIGHT, WIDTH), 0.25, dtype=np.float32))
    duty = persistence / period
    for index in range(FRAME_COUNT):
        image = flat.copy()
        if (index / FPS % period) / period < duty:
            top = int(HEIGHT * 0.55)
            image[top:top + 40, :] = (250, 250, 250)
        writer.write(image)
    writer.release()


def run_analyzer(video: Path, output: Path) -> dict:
    result = subprocess.run(
        [sys.executable, str(SCRIPT), str(video), "--output", str(output),
         "--duration", "12", "--sample-fps", "10"],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        raise SystemExit(f"analyzer failed for {video.name}:\n{result.stderr}")
    return json.loads((output / "analysis.json").read_text(encoding="utf-8"))


def relative_error(measured: float | None, expected: float) -> float:
    if measured is None:
        return float("inf")
    return abs(float(measured) - expected) / expected


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--keep", type=Path, help="keep the clips and artifacts here")
    args = parser.parse_args()

    root = args.keep or Path(tempfile.mkdtemp(prefix="wave-selftest-"))
    root.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []
    lines: list[str] = []

    def check(name: str, ok: bool, detail: str) -> None:
        lines.append(f"[{'PASS' if ok else 'FAIL'}] {name}: {detail}")
        if not ok:
            failures.append(name)

    try:
        # 1. Static camera, travelling crests: spacing, period and speed must come back.
        clip = root / "wave.mp4"
        write_traveling_wave(clip, wavelength=40.0, period=2.0, shake=0.0)
        data = run_analyzer(clip, root / "wave")
        periodicity = data["periodicity"]
        speed = data["apparent_speed_cross_check"]
        spacing_error = relative_error(periodicity["preferred_spatial_spacing_px"], 40.0)
        period_error = relative_error(periodicity["preferred_temporal_period_s"], 2.0)
        speed_error = relative_error(speed["consensus_px_s"], 20.0)
        check("wave.spacing", spacing_error <= 0.10,
              f"{periodicity['preferred_spatial_spacing_px']} px vs 40 ({spacing_error:.1%})")
        check("wave.period", period_error <= 0.10,
              f"{periodicity['preferred_temporal_period_s']} s vs 2.0 ({period_error:.1%})")
        check("wave.speed", speed_error <= 0.15,
              f"{speed['consensus_px_s']} px/s vs 20 ({speed_error:.1%})")
        check("wave.speed_is_cross_checked", speed["consensus_source"] != "unresolved",
              speed["consensus_source"])
        check("wave.no_false_cuts", not data["stabilization"].get("cut_candidates"),
              f"{len(data['stabilization'].get('cut_candidates', []))} cut candidates")

        # 2. The same clip shaken: stabilization must engage and still recover the truth.
        clip = root / "shake.mp4"
        write_traveling_wave(clip, wavelength=40.0, period=2.0, shake=3.5)
        data = run_analyzer(clip, root / "shake")
        stabilization = data["stabilization"]
        speed = data["apparent_speed_cross_check"]
        check("shake.stabilization_used", stabilization["used_for_analysis"],
              f"reasons={stabilization['rejection_reasons']}")
        check("shake.jitter_reduced", (stabilization.get("jitter_reduction_ratio") or 0.0) >= 1.5,
              f"{stabilization.get('jitter_px_per_pair_before')} -> "
              f"{stabilization.get('jitter_px_per_pair_after')} px/pair")
        check("shake.frame_kept", (stabilization.get("minimum_valid_frame_fraction") or 0.0) >= 0.85,
              f"min valid area {stabilization.get('minimum_valid_frame_fraction')}")
        check("shake.period", relative_error(data["periodicity"]["preferred_temporal_period_s"], 2.0) <= 0.10,
              f"{data['periodicity']['preferred_temporal_period_s']} s vs 2.0")
        check("shake.speed", relative_error(speed["consensus_px_s"], 20.0) <= 0.20,
              f"{speed['consensus_px_s']} px/s vs 20")

        # 3. Locked camera and a known foam duty cycle.
        clip = root / "foam.mp4"
        write_foam_pulse(clip, period=3.0, persistence=1.0)
        data = run_analyzer(clip, root / "foam")
        foam = data["foam"]
        check("foam.camera_locked", data["stabilization"]["camera_motion"] == "locked",
              data["stabilization"]["camera_motion"])
        check("foam.period", relative_error(data["periodicity"]["preferred_temporal_period_s"], 3.0) <= 0.10,
              f"{data['periodicity']['preferred_temporal_period_s']} s vs 3.0")
        check("foam.persistence", relative_error(foam["fixed_pixel_persistence_s"], 1.0) <= 0.20,
              f"{foam['fixed_pixel_persistence_s']} s vs 1.0")
        check("foam.accepted", foam["metrics_accepted_automatically"],
              f"reasons={foam['automatic_rejection_reasons']}")
        check("foam.no_false_cuts", not data["stabilization"].get("cut_candidates"),
              f"{len(data['stabilization'].get('cut_candidates', []))} cut candidates")
    finally:
        if args.keep is None:
            shutil.rmtree(root, ignore_errors=True)

    print("\n".join(lines))
    if failures:
        print(f"\n{len(failures)} check(s) failed: {', '.join(failures)}")
        raise SystemExit(1)
    print(f"\nAll {len(lines)} checks passed.")


if __name__ == "__main__":
    main()
