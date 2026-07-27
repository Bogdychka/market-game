#!/usr/bin/env python3
"""Extract video metadata, representative frames, and motion peaks."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("video", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--samples", type=int, default=12)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--thumbnail-width", type=int, default=480)
    parser.add_argument("--motion-rate", type=float, default=2.0)
    parser.add_argument("--motion-peaks", type=int, default=6)
    return parser.parse_args()


def open_video(path: Path) -> tuple[cv2.VideoCapture, dict[str, float | int | str]]:
    capture = cv2.VideoCapture(str(path))
    if not capture.isOpened():
        raise RuntimeError(f"Could not open video: {path}")

    fps = float(capture.get(cv2.CAP_PROP_FPS))
    frame_count = int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
    width = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
    duration = frame_count / fps if fps > 0 else 0.0
    codec_value = int(capture.get(cv2.CAP_PROP_FOURCC))
    codec = "".join(chr((codec_value >> (8 * index)) & 0xFF) for index in range(4)).strip("\x00")
    metadata: dict[str, float | int | str] = {
        "codec": codec,
        "width": width,
        "height": height,
        "fps": round(fps, 4),
        "frame_count": frame_count,
        "duration_seconds": round(duration, 4),
    }
    return capture, metadata


def sample_times(duration: float, count: int) -> list[float]:
    count = max(1, count)
    if duration <= 0 or count == 1:
        return [0.0]
    end = max(0.0, duration - max(0.1, duration * 0.01))
    return [end * index / (count - 1) for index in range(count)]


def read_frame(capture: cv2.VideoCapture, timestamp: float) -> np.ndarray:
    for rewind in (0.0, 0.05, 0.1, 0.25, 0.5):
        candidate = max(0.0, timestamp - rewind)
        capture.set(cv2.CAP_PROP_POS_MSEC, candidate * 1000.0)
        success, frame = capture.read()
        if success and frame is not None:
            return frame
    raise RuntimeError(f"Could not decode frame at {timestamp:.3f}s")


def timestamp_label(seconds: float) -> str:
    minutes = int(seconds // 60)
    remainder = seconds - minutes * 60
    return f"{minutes:02d}:{remainder:05.2f}"


def save_samples(
    capture: cv2.VideoCapture,
    output: Path,
    times: list[float],
) -> tuple[list[dict[str, float | int | str]], list[Image.Image]]:
    frames_directory = output / "frames"
    frames_directory.mkdir(parents=True, exist_ok=True)
    records: list[dict[str, float | int | str]] = []
    images: list[Image.Image] = []
    for index, timestamp in enumerate(times):
        frame = read_frame(capture, timestamp)
        frame_path = frames_directory / f"frame_{index:03d}_{timestamp:08.3f}s.jpg"
        cv2.imwrite(str(frame_path), frame, [cv2.IMWRITE_JPEG_QUALITY, 92])
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        images.append(Image.fromarray(rgb))
        records.append(
            {
                "index": index,
                "timestamp_seconds": round(timestamp, 4),
                "path": frame_path.as_posix(),
                "mean_brightness": round(float(cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY).mean()), 3),
            }
        )
    return records, images


def resize_thumbnail(image: Image.Image, width: int) -> Image.Image:
    ratio = width / image.width
    height = max(1, round(image.height * ratio))
    return image.resize((width, height), Image.Resampling.LANCZOS)


def build_contact_sheet(
    images: list[Image.Image],
    times: list[float],
    output_path: Path,
    columns: int,
    thumbnail_width: int,
) -> None:
    thumbnails = [resize_thumbnail(image, thumbnail_width) for image in images]
    columns = max(1, min(columns, len(thumbnails)))
    rows = math.ceil(len(thumbnails) / columns)
    label_height = 32
    cell_height = max(image.height for image in thumbnails) + label_height
    sheet = Image.new("RGB", (columns * thumbnail_width, rows * cell_height), (24, 24, 24))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=18)
    for index, thumbnail in enumerate(thumbnails):
        x = index % columns * thumbnail_width
        y = index // columns * cell_height
        sheet.paste(thumbnail, (x, y))
        draw.rectangle((x, y + thumbnail.height, x + thumbnail_width, y + cell_height), fill=(24, 24, 24))
        draw.text((x + 8, y + thumbnail.height + 6), timestamp_label(times[index]), fill=(255, 255, 255), font=font)
    sheet.save(output_path)


def analyze_motion(
    capture: cv2.VideoCapture,
    duration: float,
    rate: float,
    peak_count: int,
) -> list[dict[str, float]]:
    if duration <= 0 or rate <= 0:
        return []
    interval = 1.0 / rate
    timestamps = np.arange(0.0, duration, interval)
    previous: np.ndarray | None = None
    scores: list[tuple[float, float]] = []
    for timestamp in timestamps:
        frame = read_frame(capture, float(timestamp))
        gray = cv2.resize(cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY), (320, 180))
        if previous is not None:
            score = float(cv2.absdiff(previous, gray).mean())
            scores.append((score, float(timestamp)))
        previous = gray
    peaks = sorted(scores, reverse=True)[: max(0, peak_count)]
    return [
        {"timestamp_seconds": round(timestamp, 4), "motion_score": round(score, 4)}
        for score, timestamp in peaks
    ]


def main() -> int:
    args = parse_args()
    video_path = args.video.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    capture, metadata = open_video(video_path)
    try:
        duration = float(metadata["duration_seconds"])
        times = sample_times(duration, args.samples)
        samples, images = save_samples(capture, output, times)
        contact_sheet = output / "contact_sheet.png"
        build_contact_sheet(images, times, contact_sheet, args.columns, args.thumbnail_width)
        motion_peaks = analyze_motion(capture, duration, args.motion_rate, args.motion_peaks)
    finally:
        capture.release()

    result = {
        "input": video_path.as_posix(),
        "file_size_bytes": video_path.stat().st_size,
        **metadata,
        "samples": samples,
        "motion_peaks": motion_peaks,
        "contact_sheet": contact_sheet.as_posix(),
    }
    analysis_path = output / "analysis.json"
    analysis_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps({"analysis": str(analysis_path), "contact_sheet": str(contact_sheet)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
