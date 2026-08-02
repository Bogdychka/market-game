#!/usr/bin/env python3
"""Measure repeatable visual evidence in a shore-wave reference video."""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from pathlib import Path
from typing import Any

import cv2
import numpy as np

import wave_video_diagnostics as diagnostics


MAX_FRAMES = 480
MAX_REVIEW_FRAMES = 144
KYMO_SAMPLES = 320


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Analyze shore-wave motion, periodicity, foam, run-up, and color."
    )
    parser.add_argument("video", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--start", type=float, default=0.0)
    parser.add_argument("--duration", type=float, default=20.0)
    parser.add_argument("--sample-fps", type=float, default=10.0)
    parser.add_argument("--analysis-width", type=int, default=640)
    parser.add_argument("--review-frames", type=int, default=72)
    parser.add_argument("--roi", type=str, default="")
    parser.add_argument("--transect", type=str, default="")
    parser.add_argument("--meters-per-pixel", type=float, default=0.0)
    parser.add_argument("--no-stabilization", action="store_true")
    return parser.parse_args()


def fail(message: str) -> None:
    print(f"error: {message}", file=sys.stderr)
    raise SystemExit(2)


def safe_slug(value: str) -> str:
    slug = re.sub(r"[^A-Za-z0-9._-]+", "-", value).strip("-._")
    return slug or "wave-video"


def finite_float(value: Any, digits: int = 4) -> float | None:
    number = float(value)
    if not math.isfinite(number):
        return None
    return round(number, digits)


def confidence_label(value: float) -> str:
    if value >= 0.72:
        return "high"
    if value >= 0.42:
        return "medium"
    return "low"


def parse_numbers(text: str, expected: int) -> list[float]:
    try:
        values = [float(value.strip()) for value in text.split(",")]
    except ValueError as exc:
        raise ValueError(f"Expected {expected} comma-separated numbers: {text}") from exc
    if len(values) != expected:
        raise ValueError(f"Expected {expected} comma-separated numbers: {text}")
    return values


def parse_rect(text: str, width: int, height: int) -> tuple[int, int, int, int]:
    if not text:
        return 0, 0, width, height
    values = parse_numbers(text, 4)
    if max(abs(value) for value in values) <= 1.0:
        values = [values[0] * width, values[1] * height, values[2] * width, values[3] * height]
    x, y, w, h = [int(round(value)) for value in values]
    x = max(0, min(width - 2, x))
    y = max(0, min(height - 2, y))
    w = max(2, min(width - x, w))
    h = max(2, min(height - y, h))
    return x, y, w, h


def parse_transect(text: str, width: int, height: int) -> tuple[float, float, float, float] | None:
    if not text:
        return None
    values = parse_numbers(text, 4)
    if max(abs(value) for value in values) <= 1.0:
        values = [values[0] * width, values[1] * height, values[2] * width, values[3] * height]
    x1, y1, x2, y2 = values
    return (
        float(np.clip(x1, 0, width - 1)),
        float(np.clip(y1, 0, height - 1)),
        float(np.clip(x2, 0, width - 1)),
        float(np.clip(y2, 0, height - 1)),
    )


def read_video(args: argparse.Namespace) -> tuple[list[np.ndarray], np.ndarray, dict[str, Any]]:
    capture = cv2.VideoCapture(str(args.video))
    if not capture.isOpened():
        fail(f"Could not open video: {args.video}")

    source_fps = float(capture.get(cv2.CAP_PROP_FPS))
    frame_count = int(capture.get(cv2.CAP_PROP_FRAME_COUNT))
    source_width = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH))
    source_height = int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
    if source_fps <= 0 or source_width <= 0 or source_height <= 0:
        fail("Video metadata is incomplete or unsupported")

    source_duration = frame_count / source_fps if frame_count > 0 else 0.0
    start = max(0.0, args.start)
    requested_duration = max(1.0, args.duration)
    if source_duration > 0:
        requested_duration = min(requested_duration, max(0.0, source_duration - start))
    if requested_duration <= 0:
        fail("The requested start time is outside the video")

    requested_sample_fps = float(np.clip(args.sample_fps, 1.0, source_fps))
    sample_fps = min(requested_sample_fps, MAX_FRAMES / requested_duration)
    sample_step = max(1, int(round(source_fps / sample_fps)))
    actual_sample_fps = source_fps / sample_step
    start_frame = int(round(start * source_fps))
    end_frame = start_frame + int(round(requested_duration * source_fps))
    if frame_count > 0:
        end_frame = min(end_frame, frame_count)

    capture.set(cv2.CAP_PROP_POS_FRAMES, start_frame)
    scale = min(1.0, max(160, args.analysis_width) / source_width)
    analysis_width = int(round(source_width * scale))
    analysis_height = int(round(source_height * scale))
    frames: list[np.ndarray] = []
    times: list[float] = []
    index = start_frame

    while index < end_frame and len(frames) < MAX_FRAMES:
        ok, frame = capture.read()
        if not ok:
            break
        if (index - start_frame) % sample_step == 0:
            if scale != 1.0:
                frame = cv2.resize(frame, (analysis_width, analysis_height), interpolation=cv2.INTER_AREA)
            frames.append(frame)
            times.append(index / source_fps)
        index += 1

    capture.release()
    if len(frames) < 8:
        fail("The selected interval produced fewer than eight usable frames")

    metadata = {
        "path": str(args.video.resolve()),
        "codec_backend": cv2.getBuildInformation().splitlines()[0],
        "source_width": source_width,
        "source_height": source_height,
        "source_fps": finite_float(source_fps),
        "source_frame_count": frame_count,
        "source_duration_s": finite_float(source_duration),
        "analysis_width": analysis_width,
        "analysis_height": analysis_height,
        "analysis_start_s": finite_float(times[0]),
        "analysis_end_s": finite_float(times[-1]),
        "analysis_frames": len(frames),
        "sample_fps": finite_float(actual_sample_fps),
    }
    return frames, np.asarray(times, dtype=np.float64), metadata


def moving_average(values: np.ndarray, window: int) -> np.ndarray:
    """Edge-safe moving average used to smooth the camera trajectory."""
    if window <= 1 or len(values) < 3:
        return values.astype(np.float64)
    window = min(window, len(values))
    if window % 2 == 0:
        window += 1
    pad = window // 2
    padded = np.pad(values.astype(np.float64), (pad, pad), mode="edge")
    kernel = np.ones(window, dtype=np.float64) / window
    return np.convolve(padded, kernel, mode="valid")


def estimate_affine(current_gray: np.ndarray, previous_gray: np.ndarray) -> tuple[np.ndarray, float]:
    points = cv2.goodFeaturesToTrack(
        previous_gray,
        maxCorners=500,
        qualityLevel=0.015,
        minDistance=9,
        blockSize=7,
    )
    if points is None or len(points) < 16:
        return np.eye(2, 3, dtype=np.float32), 0.0
    tracked, status, _ = cv2.calcOpticalFlowPyrLK(
        previous_gray,
        current_gray,
        points,
        None,
        winSize=(25, 25),
        maxLevel=3,
        criteria=(cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 30, 0.01),
    )
    if tracked is None or status is None:
        return np.eye(2, 3, dtype=np.float32), 0.0
    good_previous = points[status.reshape(-1) == 1].reshape(-1, 2)
    good_current = tracked[status.reshape(-1) == 1].reshape(-1, 2)
    if len(good_previous) < 12:
        return np.eye(2, 3, dtype=np.float32), 0.0
    matrix, inliers = cv2.estimateAffinePartial2D(
        good_current,
        good_previous,
        method=cv2.RANSAC,
        ransacReprojThreshold=2.5,
        maxIters=1500,
        confidence=0.99,
        refineIters=10,
    )
    if matrix is None or inliers is None:
        return np.eye(2, 3, dtype=np.float32), 0.0
    scale = math.sqrt(float(matrix[0, 0] ** 2 + matrix[0, 1] ** 2))
    translation = math.hypot(float(matrix[0, 2]), float(matrix[1, 2]))
    quality = float(np.mean(inliers))
    if not 0.92 <= scale <= 1.08 or translation > 0.18 * max(current_gray.shape):
        return np.eye(2, 3, dtype=np.float32), quality * 0.2
    return matrix.astype(np.float32), quality


def stabilize_frames(
    frames: list[np.ndarray], times: np.ndarray, enabled: bool
) -> tuple[list[np.ndarray], list[np.ndarray], dict[str, Any]]:
    """Remove camera jitter by smoothing the solved trajectory.

    Absolute accumulation ("warp every frame back onto frame zero") drifts without bound
    when the tracked features sit on moving water, which pushes valid image area toward
    zero and gets a perfectly good solve rejected. Smoothing the trajectory and warping by
    (smoothed - raw) keeps every correction bounded and cannot run away with the waves.
    """
    height, width = frames[0].shape[:2]
    full_mask = np.full((height, width), 255, dtype=np.uint8)
    if not enabled:
        return frames, [full_mask for _ in frames], {
            "enabled": False,
            "method": "disabled_by_option",
            "camera_motion": "not_measured",
            "median_inlier_ratio": None,
            "accepted_pair_fraction": None,
            "confidence": "not_applicable",
            "used_for_analysis": False,
            "rejection_reasons": ["disabled_by_option"],
            "pair_diagnostics": [],
            "warning": None,
        }

    previous_gray = cv2.cvtColor(frames[0], cv2.COLOR_BGR2GRAY)
    steps: list[tuple[float, float, float]] = []
    qualities: list[float] = []
    pair_diagnostics: list[dict[str, Any]] = []
    accepted = 0

    for index, frame in enumerate(frames[1:], 1):
        current_gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        affine, quality = estimate_affine(current_gray, previous_gray)
        qualities.append(quality)
        accepted_pair = quality >= 0.28
        rotation = math.degrees(math.atan2(float(affine[1, 0]), float(affine[0, 0])))
        translation = math.hypot(float(affine[0, 2]), float(affine[1, 2]))
        if accepted_pair:
            accepted += 1
            steps.append((float(affine[0, 2]), float(affine[1, 2]), math.radians(rotation)))
        else:
            steps.append((0.0, 0.0, 0.0))
        frame_difference = float(np.mean(cv2.absdiff(previous_gray, current_gray)))
        exposure_delta = float(np.mean(current_gray) - np.mean(previous_gray))
        pair_diagnostics.append({
            "time_s": finite_float(times[index]),
            "inlier_ratio": finite_float(quality),
            "accepted": accepted_pair,
            "translation_px": finite_float(translation),
            "rotation_deg": finite_float(rotation),
            "scale": finite_float(math.sqrt(float(affine[0, 0] ** 2 + affine[0, 1] ** 2))),
            "mean_frame_difference_0_255": finite_float(frame_difference),
            "exposure_delta_0_255": finite_float(exposure_delta),
        })
        previous_gray = current_gray

    step_array = np.asarray(steps, dtype=np.float64)
    trajectory = np.cumsum(step_array, axis=0)
    trajectory = np.vstack([np.zeros((1, 3)), trajectory])
    time_step = float(np.median(np.diff(times))) if len(times) > 1 else 0.1
    window = max(5, int(round(0.6 / max(time_step, 1e-6))))
    smoothed = np.column_stack([
        moving_average(trajectory[:, axis], window) for axis in range(3)
    ])
    correction = smoothed - trajectory

    jitter_before = float(np.median(np.linalg.norm(np.diff(trajectory[:, :2], axis=0), axis=1)))
    jitter_after = float(np.median(np.linalg.norm(np.diff(smoothed[:, :2], axis=0), axis=1)))
    step_translation = np.linalg.norm(step_array[:, :2], axis=1) if len(step_array) else np.zeros(1)
    median_step_translation = float(np.median(step_translation))
    camera_is_locked = median_step_translation < 0.35 and jitter_before < 0.5
    # A solve that has latched onto travelling water walks steadily in one direction, while a
    # handheld camera wanders. Net displacement over path length separates the two.
    path_length = float(np.sum(step_translation))
    net_displacement = float(np.linalg.norm(trajectory[-1, :2] - trajectory[0, :2]))
    drift_ratio = net_displacement / max(path_length, 1e-6)

    stabilized: list[np.ndarray] = []
    masks: list[np.ndarray] = []
    valid_fractions: list[float] = []
    center = (width * 0.5, height * 0.5)
    for index, frame in enumerate(frames):
        dx, dy, da = correction[index]
        matrix = cv2.getRotationMatrix2D(center, math.degrees(da), 1.0)
        matrix[0, 2] += dx
        matrix[1, 2] += dy
        matrix = matrix.astype(np.float32)
        stabilized.append(cv2.warpAffine(
            frame, matrix, (width, height),
            flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT,
        ))
        validity = cv2.warpAffine(
            full_mask, matrix, (width, height),
            flags=cv2.INTER_NEAREST, borderMode=cv2.BORDER_CONSTANT, borderValue=0,
        )
        masks.append(validity)
        valid_fraction = float(np.mean(validity > 0))
        valid_fractions.append(valid_fraction)
        pair_index = index - 1
        if 0 <= pair_index < len(pair_diagnostics):
            pair_diagnostics[pair_index]["valid_frame_fraction"] = finite_float(valid_fraction)
            pair_diagnostics[pair_index]["correction_px"] = finite_float(math.hypot(dx, dy))

    differences = np.asarray(
        [item["mean_frame_difference_0_255"] or 0.0 for item in pair_diagnostics], dtype=np.float64
    )
    exposures = np.asarray(
        [abs(item["exposure_delta_0_255"] or 0.0) for item in pair_diagnostics], dtype=np.float64
    )
    # A cut is an outlier against the clip's own dynamics. A fixed threshold flagged every
    # recurring surge or foam flash; scaling off the clip's p90 only fires on true outliers.
    difference_limit = max(40.0, 2.5 * float(np.percentile(differences, 90)))
    for item, difference, exposure in zip(pair_diagnostics, differences, exposures):
        item["cut_candidate"] = bool(
            difference > difference_limit and (item["inlier_ratio"] or 0.0) < 0.25
        )

    median_quality = float(np.median(qualities)) if qualities else 0.0
    accepted_fraction = accepted / max(1, len(frames) - 1)
    combined = 0.6 * median_quality + 0.4 * accepted_fraction
    median_valid = float(np.median(valid_fractions))
    minimum_valid = float(np.min(valid_fractions))
    jitter_reduction = jitter_before / max(jitter_after, 1e-6)

    rejection_reasons: list[str] = []
    if camera_is_locked:
        rejection_reasons.append("camera_is_locked_so_no_correction_is_needed")
    else:
        if combined < 0.42:
            rejection_reasons.append("weak_feature_solve")
        if minimum_valid < 0.80:
            rejection_reasons.append("corrections_crop_too_much_of_the_frame")
        if jitter_reduction < 1.05:
            rejection_reasons.append("stabilization_did_not_reduce_measured_jitter")
    use_for_analysis = not rejection_reasons

    if camera_is_locked:
        warning = None
    elif use_for_analysis:
        warning = None
    else:
        warning = (
            "Camera solve was rejected for measurements ("
            + ", ".join(rejection_reasons)
            + "); raw frames are used. Inspect stabilization_review.jpg."
        )

    if camera_is_locked:
        camera_motion = "locked"
    elif drift_ratio > 0.75 and jitter_reduction < 1.05:
        camera_motion = "solve_latched_onto_moving_water"
    else:
        camera_motion = "handheld_or_moving"

    return stabilized, masks, {
        "enabled": True,
        "method": "trajectory_smoothing",
        "smoothing_window_frames": window,
        "camera_motion": camera_motion,
        "trajectory_drift_ratio": finite_float(drift_ratio),
        "median_inlier_ratio": finite_float(median_quality),
        "accepted_pair_fraction": finite_float(accepted_fraction),
        "median_valid_frame_fraction": finite_float(median_valid),
        "minimum_valid_frame_fraction": finite_float(minimum_valid),
        "jitter_px_per_pair_before": finite_float(jitter_before),
        "jitter_px_per_pair_after": finite_float(jitter_after),
        "jitter_reduction_ratio": finite_float(jitter_reduction) if jitter_before > 1e-3 else None,
        "max_correction_px": finite_float(np.max(np.linalg.norm(correction[:, :2], axis=1))),
        "confidence": confidence_label(combined),
        "used_for_analysis": use_for_analysis,
        "rejection_reasons": rejection_reasons,
        "cut_candidates": [item for item in pair_diagnostics if item["cut_candidate"]],
        "pair_diagnostics": pair_diagnostics,
        "warning": warning,
    }


def analyze_motion(
    frames: list[np.ndarray],
    valid_masks: list[np.ndarray],
    times: np.ndarray,
    roi: tuple[int, int, int, int],
) -> tuple[dict[str, Any], np.ndarray]:
    x, y, width, height = roi
    flow_sum = np.zeros((height, width, 2), dtype=np.float64)
    flow_weight = np.zeros((height, width), dtype=np.float64)
    vectors: list[np.ndarray] = []
    speeds: list[float] = []
    pixel_speeds: list[float] = []
    coherences: list[float] = []
    pair_series: list[dict[str, Any]] = []

    previous = cv2.cvtColor(frames[0][y:y + height, x:x + width], cv2.COLOR_BGR2GRAY)
    for index in range(1, len(frames)):
        current = cv2.cvtColor(frames[index][y:y + height, x:x + width], cv2.COLOR_BGR2GRAY)
        flow = cv2.calcOpticalFlowFarneback(
            previous,
            current,
            None,
            pyr_scale=0.5,
            levels=4,
            winsize=21,
            iterations=4,
            poly_n=7,
            poly_sigma=1.5,
            flags=0,
        )
        magnitude = np.linalg.norm(flow, axis=2)
        valid = (
            (valid_masks[index][y:y + height, x:x + width] > 0)
            & (valid_masks[index - 1][y:y + height, x:x + width] > 0)
        )
        if np.any(valid):
            threshold = float(np.percentile(magnitude[valid], 65))
            strong = valid & (magnitude >= max(0.08, threshold))
        else:
            strong = valid
        if np.count_nonzero(strong) >= 32:
            selected = flow[strong]
            selected_magnitude = np.linalg.norm(selected, axis=1)
            weights = np.maximum(selected_magnitude, 0.05)
            vector = np.average(selected, axis=0, weights=weights)
            vectors.append(vector)
            dt = max(1e-6, float(times[index] - times[index - 1]))
            speeds.append(float(np.linalg.norm(vector) / dt))
            pixel_speeds.append(float(np.median(selected_magnitude) / dt))
            unit = selected / np.maximum(selected_magnitude[:, None], 1e-6)
            pair_coherence = float(np.linalg.norm(np.average(unit, axis=0, weights=weights)))
            coherences.append(pair_coherence)
            pair_series.append({
                "time_s": finite_float(times[index]),
                "vector_xy": [finite_float(vector[0]), finite_float(vector[1])],
                "speed_px_s": finite_float(np.linalg.norm(vector) / dt),
                "coherence": finite_float(pair_coherence),
                "direction_deg": finite_float(math.degrees(math.atan2(vector[1], vector[0])) % 360.0),
            })
            flow_sum[strong] += flow[strong]
            flow_weight[strong] += 1.0
        previous = current

    average_flow = np.zeros_like(flow_sum, dtype=np.float32)
    populated = flow_weight > 0
    average_flow[populated] = (flow_sum[populated] / flow_weight[populated, None]).astype(np.float32)
    if vectors:
        median_vector = np.median(np.asarray(vectors), axis=0)
        vector_length = float(np.linalg.norm(median_vector))
        direction = median_vector / vector_length if vector_length > 1e-6 else np.array([0.0, 1.0])
        coherence = float(np.median(coherences))
        speed = float(np.median(speeds))
        confidence = min(1.0, coherence * min(1.0, len(vectors) / 30.0))
        angles = np.asarray([math.atan2(vector[1], vector[0]) for vector in vectors])
        angle_weights = np.maximum(np.asarray(speeds), 1e-6)
        histogram, _ = np.histogram(
            angles % (2.0 * np.pi),
            bins=36,
            range=(0.0, 2.0 * np.pi),
            weights=angle_weights,
        )
        histogram = histogram / max(float(np.sum(histogram)), 1e-9)
        resultant = abs(np.sum(angle_weights * np.exp(1j * angles))) / max(float(np.sum(angle_weights)), 1e-9)
        direction_spread = math.degrees(math.sqrt(max(0.0, -2.0 * math.log(max(resultant, 1e-6)))))
    else:
        direction = np.array([0.0, 1.0])
        coherence = 0.0
        speed = 0.0
        confidence = 0.0
        histogram = np.zeros(36, dtype=np.float64)
        direction_spread = 180.0

    result = {
        "meaning": "Residual apparent image-pattern motion after optional camera stabilization; not fluid velocity.",
        "speed_caveat": (
            "Optical flow under-reads the speed of a periodic crest pattern because coarse-to-fine "
            "matching aliases on repeating crests. Trust the crest-track and spacing-over-period "
            "estimates for speed; trust optical flow for direction. See apparent_speed_cross_check."
        ),
        "direction_image_xy": [finite_float(direction[0]), finite_float(direction[1])],
        "speed_px_s": finite_float(speed),
        "speed_p10_px_s": finite_float(np.percentile(speeds, 10)) if speeds else None,
        "speed_p90_px_s": finite_float(np.percentile(speeds, 90)) if speeds else None,
        "median_pixel_speed_px_s": finite_float(np.median(pixel_speeds)) if pixel_speeds else None,
        "direction_coherence": finite_float(coherence),
        "direction_spread_deg": finite_float(direction_spread),
        "direction_histogram_360": [finite_float(value, 6) for value in histogram],
        "usable_frame_pairs": len(vectors),
        "confidence_score": finite_float(confidence),
        "confidence": confidence_label(confidence),
        "direction_confidence": confidence_label(confidence),
        "speed_confidence": "unresolved_until_cross_checked",
        "time_series": pair_series,
    }
    return result, average_flow


def line_through_rect(
    roi: tuple[int, int, int, int], direction: np.ndarray
) -> tuple[float, float, float, float]:
    x, y, width, height = roi
    center = np.array([x + 0.5 * (width - 1), y + 0.5 * (height - 1)], dtype=np.float64)
    vector = np.asarray(direction, dtype=np.float64)
    length = float(np.linalg.norm(vector))
    if length < 1e-6:
        vector = np.array([0.0, 1.0])
    else:
        vector /= length
    candidates: list[float] = []
    bounds = [(x, 0), (x + width - 1, 0), (y, 1), (y + height - 1, 1)]
    for value, axis in bounds:
        if abs(vector[axis]) < 1e-8:
            continue
        t = (value - center[axis]) / vector[axis]
        point = center + t * vector
        if x - 1e-5 <= point[0] <= x + width - 1 + 1e-5 and y - 1e-5 <= point[1] <= y + height - 1 + 1e-5:
            candidates.append(float(t))
    if len(candidates) < 2:
        return center[0], y, center[0], y + height - 1
    low, high = min(candidates), max(candidates)
    first = center + low * vector
    second = center + high * vector
    return float(first[0]), float(first[1]), float(second[0]), float(second[1])


def estimate_acf_period(
    signal: np.ndarray,
    spacing: float,
    minimum: float,
    maximum: float,
) -> tuple[float | None, float]:
    values = np.asarray(signal, dtype=np.float64)
    values = values - np.mean(values)
    energy = float(np.dot(values, values))
    if len(values) < 8 or energy < 1e-8:
        return None, 0.0
    correlation = np.correlate(values, values, mode="full")[len(values) - 1:]
    overlap = np.arange(len(values), 0, -1, dtype=np.float64)
    correlation = correlation / np.maximum(overlap, 1.0)
    correlation /= max(abs(float(correlation[0])), 1e-8)
    low = max(1, int(math.ceil(minimum / spacing)))
    high = min(len(correlation) - 2, int(math.floor(maximum / spacing)))
    if high <= low:
        return None, 0.0
    candidates = [
        index
        for index in range(low, high + 1)
        if correlation[index] >= correlation[index - 1] and correlation[index] > correlation[index + 1]
    ]
    if not candidates:
        return None, 0.0
    strongest = max(float(correlation[index]) for index in candidates)
    if strongest < 0.08:
        return None, max(0.0, strongest)
    strong_candidates = [
        index for index in candidates
        if float(correlation[index]) >= strongest * 0.85
    ]
    best = min(strong_candidates)
    score = float(np.clip(correlation[best], 0.0, 1.0))
    if score < 0.08:
        return None, score
    return best * spacing, score


def analyze_kymograph(
    frames: list[np.ndarray],
    times: np.ndarray,
    transect: tuple[float, float, float, float],
) -> tuple[np.ndarray, dict[str, Any], dict[str, Any]]:
    x1, y1, x2, y2 = transect
    xs = np.linspace(x1, x2, KYMO_SAMPLES, dtype=np.float32)
    ys = np.linspace(y1, y2, KYMO_SAMPLES, dtype=np.float32)
    map_x = xs.reshape(1, -1)
    map_y = ys.reshape(1, -1)
    rows = []
    for frame in frames:
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        row = cv2.remap(gray, map_x, map_y, interpolation=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
        rows.append(row.reshape(-1))
    kymograph = np.asarray(rows, dtype=np.uint8)
    line_length = math.hypot(x2 - x1, y2 - y1)
    spatial_step = line_length / max(1, KYMO_SAMPLES - 1)
    time_step = float(np.median(np.diff(times)))

    spatial_periods: list[float] = []
    spatial_scores: list[float] = []
    for row in kymograph[:: max(1, len(kymograph) // 40)]:
        period, score = estimate_acf_period(row, spatial_step, 6.0, max(8.0, line_length * 0.48))
        if period is not None:
            spatial_periods.append(period)
            spatial_scores.append(score)

    temporal_periods: list[float] = []
    temporal_scores: list[float] = []
    duration = max(time_step, float(times[-1] - times[0]))
    for column in np.linspace(0, KYMO_SAMPLES - 1, 32, dtype=int):
        period, score = estimate_acf_period(
            kymograph[:, column],
            time_step,
            0.45,
            max(0.5, min(12.0, duration * 0.48)),
        )
        if period is not None:
            temporal_periods.append(period)
            temporal_scores.append(score)

    spatial_period = float(np.median(spatial_periods)) if spatial_periods else None
    temporal_period = float(np.median(temporal_periods)) if temporal_periods else None
    spatial_confidence = float(np.median(spatial_scores)) if spatial_scores else 0.0
    temporal_confidence = float(np.median(temporal_scores)) if temporal_scores else 0.0
    phase_speed = spatial_period / temporal_period if spatial_period and temporal_period else None
    spectra, spectrum_plot_data = diagnostics.analyze_spectra(
        kymograph, times, line_length
    )
    crest_tracks, track_overlay = diagnostics.analyze_crest_tracks(
        kymograph, times, line_length
    )
    temporal_spectral = spectra["temporal_peaks"][0]["period_s"] \
        if spectra["temporal_peaks"] else None
    spatial_spectral = spectra["spatial_peaks"][0]["spacing_px"] \
        if spectra["spatial_peaks"] else None
    temporal_disagreement = (
        abs(temporal_spectral - temporal_period) / temporal_period
        if temporal_spectral and temporal_period
        else None
    )
    spatial_disagreement = (
        abs(spatial_spectral - spatial_period) / spatial_period
        if spatial_spectral and spatial_period
        else None
    )
    strongest_temporal_peak = spectra["temporal_peaks"][0] \
        if spectra["temporal_peaks"] else None
    temporal_spectrum_is_distinct = bool(
        strongest_temporal_peak
        and strongest_temporal_peak["power_fraction"] >= 0.04
        and strongest_temporal_peak["prominence_over_median"] >= 3.0
    )
    preferred_temporal = temporal_spectral \
        if temporal_spectrum_is_distinct else temporal_period
    if spatial_period and spatial_confidence >= 0.35:
        preferred_spatial = spatial_period
        preferred_spatial_source = "autocorrelation"
    elif spatial_spectral and spectra["spatial_confidence_score"] >= 0.45:
        preferred_spatial = spatial_spectral
        preferred_spatial_source = "spectrum"
    elif spatial_period is None and spatial_spectral is None:
        preferred_spatial = None
        preferred_spatial_source = "no_periodic_spatial_signal_detected"
    else:
        preferred_spatial = None
        preferred_spatial_source = "unresolved_both_methods_are_weak"
    result = {
        "transect_xyxy": [finite_float(value) for value in transect],
        "transect_length_px": finite_float(line_length),
        "spatial_spacing_px": finite_float(spatial_period) if spatial_period else None,
        "spatial_confidence_score": finite_float(spatial_confidence),
        "spatial_confidence": confidence_label(spatial_confidence),
        "temporal_period_s": finite_float(temporal_period) if temporal_period else None,
        "temporal_confidence_score": finite_float(temporal_confidence),
        "temporal_confidence": confidence_label(temporal_confidence),
        "spacing_over_period_px_s": finite_float(phase_speed) if phase_speed else None,
        "preferred_temporal_period_s": finite_float(preferred_temporal) if preferred_temporal else None,
        "preferred_temporal_source": (
            "spectrum" if preferred_temporal == temporal_spectral and temporal_spectral else "autocorrelation"
        ),
        "temporal_spectrum_is_distinct": temporal_spectrum_is_distinct,
        "temporal_method_disagreement_fraction": finite_float(temporal_disagreement)
        if temporal_disagreement is not None else None,
        "temporal_method_conflict": temporal_disagreement > 0.2
        if temporal_disagreement is not None else False,
        "preferred_spatial_spacing_px": finite_float(preferred_spatial) if preferred_spatial else None,
        "preferred_spatial_source": preferred_spatial_source,
        "spatial_method_disagreement_fraction": finite_float(spatial_disagreement)
        if spatial_disagreement is not None else None,
        "spatial_method_conflict": spatial_disagreement > 0.2
        if spatial_disagreement is not None else False,
        "spectrum": spectra,
        "crest_tracks": crest_tracks,
        "transect_bands": diagnostics.analyze_transect_bands(
            kymograph, times, line_length
        ),
    }
    return kymograph, result, {
        "spectrum_plot_data": spectrum_plot_data,
        "track_overlay": track_overlay,
        "line_length_px": line_length,
    }


def analyze_foam(
    frames: list[np.ndarray],
    times: np.ndarray,
    roi: tuple[int, int, int, int],
    direction: np.ndarray,
) -> tuple[dict[str, Any], np.ndarray]:
    x, y, width, height = roi
    samples = []
    stride_frames = max(1, len(frames) // 24)
    for frame in frames[::stride_frames]:
        hsv = cv2.cvtColor(frame[y:y + height, x:x + width], cv2.COLOR_BGR2HSV)
        samples.append(hsv[::6, ::6].reshape(-1, 3))
    sample = np.concatenate(samples, axis=0)
    saturation_threshold = int(min(115, np.percentile(sample[:, 1], 45)))
    value_threshold = int(max(145, np.percentile(sample[:, 2], 73)))
    kernel = np.ones((3, 3), dtype=np.uint8)
    occupancy = np.zeros((height, width), dtype=np.float64)
    coverage: list[float] = []
    grid_step = max(4, min(width, height) // 80)
    active = np.zeros((math.ceil(height / grid_step), math.ceil(width / grid_step)), dtype=np.int32)
    run_lengths: list[int] = []
    fronts: list[float] = []
    direction = np.asarray(direction, dtype=np.float64)
    direction /= max(float(np.linalg.norm(direction)), 1e-6)

    for frame in frames:
        hsv = cv2.cvtColor(frame[y:y + height, x:x + width], cv2.COLOR_BGR2HSV)
        mask = ((hsv[:, :, 1] <= saturation_threshold) & (hsv[:, :, 2] >= value_threshold)).astype(np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)
        occupancy += mask
        coverage.append(float(np.mean(mask)))
        grid = mask[::grid_step, ::grid_step].astype(bool)
        ended = (~grid) & (active > 0)
        run_lengths.extend(active[ended].tolist())
        active[grid] += 1
        active[~grid] = 0
        ys, xs = np.nonzero(mask)
        if len(xs) >= 24:
            projection = xs * direction[0] + ys * direction[1]
            fronts.append(float(np.percentile(projection, 95)))
        else:
            fronts.append(float("nan"))

    run_lengths.extend(active[active > 0].tolist())
    occupancy /= len(frames)
    time_step = float(np.median(np.diff(times)))
    persistence = float(np.median(run_lengths) * time_step) if run_lengths else 0.0
    coverage_array = np.asarray(coverage)
    front_array = np.asarray(fronts, dtype=np.float64)
    finite_fronts = front_array[np.isfinite(front_array)]
    runup = float(np.percentile(finite_fronts, 95) - np.percentile(finite_fronts, 5)) \
        if len(finite_fronts) >= 5 else None
    threshold_separation = max(0.0, (value_threshold / 255.0) - (saturation_threshold / 255.0))
    mean_coverage = float(np.mean(coverage_array))
    coverage_cv = float(np.std(coverage_array) / max(mean_coverage, 1e-6))
    persistent_fraction = float(np.mean(occupancy > 0.65))
    recurrent_fraction = float(np.mean(occupancy > 0.25))
    column_profile = np.mean(occupancy, axis=0)
    row_profile = np.mean(occupancy, axis=1)

    def concentrated_share(profile: np.ndarray) -> float:
        count = max(1, math.ceil(len(profile) * 0.25))
        total = float(np.sum(profile))
        if total <= 1e-9:
            return 0.0
        return float(np.sum(np.sort(profile)[-count:]) / total)

    column_concentration = concentrated_share(column_profile)
    row_concentration = concentrated_share(row_profile)
    weak_color_separation = saturation_threshold < 24
    static_candidate_field = mean_coverage > 0.08 and coverage_cv < 0.12
    specular_lane_candidate = recurrent_fraction > 0.015 and column_concentration > 0.42
    broad_recurrent_highlight_field = recurrent_fraction > 0.18 and mean_coverage > 0.10
    rejection_reasons = []
    if weak_color_separation:
        rejection_reasons.append("weak_color_separation")
    if static_candidate_field:
        rejection_reasons.append("candidate_field_is_too_static")
    if specular_lane_candidate:
        rejection_reasons.append("persistent_column_concentration_suggests_specular_glare")
    if broad_recurrent_highlight_field:
        rejection_reasons.append("broad_recurrent_highlight_field")
    static_highlight_score = min(
        1.0,
        persistent_fraction * 2.5
        + recurrent_fraction * 0.75
        + max(0.0, column_concentration - 0.35) * 1.5
        + (0.25 if weak_color_separation else 0.0)
        + max(0.0, mean_coverage - 0.25) * 1.5
        + max(0.0, 0.18 - coverage_cv) * 1.5,
    )
    static_highlight_risk = (
        "high" if rejection_reasons or static_highlight_score >= 0.38
        else "medium" if static_highlight_score >= 0.18
        else "low"
    )
    confidence = min(1.0, 0.35 + threshold_separation) if mean_coverage < 0.65 else 0.25
    if static_highlight_risk == "high":
        confidence = min(confidence, 0.25)
    events = diagnostics.detect_events(times, coverage_array)
    result = {
        "meaning": "Low-saturation, high-value white-water proxy; bright sand and sun glitter can be false positives.",
        "saturation_threshold_0_255": saturation_threshold,
        "value_threshold_0_255": value_threshold,
        "mean_coverage_fraction": finite_float(mean_coverage),
        "coverage_p05_fraction": finite_float(np.percentile(coverage_array, 5)),
        "coverage_p95_fraction": finite_float(np.percentile(coverage_array, 95)),
        "fixed_pixel_persistence_s": finite_float(persistence),
        "runup_proxy_px": finite_float(runup) if runup is not None else None,
        "persistent_highlight_fraction": finite_float(persistent_fraction),
        "recurrent_candidate_fraction": finite_float(recurrent_fraction),
        "candidate_column_concentration_top_quarter": finite_float(column_concentration),
        "candidate_row_concentration_top_quarter": finite_float(row_concentration),
        "coverage_variation_ratio": finite_float(coverage_cv),
        "glare_or_static_highlight_risk_score": finite_float(static_highlight_score),
        "glare_or_static_highlight_risk": static_highlight_risk,
        "metrics_accepted_automatically": not rejection_reasons and static_highlight_risk == "low",
        "automatic_rejection_reasons": rejection_reasons,
        "confidence_score": finite_float(confidence),
        "confidence": confidence_label(confidence),
        "events": events,
        "time_series": {
            "time_s": [finite_float(value) for value in times],
            "coverage_fraction": [finite_float(value) for value in coverage_array],
            "front_projection_px": [
                finite_float(value) if math.isfinite(value) else None
                for value in front_array
            ],
        },
    }
    return result, occupancy.astype(np.float32)


def analyze_palette(frames: list[np.ndarray], roi: tuple[int, int, int, int]) -> list[dict[str, Any]]:
    x, y, width, height = roi
    pixels = []
    for frame in frames[:: max(1, len(frames) // 16)]:
        rgb = cv2.cvtColor(frame[y:y + height, x:x + width], cv2.COLOR_BGR2RGB)
        pixels.append(rgb[::8, ::8].reshape(-1, 3))
    data = np.concatenate(pixels, axis=0)
    if len(data) > 30000:
        generator = np.random.default_rng(20260802)
        data = data[generator.choice(len(data), 30000, replace=False)]
    values = data.astype(np.float32)
    cv2.setRNGSeed(20260802)
    _, labels, centers = cv2.kmeans(
        values,
        5,
        None,
        (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_MAX_ITER, 60, 0.2),
        5,
        cv2.KMEANS_PP_CENTERS,
    )
    counts = np.bincount(labels.reshape(-1), minlength=len(centers))
    order = np.argsort(counts)[::-1]
    palette = []
    for index in order:
        color = np.clip(np.round(centers[index]), 0, 255).astype(int)
        palette.append(
            {
                "hex": "#{:02X}{:02X}{:02X}".format(*color.tolist()),
                "rgb_0_255": color.tolist(),
                "fraction": finite_float(counts[index] / max(1, np.sum(counts))),
            }
        )
    return palette


def save_contact_sheet(frames: list[np.ndarray], times: np.ndarray, path: Path) -> None:
    indices = np.linspace(0, len(frames) - 1, min(12, len(frames)), dtype=int)
    cell_width = 320
    aspect = frames[0].shape[0] / frames[0].shape[1]
    cell_height = int(round(cell_width * aspect)) + 30
    columns = 4
    rows = math.ceil(len(indices) / columns)
    sheet = np.full((rows * cell_height, columns * cell_width, 3), 22, dtype=np.uint8)
    for cell, frame_index in enumerate(indices):
        image_height = cell_height - 30
        image = cv2.resize(frames[frame_index], (cell_width, image_height), interpolation=cv2.INTER_AREA)
        row, column = divmod(cell, columns)
        top, left = row * cell_height, column * cell_width
        sheet[top:top + image_height, left:left + cell_width] = image
        cv2.putText(
            sheet,
            f"t={times[frame_index]:.2f}s  frame={frame_index}",
            (left + 8, top + image_height + 21),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.48,
            (235, 235, 235),
            1,
            cv2.LINE_AA,
        )
    cv2.imwrite(str(path), sheet, [cv2.IMWRITE_JPEG_QUALITY, 92])


def save_motion_overlay(
    frame: np.ndarray,
    roi: tuple[int, int, int, int],
    flow: np.ndarray,
    transect: tuple[float, float, float, float],
    path: Path,
) -> None:
    output = frame.copy()
    x, y, width, height = roi
    cv2.rectangle(output, (x, y), (x + width - 1, y + height - 1), (0, 220, 255), 2)
    step = max(20, min(width, height) // 12)
    for local_y in range(step // 2, height, step):
        for local_x in range(step // 2, width, step):
            vector = flow[local_y, local_x]
            if float(np.linalg.norm(vector)) < 0.05:
                continue
            origin = (x + local_x, y + local_y)
            target = (
                int(round(origin[0] + vector[0] * 7.0)),
                int(round(origin[1] + vector[1] * 7.0)),
            )
            cv2.arrowedLine(output, origin, target, (40, 255, 40), 1, cv2.LINE_AA, tipLength=0.3)
    x1, y1, x2, y2 = transect
    cv2.line(output, (round(x1), round(y1)), (round(x2), round(y2)), (255, 80, 255), 2, cv2.LINE_AA)
    cv2.imwrite(str(path), output, [cv2.IMWRITE_JPEG_QUALITY, 94])


def save_kymograph(kymograph: np.ndarray, path: Path) -> None:
    normalized = cv2.normalize(kymograph, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)
    colored = cv2.applyColorMap(normalized, cv2.COLORMAP_TURBO)
    target_height = max(240, min(720, len(kymograph) * 3))
    colored = cv2.resize(colored, (960, target_height), interpolation=cv2.INTER_NEAREST)
    cv2.putText(colored, "distance along transect ->", (18, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(colored, "time down", (18, 55), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA)
    cv2.imwrite(str(path), colored)


def save_foam_occupancy(occupancy: np.ndarray, path: Path) -> None:
    image = np.clip(occupancy * 255.0, 0, 255).astype(np.uint8)
    colored = cv2.applyColorMap(image, cv2.COLORMAP_INFERNO)
    cv2.putText(colored, "foam occupancy: black=never, white=frequent", (12, 26), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA)
    cv2.imwrite(str(path), colored)


def cross_check_apparent_speed(
    motion: dict[str, Any], periodicity: dict[str, Any]
) -> dict[str, Any]:
    """Reconcile the three independent apparent-speed estimates.

    Optical flow, crest tracking, and spacing-over-period measure the same screen-space
    quantity by different routes. Optical flow aliases on repeating crests and reads low,
    so a single number was never enough; this reports all three and refuses to pick a
    consensus when the geometric pair disagrees.
    """
    tracks = periodicity["crest_tracks"]
    estimates: dict[str, float] = {}
    flow_speed = motion.get("speed_px_s")
    if flow_speed:
        estimates["optical_flow_px_s"] = abs(float(flow_speed))
    track_speed = tracks.get("median_signed_speed_px_s")
    track_is_usable = bool(track_speed and tracks.get("speed_reliable"))
    if track_speed:
        estimates["crest_tracks_px_s"] = abs(float(track_speed))
    # Only derive spacing-over-period from values that survived their own acceptance tests;
    # the raw ratio is computed from autocorrelation peaks that may have been rejected.
    spacing = periodicity.get("preferred_spatial_spacing_px")
    period = periodicity.get("preferred_temporal_period_s")
    if (
        spacing
        and period
        and not periodicity.get("spatial_method_conflict")
        and not periodicity.get("temporal_method_conflict")
    ):
        estimates["spacing_over_period_px_s"] = abs(float(spacing) / float(period))

    # A crest-track speed whose own spread rejects it must not veto a clean
    # spacing-over-period estimate, so it is reported but kept out of the consensus.
    geometric = [
        estimates[name]
        for name in ("crest_tracks_px_s", "spacing_over_period_px_s")
        if name in estimates and (name != "crest_tracks_px_s" or track_is_usable)
    ]
    consensus: float | None = None
    source = "unresolved"
    agreement: float | None = None
    if len(geometric) == 2:
        agreement = abs(geometric[0] - geometric[1]) / max(np.mean(geometric), 1e-6)
        if agreement <= 0.25:
            consensus = float(np.mean(geometric))
            source = "crest_tracks_and_spacing_over_period_agree"
        else:
            source = "geometric_methods_disagree"
    elif len(geometric) == 1:
        consensus = geometric[0]
        source = "single_geometric_estimate"

    flow_error = (
        abs(estimates["optical_flow_px_s"] - consensus) / max(consensus, 1e-6)
        if consensus and "optical_flow_px_s" in estimates
        else None
    )
    if consensus is None:
        confidence = "low"
    elif source == "crest_tracks_and_spacing_over_period_agree":
        confidence = "high" if agreement is not None and agreement <= 0.12 else "medium"
    elif source == "single_geometric_estimate":
        confidence = "medium"
    else:
        confidence = "low"

    return {
        "meaning": "Screen-space speed of the apparent crest pattern; not fluid-particle speed.",
        "estimates_px_s": {name: finite_float(value) for name, value in estimates.items()},
        "crest_track_speed_used_in_consensus": track_is_usable,
        "geometric_agreement_fraction": finite_float(agreement) if agreement is not None else None,
        "consensus_px_s": finite_float(consensus) if consensus else None,
        "consensus_source": source,
        "optical_flow_error_fraction": finite_float(flow_error) if flow_error is not None else None,
        "optical_flow_agrees": bool(flow_error is not None and flow_error <= 0.35),
        "confidence": confidence,
    }


def build_shader_brief(
    analysis: dict[str, Any], meters_per_pixel: float
) -> dict[str, Any]:
    periodicity = analysis["periodicity"]
    motion = analysis["motion"]
    foam = analysis["foam"]
    spacing_px = periodicity.get("preferred_spatial_spacing_px")
    period_s = periodicity.get("preferred_temporal_period_s")
    period_confidence = (
        periodicity["spectrum"]["temporal_confidence"]
        if periodicity.get("preferred_temporal_source") == "spectrum"
        else periodicity["temporal_confidence"]
    )
    spacing_confidence = (
        periodicity["spectrum"]["spatial_confidence"]
        if periodicity.get("preferred_spatial_source") == "spectrum"
        else periodicity["spatial_confidence"]
    )
    period_conflict = bool(periodicity.get("temporal_method_conflict"))
    spacing_conflict = bool(periodicity.get("spatial_method_conflict"))
    if period_conflict:
        period_confidence = "low"
    if spacing_conflict:
        spacing_confidence = "low"
    stabilization = analysis["stabilization"]
    direction_requires_review = (
        not motion.get("consistent_with_transect_axis", True)
        or motion.get("direction_spread_deg", 180.0) > 45.0
        or motion.get("confidence") == "low"
        or (
            stabilization.get("enabled", False)
            and not stabilization.get("used_for_analysis", False)
        )
    )
    foam_is_accepted = foam["metrics_accepted_automatically"]
    speed_check = analysis["apparent_speed_cross_check"]
    consensus_speed_px_s = speed_check.get("consensus_px_s")
    wavelength_m = spacing_px * meters_per_pixel if spacing_px and meters_per_pixel > 0 else None
    phase_speed_m_s = (
        consensus_speed_px_s * meters_per_pixel
        if consensus_speed_px_s and meters_per_pixel > 0
        else None
    )
    metric_status = "measured_from_user_calibration" if meters_per_pixel > 0 else "unresolved_without_scale"
    inputs = {
        "apparent_pattern_speed_px_s": {
            "value": consensus_speed_px_s,
            "status": f"cross_checked_{speed_check['consensus_source']}",
            "confidence": speed_check["confidence"],
        },
        "dominant_period_s": {
            "value": period_s,
            "status": "candidate_requires_visual_review_method_conflict"
            if period_s and period_conflict
            else f"measured_image_periodicity_{periodicity.get('preferred_temporal_source')}"
            if period_s else "unresolved",
            "confidence": period_confidence,
        },
        "crest_spacing_px": {
            "value": spacing_px,
            "status": "candidate_requires_visual_review_method_conflict"
            if spacing_px and spacing_conflict
            else f"measured_image_periodicity_{periodicity.get('preferred_spatial_source')}"
            if spacing_px else "unresolved",
            "confidence": spacing_confidence,
        },
        "wavelength_m": {
            "value": finite_float(wavelength_m) if wavelength_m else None,
            "status": metric_status,
            "confidence": spacing_confidence if wavelength_m else "none",
        },
        "apparent_phase_speed_m_s": {
            "value": finite_float(phase_speed_m_s) if phase_speed_m_s else None,
            "status": metric_status,
            "confidence": speed_check["confidence"] if phase_speed_m_s else "none",
        },
        "screen_direction_xy": {
            "value": motion["direction_image_xy"],
            "status": "candidate_requires_review_camera_spread_or_axis"
            if direction_requires_review
            else "measured_screen_space_only",
            "confidence": "low" if direction_requires_review else motion["confidence"],
        },
        "foam_decay_seed_s": {
            "value": foam["fixed_pixel_persistence_s"] if foam_is_accepted else None,
            "status": (
                "measured_white_water_proxy"
                if foam_is_accepted
                else "rejected_glare_or_static_highlight_proxy"
            ),
            "confidence": foam["confidence"] if foam_is_accepted else "none",
        },
        "foam_coverage_target": {
            "value": foam["mean_coverage_fraction"] if foam_is_accepted else None,
            "status": (
                "measured_white_water_proxy"
                if foam_is_accepted
                else "rejected_glare_or_static_highlight_proxy"
            ),
            "confidence": foam["confidence"] if foam_is_accepted else "none",
        },
        "temporal_modes_s": {
            "value": periodicity["spectrum"]["temporal_peaks"],
            "status": "multi_peak_image_spectrum",
            "confidence": periodicity["spectrum"]["temporal_confidence"],
        },
        "crest_line_orientation": {
            "value": analysis["crest_structure"],
            "status": "measured_screen_space_only",
            "confidence": analysis["crest_structure"]["confidence"],
        },
        "appearance": {
            "value": analysis["appearance"],
            "status": "display_referred_camera_appearance",
            "confidence": "medium",
        },
        "color_palette": {
            "value": analysis["palette"],
            "status": "measured_display_referred_color",
            "confidence": "medium",
        },
    }
    return {
        "contract": "Use measured values as comparison targets. Do not silently convert screen-space evidence into world-space physics.",
        "inputs": inputs,
        "required_modules": [
            "directional low-frequency displacement bank",
            "shore-distance or bathymetry driven shoaling",
            "crest curvature or compression foam source",
            "temporal foam persistence and advection",
            "scene-depth shoreline intersection",
            "distance-faded micro-normal layers",
            "depth-aware absorption, refraction, and Fresnel reflection",
        ],
        "unresolved_without_additional_evidence": [
            "wave amplitude and vertical height",
            "world-space propagation direction",
            "water depth and bathymetry",
            "camera focal length and water-plane rectification",
            "fluid-particle velocity",
            "underwater optical coefficients independent of camera grading",
        ],
        "validation_order": [
            "match camera, sun, exposure, and playback time",
            "match crest travel and spacing",
            "match breaking location and timing",
            "match foam birth, advection, occupancy, and decay",
            "match depth color, refraction, reflection, and micro detail",
            "run Shader Vision A/B and performance checks",
        ],
    }


def render_report(analysis: dict[str, Any], brief: dict[str, Any]) -> str:
    source = analysis["source"]
    motion = analysis["motion"]
    periodicity = analysis["periodicity"]
    foam = analysis["foam"]
    stabilization = analysis["stabilization"]
    appearance = analysis["appearance"]
    crest_structure = analysis["crest_structure"]
    speed_check = analysis["apparent_speed_cross_check"]
    palette = ", ".join(entry["hex"] for entry in analysis["palette"])
    speed_rows = "\n".join(
        f"| {name.replace('_px_s', '').replace('_', ' ')} | {value} |"
        for name, value in speed_check["estimates_px_s"].items()
    ) or "| no estimate | - |"
    mode_rows = "\n".join(
        f"| {peak['period_s']} | {peak['power_fraction']} | {peak['prominence_over_median']} |"
        for peak in periodicity["spectrum"]["temporal_peaks"]
    ) or "| - | - | - |"
    if speed_check["consensus_px_s"]:
        speed_verdict = (
            f"{speed_check['consensus_px_s']} px/s ({speed_check['confidence']} confidence, "
            f"{speed_check['consensus_source']}); optical flow agrees: "
            f"{speed_check['optical_flow_agrees']} (error {speed_check['optical_flow_error_fraction']})"
        )
    else:
        speed_verdict = (
            f"unresolved ({speed_check['consensus_source']}) - no method survived its own "
            "acceptance test, so do not carry a crest speed into the shader from this run"
        )
    warnings = [warning for warning in analysis["warnings"] if warning]
    warning_lines = "\n".join(f"- {warning}" for warning in warnings) or "- No automatic warning; visual inspection is still required."
    modules = "\n".join(f"- {module}" for module in brief["required_modules"])
    unresolved = "\n".join(f"- {item}" for item in brief["unresolved_without_additional_evidence"])
    return f"""# Wave Video Analysis

## Source

- File: `{source['path']}`
- Interval: {source['analysis_start_s']}s to {source['analysis_end_s']}s
- Samples: {source['analysis_frames']} at {source['sample_fps']} fps
- Analysis size: {source['analysis_width']} x {source['analysis_height']}
- ROI: {analysis['roi_xywh']}

## Camera solve

- Stabilization: {stabilization['enabled']} ({stabilization.get('method')})
- Camera motion: {stabilization.get('camera_motion')}
- Frames used for measurements: {stabilization['analysis_frame_source']}
- Median feature inlier ratio: {stabilization['median_inlier_ratio']}
- Jitter px/pair before -> after: {stabilization.get('jitter_px_per_pair_before')} -> {stabilization.get('jitter_px_per_pair_after')} (reduction {stabilization.get('jitter_reduction_ratio')}x)
- Minimum valid stabilized frame fraction: {stabilization.get('minimum_valid_frame_fraction')}
- Rejection reasons: {stabilization.get('rejection_reasons') or 'none'}
- Confidence: {stabilization['confidence']}

## Apparent crest speed (three independent methods)

| method | px/s |
| --- | --- |
{speed_rows}

- Cross-checked consensus: {speed_verdict}

Optical flow measures direction well and under-reads speed on repeating crests. Use the
consensus value as the shader target and optical flow only for direction.

## Measured image evidence

- Apparent direction XY: {motion['direction_image_xy']} ({motion['direction_confidence']} direction confidence)
- Direction coherence: {motion['direction_coherence']}
- Motion/transect axis alignment: {motion.get('transect_axis_alignment')} (consistent={motion.get('consistent_with_transect_axis')})
- Autocorrelation band spacing: {periodicity['spatial_spacing_px']} px ({periodicity['spatial_confidence']} confidence)
- Autocorrelation temporal period: {periodicity['temporal_period_s']} s ({periodicity['temporal_confidence']} confidence)
- Accepted crest spacing: {periodicity['preferred_spatial_spacing_px']} px from {periodicity['preferred_spatial_source']}
- Accepted period: {periodicity['preferred_temporal_period_s']} s from {periodicity['preferred_temporal_source']}
- Crest tracks: {periodicity['crest_tracks']['track_count']} accepted, reliable={periodicity['crest_tracks']['speed_reliable']}

### Spectral temporal modes

| period s | power fraction | prominence over median |
| --- | --- | --- |
{mode_rows}

- White-water coverage: {foam['mean_coverage_fraction']}
- Fixed-pixel white-water persistence: {foam['fixed_pixel_persistence_s']} s
- Shoreward run-up proxy: {foam['runup_proxy_px']} px ({foam.get('runup_proxy_status')})
- Foam/glare risk: {foam['glare_or_static_highlight_risk']} (automatic acceptance: {foam['metrics_accepted_automatically']})
- Crest-line angle: {crest_structure['dominant_crest_line_angle_deg']} degrees ({crest_structure['confidence']} confidence)
- Trough / mid / highlight colors: {appearance['trough_color']['hex']} / {appearance['mid_color']['hex']} / {appearance['highlight_color']['hex']}
- Clipped pixels: {appearance['clipped_pixel_fraction']}
- Dominant display colors: {palette}

## Automatic warnings

{warning_lines}

## Unity modules indicated by the reference

{modules}

## Not recoverable from this uncalibrated clip

{unresolved}

## Review files

- `contact_sheet.jpg`: twelve-frame orientation summary only.
- `source_timeline_*.jpg`: up to {analysis['review_manifest']['requested_review_frames']} evenly sampled raw frames across several readable pages.
- `roi_timeline_*.jpg`: the same dense frame set cropped to the measured water region.
- `stabilization_review.jpg` and `camera_timeline.png`: raw/stabilized pairs and camera-solve evidence.
- `motion_overlay.jpg`: green arrows are residual image motion; magenta is the measurement transect.
- `motion_direction_rose.png`, `motion_timeline.png`, and `crest_orientation.png`: direction spread, time variation, and crest alignment.
- `kymograph.png`: coherent diagonal bands support repeatable crest travel.
- `kymograph_tracks.png`, `band_kymographs.png`, `temporal_spectrum.png`, and `spatial_spectrum.png`: tracked crests, three transect zones, and multiple wave modes.
- `foam_occupancy.png`, `foam_mask_review_*.jpg`, `foam_timeline.png`, and `foam_event_sheet.jpg`: verify every white-water claim against dense source frames.
- `unity_shader_brief.json`: implementation inputs with provenance and confidence.

Do not call this a physical wave reconstruction until camera geometry, scale, and playback speed are calibrated. Use the results as measurable rendering targets and validate the Unity result with fixed Shader Vision captures.
"""


def main() -> None:
    args = parse_args()
    if not args.video.exists() or not args.video.is_file():
        fail(f"Video file does not exist: {args.video}")
    if args.meters_per_pixel < 0:
        fail("--meters-per-pixel must be positive")
    args.review_frames = int(np.clip(args.review_frames, 12, MAX_REVIEW_FRAMES))

    frames, times, source = read_video(args)
    width = int(source["analysis_width"])
    height = int(source["analysis_height"])
    try:
        roi = parse_rect(args.roi, width, height)
        user_transect = parse_transect(args.transect, width, height)
    except ValueError as exc:
        fail(str(exc))

    output = args.output
    if output is None:
        output = Path("Artifacts") / "VideoAnalysis" / safe_slug(args.video.stem)
    output.mkdir(parents=True, exist_ok=True)

    stabilized, valid_masks, stabilization = stabilize_frames(
        frames, times, not args.no_stabilization
    )
    if stabilization["used_for_analysis"]:
        analysis_frames = stabilized
        analysis_masks = valid_masks
        frame_source = "stabilized"
    else:
        analysis_frames = frames
        analysis_masks = [
            np.full(frame.shape[:2], 255, dtype=np.uint8) for frame in frames
        ]
        frame_source = "raw"
    stabilization["analysis_frame_source"] = frame_source

    motion, average_flow = analyze_motion(
        analysis_frames, analysis_masks, times, roi
    )
    direction = np.asarray(motion["direction_image_xy"], dtype=np.float64)
    transect = user_transect or line_through_rect(roi, direction)
    transect_direction = np.asarray(
        [transect[2] - transect[0], transect[3] - transect[1]], dtype=np.float64
    )
    transect_direction /= max(float(np.linalg.norm(transect_direction)), 1e-6)
    motion_direction = direction / max(float(np.linalg.norm(direction)), 1e-6)
    transect_alignment = abs(float(np.dot(motion_direction, transect_direction)))
    motion["transect_axis_alignment"] = finite_float(transect_alignment)
    motion["consistent_with_transect_axis"] = transect_alignment >= 0.55
    kymograph, periodicity, kymograph_aux = analyze_kymograph(
        analysis_frames, times, transect
    )
    foam, occupancy = analyze_foam(analysis_frames, times, roi, direction)
    palette = analyze_palette(analysis_frames, roi)
    appearance = diagnostics.analyze_appearance(analysis_frames, roi)
    crest_structure = diagnostics.analyze_crest_orientation(analysis_frames, roi)

    glare_score = max(
        float(foam["glare_or_static_highlight_risk_score"]),
        min(
            1.0,
            float(appearance["low_saturation_highlight_fraction"]) * 5.0
            + float(foam["persistent_highlight_fraction"]) * 1.5,
        ),
    )
    foam["glare_or_static_highlight_risk_score"] = finite_float(glare_score)
    rejection_reasons = list(foam.get("automatic_rejection_reasons", []))
    if appearance["low_saturation_highlight_fraction"] > 0.08:
        rejection_reasons.append("broad_low_saturation_highlights")
    foam["automatic_rejection_reasons"] = rejection_reasons
    foam["glare_or_static_highlight_risk"] = (
        "high" if rejection_reasons or glare_score >= 0.38
        else "medium" if glare_score >= 0.18
        else "low"
    )
    foam["metrics_accepted_automatically"] = not rejection_reasons and glare_score < 0.38
    if not foam["metrics_accepted_automatically"]:
        foam["confidence_score"] = min(float(foam["confidence_score"]), 0.25)
        foam["confidence"] = "low"

    speed_check = cross_check_apparent_speed(motion, periodicity)
    motion["speed_confidence"] = (
        speed_check["confidence"] if speed_check["optical_flow_agrees"] else "low"
    )

    # The run-up proxy projects the foam front onto the optical-flow direction, so it is
    # only meaningful when that direction itself is trustworthy.
    if motion["direction_confidence"] == "low" or not motion["consistent_with_transect_axis"]:
        foam["runup_proxy_px"] = None
        foam["runup_proxy_status"] = "unresolved_direction_is_unreliable"
    else:
        foam["runup_proxy_status"] = "measured_along_optical_flow_direction"

    warnings = [stabilization.get("warning")]
    if motion["confidence"] == "low":
        warnings.append("Optical-flow direction is weak or incoherent; do not map it to world direction yet.")
    if not motion.get("consistent_with_transect_axis", True):
        warnings.append("Optical-flow direction conflicts with the selected transect axis; treat screen direction as unresolved until the glare, camera, or transect cause is reviewed.")
    if periodicity["temporal_confidence"] == "low":
        warnings.append("No strong temporal cycle was found; use a longer interval or a cleaner transect.")
    if periodicity["spatial_confidence"] == "low":
        warnings.append("No strong crest spacing was found; inspect the kymograph and adjust the transect.")
    if periodicity.get("temporal_method_conflict"):
        warnings.append("Temporal autocorrelation and FFT disagree by more than 20 percent; the reported spectrum period is a visual-review candidate, not an accepted physical period.")
    if periodicity.get("spatial_method_conflict"):
        warnings.append("Spatial autocorrelation and FFT disagree by more than 20 percent; do not convert crest spacing to a shader wavelength without a cleaner transect.")
    if foam["mean_coverage_fraction"] is not None and foam["mean_coverage_fraction"] > 0.55:
        warnings.append("The white-water mask covers most of the ROI and may be selecting glare, sky, or sand.")
    if foam["glare_or_static_highlight_risk"] == "high":
        reasons = ", ".join(foam.get("automatic_rejection_reasons", [])) or "highlight ambiguity"
        warnings.append(f"White-water metrics are rejected automatically ({reasons}). Inspect every foam_mask_review page.")
    if stabilization.get("cut_candidates"):
        warnings.append("Possible camera cut or abrupt exposure transition detected; split the clip before using temporal metrics.")
    if speed_check["consensus_source"] == "geometric_methods_disagree":
        warnings.append(
            "Crest tracking and spacing-over-period disagree by more than 25 percent; apparent crest speed is unresolved until the transect and kymograph are reviewed."
        )
    elif speed_check["consensus_px_s"] and not speed_check["optical_flow_agrees"]:
        warnings.append(
            f"Optical-flow speed ({motion['speed_px_s']} px/s) disagrees with the cross-checked crest speed ({speed_check['consensus_px_s']} px/s). Optical flow aliases on repeating crests; use the cross-checked value and optical flow only for direction."
        )
    if args.meters_per_pixel <= 0:
        warnings.append("No scale was supplied; metric wavelength, speed, and amplitude remain unresolved.")

    analysis = {
        "schema_version": 3,
        "source": source,
        "roi_xywh": list(roi),
        "stabilization": stabilization,
        "motion": motion,
        "apparent_speed_cross_check": speed_check,
        "periodicity": periodicity,
        "foam": foam,
        "palette": palette,
        "appearance": appearance,
        "crest_structure": crest_structure,
        "calibration": {
            "meters_per_pixel": finite_float(args.meters_per_pixel) if args.meters_per_pixel > 0 else None,
            "scope": "Assumed constant on the analyzed water plane." if args.meters_per_pixel > 0 else "No metric calibration supplied.",
        },
        "warnings": warnings,
    }
    source_timeline = diagnostics.save_timeline_pages(
        frames, times, output, "source_timeline", args.review_frames
    )
    # ROI and foam pages must show the frames the metrics were computed on, otherwise a
    # stabilized run reviews a different set of pixels than it measured.
    roi_timeline = diagnostics.save_timeline_pages(
        analysis_frames, times, output, "roi_timeline", args.review_frames, roi
    )
    foam_reviews = diagnostics.save_foam_mask_pages(
        analysis_frames,
        times,
        roi,
        int(foam["saturation_threshold_0_255"]),
        int(foam["value_threshold_0_255"]),
        output,
        args.review_frames,
    )
    analysis["review_manifest"] = {
        "requested_review_frames": args.review_frames,
        "source_timeline_pages": source_timeline,
        "roi_timeline_pages": roi_timeline,
        "foam_mask_review_pages": foam_reviews,
        "stabilization_review": "stabilization_review.jpg",
        "event_sheet": "foam_event_sheet.jpg" if foam["events"] else None,
    }
    brief = build_shader_brief(analysis, args.meters_per_pixel)

    save_contact_sheet(frames, times, output / "contact_sheet.jpg")
    save_motion_overlay(
        analysis_frames[len(analysis_frames) // 2],
        roi,
        average_flow,
        transect,
        output / "motion_overlay.jpg",
    )
    save_kymograph(kymograph, output / "kymograph.png")
    save_foam_occupancy(occupancy, output / "foam_occupancy.png")
    diagnostics.save_stabilization_review(
        frames, stabilized, times, output / "stabilization_review.jpg"
    )
    diagnostics.save_track_overlay(
        kymograph_aux["track_overlay"], output / "kymograph_tracks.png"
    )
    diagnostics.save_band_kymographs(
        kymograph, output / "band_kymographs.png"
    )
    diagnostics.save_orientation_plot(
        crest_structure, output / "crest_orientation.png", "crest-line orientation"
    )
    diagnostics.save_direction_rose(
        motion["direction_histogram_360"], output / "motion_direction_rose.png"
    )
    spectrum_data = kymograph_aux["spectrum_plot_data"]
    time_step = float(np.median(np.diff(times)))
    duration = max(time_step, float(times[-1] - times[0]))
    diagnostics.save_spectrum_plot(
        *spectrum_data["temporal"],
        output / "temporal_spectrum.png",
        "temporal wave modes",
        "period seconds",
        max(1.0 / max(duration * 0.75, 0.5), 0.08),
        min(2.5, 0.5 / max(time_step, 1e-6)),
    )
    line_length = float(kymograph_aux["line_length_px"])
    diagnostics.save_spectrum_plot(
        *spectrum_data["spatial"],
        output / "spatial_spectrum.png",
        "spatial wave modes",
        "spacing pixels",
        1.0 / max(line_length * 0.75, 8.0),
        1.0 / 6.0,
    )

    motion_series = motion["time_series"]
    if motion_series:
        motion_times = np.asarray([item["time_s"] for item in motion_series], dtype=np.float64)
        diagnostics.save_series_plot(
            motion_times,
            [
                ("speed px/s", np.asarray([item["speed_px_s"] for item in motion_series]), (60, 220, 255)),
                ("direction coherence", np.asarray([item["coherence"] for item in motion_series]), (80, 255, 100)),
            ],
            output / "motion_timeline.png",
            "apparent motion through time",
        )
    foam_times = np.asarray(foam["time_series"]["time_s"], dtype=np.float64)
    foam_front = np.asarray([
        value if value is not None else np.nan
        for value in foam["time_series"]["front_projection_px"]
    ], dtype=np.float64)
    diagnostics.save_series_plot(
        foam_times,
        [
            ("white-water coverage", np.asarray(foam["time_series"]["coverage_fraction"]), (70, 220, 255)),
            ("front projection px", foam_front, (255, 100, 210)),
        ],
        output / "foam_timeline.png",
        "white-water candidate and run-up proxy",
    )
    camera_series = stabilization.get("pair_diagnostics", [])
    if camera_series:
        camera_times = np.asarray([item["time_s"] for item in camera_series], dtype=np.float64)
        diagnostics.save_series_plot(
            camera_times,
            [
                ("translation px", np.asarray([item["translation_px"] for item in camera_series]), (60, 220, 255)),
                ("inlier ratio", np.asarray([item["inlier_ratio"] for item in camera_series]), (80, 255, 100)),
                ("valid frame", np.asarray([item["valid_frame_fraction"] for item in camera_series]), (255, 120, 220)),
            ],
            output / "camera_timeline.png",
            "camera solve diagnostics",
        )
    diagnostics.save_event_sheet(
        analysis_frames, times, roi, foam["events"], output / "foam_event_sheet.jpg"
    )
    (output / "analysis.json").write_text(json.dumps(analysis, indent=2), encoding="utf-8")
    (output / "unity_shader_brief.json").write_text(json.dumps(brief, indent=2), encoding="utf-8")
    (output / "report.md").write_text(render_report(analysis, brief), encoding="utf-8")

    print(json.dumps({
        "status": "ok",
        "output": str(output.resolve()),
        "frames": source["analysis_frames"],
        "motion_confidence": motion["confidence"],
        "period_s": periodicity["preferred_temporal_period_s"],
        "spacing_px": periodicity["preferred_spatial_spacing_px"],
        "apparent_speed_px_s": speed_check["consensus_px_s"],
        "apparent_speed_source": speed_check["consensus_source"],
        "stabilization_used": stabilization["used_for_analysis"],
        "camera_motion": stabilization.get("camera_motion"),
        "review_frames": args.review_frames,
        "foam_metrics_accepted": foam["metrics_accepted_automatically"],
        "foam_persistence_s": foam["fixed_pixel_persistence_s"],
    }, indent=2))


if __name__ == "__main__":
    main()
