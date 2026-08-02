"""Dense visual and signal diagnostics for analyze_wave_video.py."""

from __future__ import annotations

import math
from pathlib import Path
from typing import Any

import cv2
import numpy as np


PAGE_SIZE = 24


def number(value: Any, digits: int = 4) -> float | None:
    result = float(value)
    return round(result, digits) if math.isfinite(result) else None


def confidence(value: float) -> str:
    if value >= 0.72:
        return "high"
    if value >= 0.42:
        return "medium"
    return "low"


def selected_indices(frame_count: int, requested: int) -> np.ndarray:
    count = max(1, min(frame_count, requested))
    return np.unique(np.linspace(0, frame_count - 1, count, dtype=int))


def _resize_cell(image: np.ndarray, cell_width: int, cell_height: int) -> np.ndarray:
    height, width = image.shape[:2]
    scale = min(cell_width / max(1, width), cell_height / max(1, height))
    resized = cv2.resize(
        image,
        (max(1, round(width * scale)), max(1, round(height * scale))),
        interpolation=cv2.INTER_AREA if scale < 1 else cv2.INTER_LINEAR,
    )
    canvas = np.full((cell_height, cell_width, 3), 18, dtype=np.uint8)
    top = (cell_height - resized.shape[0]) // 2
    left = (cell_width - resized.shape[1]) // 2
    canvas[top:top + resized.shape[0], left:left + resized.shape[1]] = resized
    return canvas


def _write_cell(
    sheet: np.ndarray,
    image: np.ndarray,
    label: str,
    cell: int,
    columns: int,
    cell_width: int,
    image_height: int,
) -> None:
    row, column = divmod(cell, columns)
    top = row * (image_height + 28)
    left = column * cell_width
    sheet[top:top + image_height, left:left + cell_width] = _resize_cell(
        image, cell_width, image_height
    )
    cv2.putText(
        sheet,
        label,
        (left + 6, top + image_height + 20),
        cv2.FONT_HERSHEY_SIMPLEX,
        0.43,
        (235, 235, 235),
        1,
        cv2.LINE_AA,
    )


def save_timeline_pages(
    frames: list[np.ndarray],
    times: np.ndarray,
    output: Path,
    prefix: str,
    review_frames: int,
    roi: tuple[int, int, int, int] | None = None,
) -> list[str]:
    indices = selected_indices(len(frames), review_frames)
    sample = frames[0] if roi is None else frames[0][roi[1]:roi[1] + roi[3], roi[0]:roi[0] + roi[2]]
    portrait = sample.shape[0] > sample.shape[1] * 1.15
    columns = 6 if portrait else 4
    cell_width = 220 if portrait else 320
    image_height = 390 if portrait else 200
    files: list[str] = []
    for page_index, start in enumerate(range(0, len(indices), PAGE_SIZE), 1):
        page_indices = indices[start:start + PAGE_SIZE]
        rows = math.ceil(len(page_indices) / columns)
        sheet = np.full(
            (rows * (image_height + 28), columns * cell_width, 3),
            18,
            dtype=np.uint8,
        )
        for cell, frame_index in enumerate(page_indices):
            frame = frames[frame_index]
            if roi is not None:
                x, y, width, height = roi
                frame = frame[y:y + height, x:x + width]
            _write_cell(
                sheet,
                frame,
                f"t={times[frame_index]:.2f}s  sample={frame_index}",
                cell,
                columns,
                cell_width,
                image_height,
            )
        name = f"{prefix}_{page_index:02d}.jpg"
        cv2.imwrite(str(output / name), sheet, [cv2.IMWRITE_JPEG_QUALITY, 92])
        files.append(name)
    return files


def save_stabilization_review(
    raw_frames: list[np.ndarray],
    stabilized_frames: list[np.ndarray],
    times: np.ndarray,
    path: Path,
) -> None:
    indices = selected_indices(len(raw_frames), 10)
    cell_width = 300
    image_height = 190
    sheet = np.full((len(indices) * (image_height + 26), cell_width * 2, 3), 16, dtype=np.uint8)
    for row, frame_index in enumerate(indices):
        top = row * (image_height + 26)
        sheet[top:top + image_height, :cell_width] = _resize_cell(
            raw_frames[frame_index], cell_width, image_height
        )
        sheet[top:top + image_height, cell_width:] = _resize_cell(
            stabilized_frames[frame_index], cell_width, image_height
        )
        label = f"RAW  t={times[frame_index]:.2f}s"
        cv2.putText(sheet, label, (6, top + image_height + 19), cv2.FONT_HERSHEY_SIMPLEX, 0.43, (235, 235, 235), 1, cv2.LINE_AA)
        cv2.putText(sheet, "STABILIZED", (cell_width + 6, top + image_height + 19), cv2.FONT_HERSHEY_SIMPLEX, 0.43, (235, 235, 235), 1, cv2.LINE_AA)
    cv2.imwrite(str(path), sheet, [cv2.IMWRITE_JPEG_QUALITY, 92])


def _aggregate_spectrum(matrix: np.ndarray, sample_spacing: float, axis: int) -> tuple[np.ndarray, np.ndarray]:
    values = matrix.astype(np.float64)
    values -= np.mean(values, axis=axis, keepdims=True)
    length = values.shape[axis]
    window_shape = [1] * values.ndim
    window_shape[axis] = length
    values *= np.hanning(length).reshape(window_shape)
    spectrum = np.fft.rfft(values, axis=axis)
    power = np.abs(spectrum) ** 2
    other_axes = tuple(index for index in range(power.ndim) if index != axis)
    aggregate = np.median(power, axis=other_axes) if other_axes else power
    frequencies = np.fft.rfftfreq(length, d=max(sample_spacing, 1e-6))
    return frequencies, aggregate


def _find_spectral_peaks(
    frequencies: np.ndarray,
    power: np.ndarray,
    minimum_frequency: float,
    maximum_frequency: float,
    value_name: str,
) -> tuple[list[dict[str, Any]], float]:
    mask = (frequencies >= minimum_frequency) & (frequencies <= maximum_frequency)
    valid = np.flatnonzero(mask)
    if len(valid) < 3 or float(np.sum(power[valid])) <= 1e-12:
        return [], 0.0
    candidates = [
        index for index in valid[1:-1]
        if power[index] >= power[index - 1] and power[index] > power[index + 1]
    ]
    if not candidates:
        candidates = [int(valid[np.argmax(power[valid])])]
    total = float(np.sum(power[valid]))
    median_power = float(np.median(power[valid])) + 1e-12
    chosen: list[int] = []
    for index in sorted(candidates, key=lambda item: power[item], reverse=True):
        if any(abs(index - previous) < 2 for previous in chosen):
            continue
        chosen.append(index)
        if len(chosen) == 4:
            break
    peaks = []
    for index in chosen:
        frequency = float(frequencies[index])
        item = {
            "frequency": number(frequency),
            value_name: number(1.0 / frequency),
            "power_fraction": number(power[index] / total),
            "prominence_over_median": number(power[index] / median_power),
        }
        peaks.append(item)
    dominant_share = float(peaks[0]["power_fraction"]) if peaks else 0.0
    dominant_prominence = float(peaks[0]["prominence_over_median"]) if peaks else 0.0
    score = min(1.0, dominant_share * 5.0) * min(1.0, math.log10(max(1.0, dominant_prominence)) / 2.0)
    return peaks, score


def analyze_spectra(
    kymograph: np.ndarray,
    times: np.ndarray,
    line_length_px: float,
) -> tuple[dict[str, Any], dict[str, tuple[np.ndarray, np.ndarray]]]:
    time_step = float(np.median(np.diff(times)))
    spatial_step = line_length_px / max(1, kymograph.shape[1] - 1)
    temporal_frequencies, temporal_power = _aggregate_spectrum(kymograph, time_step, axis=0)
    spatial_frequencies, spatial_power = _aggregate_spectrum(kymograph, spatial_step, axis=1)
    duration = max(time_step, float(times[-1] - times[0]))
    temporal_peaks, temporal_score = _find_spectral_peaks(
        temporal_frequencies,
        temporal_power,
        max(1.0 / max(duration * 0.75, 0.5), 0.08),
        min(2.5, 0.5 / max(time_step, 1e-6)),
        "period_s",
    )
    spatial_peaks, spatial_score = _find_spectral_peaks(
        spatial_frequencies,
        spatial_power,
        1.0 / max(line_length_px * 0.75, 8.0),
        1.0 / 6.0,
        "spacing_px",
    )
    result = {
        "temporal_peaks": temporal_peaks,
        "temporal_confidence_score": number(temporal_score),
        "temporal_confidence": confidence(temporal_score),
        "spatial_peaks": spatial_peaks,
        "spatial_confidence_score": number(spatial_score),
        "spatial_confidence": confidence(spatial_score),
    }
    return result, {
        "temporal": (temporal_frequencies, temporal_power),
        "spatial": (spatial_frequencies, spatial_power),
    }


def save_spectrum_plot(
    frequencies: np.ndarray,
    power: np.ndarray,
    path: Path,
    title: str,
    value_label: str,
    minimum_frequency: float,
    maximum_frequency: float,
) -> None:
    width, height = 1000, 420
    canvas = np.full((height, width, 3), 20, dtype=np.uint8)
    mask = (frequencies >= minimum_frequency) & (frequencies <= maximum_frequency)
    indices = np.flatnonzero(mask)
    if len(indices) >= 2:
        axis_values = 1.0 / np.maximum(frequencies[indices], 1e-9)
        order = np.argsort(axis_values)
        axis_values = axis_values[order]
        values = np.log1p(power[indices][order])
        values -= np.min(values)
        values /= max(float(np.max(values)), 1e-9)
        axis_span = max(float(np.ptp(axis_values)), 1e-9)
        xs = (70 + (axis_values - axis_values[0]) / axis_span * (width - 100)).astype(int)
        ys = (height - 55 - values * (height - 110)).astype(int)
        points = np.column_stack([xs, ys]).reshape(-1, 1, 2)
        cv2.polylines(canvas, [points], False, (60, 220, 255), 2, cv2.LINE_AA)
        for fraction in (0.0, 0.25, 0.5, 0.75, 1.0):
            value = float(axis_values[0] + fraction * axis_span)
            x = round(70 + fraction * (width - 100))
            cv2.putText(canvas, f"{value:.2f}", (x - 18, height - 24), cv2.FONT_HERSHEY_SIMPLEX, 0.42, (205, 205, 205), 1, cv2.LINE_AA)
    cv2.putText(canvas, title, (24, 34), cv2.FONT_HERSHEY_SIMPLEX, 0.72, (245, 245, 245), 2, cv2.LINE_AA)
    cv2.putText(canvas, f"horizontal axis: {value_label}, power is logarithmic", (24, 58), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (190, 190, 190), 1, cv2.LINE_AA)
    cv2.imwrite(str(path), canvas)


def analyze_crest_tracks(
    kymograph: np.ndarray,
    times: np.ndarray,
    line_length_px: float,
) -> tuple[dict[str, Any], np.ndarray]:
    normalized = cv2.normalize(kymograph, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)
    enhanced = cv2.absdiff(normalized, cv2.GaussianBlur(normalized, (0, 0), 2.0))
    edges = cv2.Canny(enhanced, 35, 100)
    lines = cv2.HoughLinesP(
        edges,
        1,
        np.pi / 180.0,
        threshold=max(16, min(kymograph.shape) // 10),
        minLineLength=max(18, min(kymograph.shape) // 8),
        maxLineGap=10,
    )
    time_step = float(np.median(np.diff(times)))
    spatial_step = line_length_px / max(1, kymograph.shape[1] - 1)
    candidates: list[dict[str, Any]] = []
    overlay = cv2.cvtColor(normalized, cv2.COLOR_GRAY2BGR)
    if lines is not None:
        for raw in np.asarray(lines).reshape(-1, 4):
            x1, y1, x2, y2 = [int(value) for value in raw]
            if y2 < y1:
                x1, y1, x2, y2 = x2, y2, x1, y1
            dx = x2 - x1
            dy = y2 - y1
            if abs(dy) < max(5, kymograph.shape[0] * 0.035) or abs(dx) < 3:
                continue
            speed = dx * spatial_step / (dy * time_step)
            lifetime = abs(dy) * time_step
            if abs(speed) > line_length_px / max(time_step, 1e-6):
                continue
            candidates.append({
                "speed_px_s": number(speed),
                "lifetime_s": number(lifetime),
                "line": [x1, y1, x2, y2],
                "midpoint": [(x1 + x2) * 0.5, (y1 + y2) * 0.5],
            })
    raw_candidate_count = len(candidates)
    if candidates:
        candidate_speeds = np.asarray(
            [candidate["speed_px_s"] for candidate in candidates], dtype=np.float64
        )
        center = float(np.median(candidate_speeds))
        mad = float(np.median(np.abs(candidate_speeds - center)))
        speed_limit = max(4.0, mad * 4.5)
        candidates = [
            candidate for candidate in candidates
            if abs(candidate["speed_px_s"] - center) <= speed_limit
        ]
    candidates.sort(key=lambda item: item["lifetime_s"], reverse=True)
    tracks: list[dict[str, Any]] = []
    for candidate in candidates:
        midpoint = np.asarray(candidate["midpoint"], dtype=np.float64)
        duplicate = False
        for accepted in tracks:
            accepted_midpoint = np.asarray(accepted["midpoint"], dtype=np.float64)
            speed_tolerance = max(3.0, abs(accepted["speed_px_s"]) * 0.22)
            if (
                float(np.linalg.norm(midpoint - accepted_midpoint)) < 24.0
                and abs(candidate["speed_px_s"] - accepted["speed_px_s"]) < speed_tolerance
            ):
                duplicate = True
                break
        if duplicate:
            continue
        tracks.append(candidate)
        if len(tracks) >= 32:
            break
    for track in tracks[:18]:
        x1, y1, x2, y2 = track["line"]
        cv2.line(overlay, (x1, y1), (x2, y2), (20, 40, 255), 1, cv2.LINE_AA)
    for track in tracks:
        track.pop("midpoint", None)
    speeds = np.asarray([track["speed_px_s"] for track in tracks], dtype=np.float64)
    lifetimes = np.asarray([track["lifetime_s"] for track in tracks], dtype=np.float64)
    if len(speeds):
        positive = max(float(np.mean(speeds >= 0)), float(np.mean(speeds < 0)))
        speed_p10 = float(np.percentile(speeds, 10))
        speed_p90 = float(np.percentile(speeds, 90))
        median_absolute_speed = float(np.median(np.abs(speeds)))
        speed_spread_ratio = (speed_p90 - speed_p10) / max(median_absolute_speed, 1e-6)
        speed_reliable = positive >= 0.72 and speed_spread_ratio <= 1.5 and len(tracks) >= 6
        confidence_score = min(1.0, positive * min(1.0, len(tracks) / 18.0))
        confidence_score *= max(0.0, 1.0 - min(1.0, speed_spread_ratio / 2.0))
        result = {
            "raw_candidate_count": raw_candidate_count,
            "filtered_candidate_count": len(candidates),
            "track_count": len(tracks),
            "median_signed_speed_px_s": number(np.median(speeds)),
            "speed_p10_px_s": number(speed_p10),
            "speed_p90_px_s": number(speed_p90),
            "speed_spread_over_median_absolute": number(speed_spread_ratio),
            "speed_reliable": speed_reliable,
            "median_visible_lifetime_s": number(np.median(lifetimes)),
            "direction_agreement": number(positive),
            "confidence": confidence(confidence_score),
        }
    else:
        result = {
            "raw_candidate_count": raw_candidate_count,
            "filtered_candidate_count": len(candidates),
            "track_count": 0,
            "median_signed_speed_px_s": None,
            "speed_p10_px_s": None,
            "speed_p90_px_s": None,
            "speed_spread_over_median_absolute": None,
            "speed_reliable": False,
            "median_visible_lifetime_s": None,
            "direction_agreement": 0.0,
            "confidence": "low",
        }
    return result, overlay


def save_track_overlay(overlay: np.ndarray, path: Path) -> None:
    image = cv2.applyColorMap(overlay[:, :, 0], cv2.COLORMAP_TURBO)
    red = overlay[:, :, 2].astype(np.int16) > overlay[:, :, 0].astype(np.int16) + 40
    image[red] = (20, 30, 255)
    image = cv2.resize(image, (960, max(260, overlay.shape[0] * 3)), interpolation=cv2.INTER_NEAREST)
    cv2.putText(image, "red lines: 18 longest filtered crest candidates", (18, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.62, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.imwrite(str(path), image)


def analyze_transect_bands(
    kymograph: np.ndarray,
    times: np.ndarray,
    line_length_px: float,
) -> list[dict[str, Any]]:
    names = ("start", "middle", "end")
    results = []
    for index, name in enumerate(names):
        first = round(index * kymograph.shape[1] / 3)
        last = round((index + 1) * kymograph.shape[1] / 3)
        band = kymograph[:, first:last]
        spectra, _ = analyze_spectra(band, times, line_length_px / 3.0)
        results.append({
            "name": name,
            "transect_fraction": [number(index / 3.0), number((index + 1) / 3.0)],
            "temporal_peaks": spectra["temporal_peaks"],
            "temporal_confidence": spectra["temporal_confidence"],
            "spatial_peaks": spectra["spatial_peaks"],
            "spatial_confidence": spectra["spatial_confidence"],
        })
    return results


def save_band_kymographs(kymograph: np.ndarray, path: Path) -> None:
    normalized = cv2.normalize(kymograph, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)
    panels = []
    for index, name in enumerate(("TRANSECT START", "MIDDLE", "TRANSECT END")):
        first = round(index * normalized.shape[1] / 3)
        last = round((index + 1) * normalized.shape[1] / 3)
        panel = cv2.applyColorMap(normalized[:, first:last], cv2.COLORMAP_TURBO)
        panel = cv2.resize(panel, (420, max(280, normalized.shape[0] * 3)), interpolation=cv2.INTER_NEAREST)
        cv2.putText(panel, name, (12, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.58, (255, 255, 255), 2, cv2.LINE_AA)
        panels.append(panel)
    cv2.imwrite(str(path), np.concatenate(panels, axis=1))


def analyze_appearance(
    frames: list[np.ndarray],
    roi: tuple[int, int, int, int],
) -> dict[str, Any]:
    x, y, width, height = roi
    samples = []
    frame_luminance = []
    detail = []
    for frame in frames[:: max(1, len(frames) // 48)]:
        crop = frame[y:y + height, x:x + width]
        rgb = cv2.cvtColor(crop, cv2.COLOR_BGR2RGB)
        samples.append(rgb[::5, ::5].reshape(-1, 3))
        gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
        frame_luminance.append(float(np.mean(gray) / 255.0))
        detail.append(float(cv2.Laplacian(gray, cv2.CV_32F).var()))
    pixels = np.concatenate(samples, axis=0).astype(np.float32)
    luminance = (pixels[:, 0] * 0.2126 + pixels[:, 1] * 0.7152 + pixels[:, 2] * 0.0722) / 255.0
    hsv = cv2.cvtColor(pixels.reshape(-1, 1, 3).astype(np.uint8), cv2.COLOR_RGB2HSV).reshape(-1, 3)
    low = pixels[luminance <= np.percentile(luminance, 15)]
    middle = pixels[(luminance >= np.percentile(luminance, 45)) & (luminance <= np.percentile(luminance, 55))]
    high = pixels[luminance >= np.percentile(luminance, 92)]

    def color_entry(values: np.ndarray) -> dict[str, Any]:
        color = np.clip(np.median(values, axis=0), 0, 255).astype(int)
        return {
            "hex": "#{:02X}{:02X}{:02X}".format(*color.tolist()),
            "rgb_0_255": color.tolist(),
        }

    clipped = np.mean(np.max(pixels, axis=1) >= 250)
    low_saturation_highlight = np.mean((hsv[:, 1] < 45) & (hsv[:, 2] > 220))
    exposure_range = np.percentile(frame_luminance, 95) - np.percentile(frame_luminance, 5)
    return {
        "luminance_p05": number(np.percentile(luminance, 5)),
        "luminance_p50": number(np.percentile(luminance, 50)),
        "luminance_p95": number(np.percentile(luminance, 95)),
        "clipped_pixel_fraction": number(clipped),
        "low_saturation_highlight_fraction": number(low_saturation_highlight),
        "median_saturation_0_255": number(np.median(hsv[:, 1])),
        "exposure_drift_fraction": number(exposure_range),
        "median_detail_energy": number(np.median(detail)),
        "trough_color": color_entry(low),
        "mid_color": color_entry(middle),
        "highlight_color": color_entry(high),
    }


def analyze_crest_orientation(
    frames: list[np.ndarray],
    roi: tuple[int, int, int, int],
) -> dict[str, Any]:
    x, y, width, height = roi
    histogram = np.zeros(36, dtype=np.float64)
    vector = 0j
    total_weight = 0.0
    for frame in frames[:: max(1, len(frames) // 36)]:
        gray = cv2.cvtColor(frame[y:y + height, x:x + width], cv2.COLOR_BGR2GRAY)
        gx = cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=3)
        gy = cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=3)
        magnitude = np.hypot(gx, gy)
        threshold = np.percentile(magnitude, 75)
        selected = magnitude >= max(8.0, threshold)
        if not np.any(selected):
            continue
        edge_angle = (np.arctan2(gy[selected], gx[selected]) + np.pi * 0.5) % np.pi
        weights = magnitude[selected]
        bins = np.floor(edge_angle / np.pi * len(histogram)).astype(int) % len(histogram)
        histogram += np.bincount(bins, weights=weights, minlength=len(histogram))
        vector += np.sum(weights * np.exp(2j * edge_angle))
        total_weight += float(np.sum(weights))
    coherence = abs(vector) / max(total_weight, 1e-9)
    angle = (math.degrees(math.atan2(vector.imag, vector.real)) * 0.5) % 180.0
    normalized = histogram / max(float(np.sum(histogram)), 1e-9)
    return {
        "dominant_crest_line_angle_deg": number(angle),
        "orientation_coherence": number(coherence),
        "confidence": confidence(float(coherence)),
        "histogram_0_180": [number(value, 6) for value in normalized],
    }


def save_orientation_plot(data: dict[str, Any], path: Path, title: str) -> None:
    histogram = np.asarray(data["histogram_0_180"], dtype=np.float64)
    width, height = 760, 420
    canvas = np.full((height, width, 3), 20, dtype=np.uint8)
    maximum = max(float(np.max(histogram)), 1e-9)
    bar_width = (width - 70) / len(histogram)
    for index, value in enumerate(histogram):
        x1 = round(45 + index * bar_width)
        x2 = round(45 + (index + 1) * bar_width - 1)
        y = round(height - 48 - value / maximum * (height - 110))
        cv2.rectangle(canvas, (x1, y), (x2, height - 48), (90, 220, 255), -1)
    cv2.putText(canvas, title, (22, 32), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (245, 245, 245), 2, cv2.LINE_AA)
    cv2.putText(canvas, "crest-line angle: 0 to 180 degrees", (22, height - 18), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (190, 190, 190), 1, cv2.LINE_AA)
    cv2.imwrite(str(path), canvas)


def save_series_plot(
    times: np.ndarray,
    series: list[tuple[str, np.ndarray, tuple[int, int, int]]],
    path: Path,
    title: str,
) -> None:
    width, height = 1000, 440
    canvas = np.full((height, width, 3), 20, dtype=np.uint8)
    left, right, top, bottom = 70, width - 30, 65, height - 45
    for index, (name, values, color) in enumerate(series):
        values = np.asarray(values, dtype=np.float64)
        finite = np.isfinite(values)
        if np.count_nonzero(finite) < 2:
            continue
        low, high = np.percentile(values[finite], (3, 97))
        if high <= low:
            high = low + 1.0
        xs = left + (times - times[0]) / max(float(times[-1] - times[0]), 1e-6) * (right - left)
        ys = bottom - np.clip((values - low) / (high - low), 0, 1) * (bottom - top)
        points = np.column_stack([xs[finite], ys[finite]]).astype(int).reshape(-1, 1, 2)
        cv2.polylines(canvas, [points], False, color, 2, cv2.LINE_AA)
        cv2.putText(canvas, name, (left + index * 230, 52), cv2.FONT_HERSHEY_SIMPLEX, 0.46, color, 1, cv2.LINE_AA)
    cv2.putText(canvas, title, (20, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.68, (245, 245, 245), 2, cv2.LINE_AA)
    cv2.line(canvas, (left, bottom), (right, bottom), (120, 120, 120), 1)
    cv2.imwrite(str(path), canvas)


def save_direction_rose(histogram: list[float], path: Path) -> None:
    values = np.asarray(histogram, dtype=np.float64)
    size = 560
    canvas = np.full((size, size, 3), 20, dtype=np.uint8)
    center = np.array([size // 2, size // 2], dtype=np.float64)
    radius = size * 0.4
    maximum = max(float(np.max(values)), 1e-9)
    cv2.circle(canvas, tuple(center.astype(int)), round(radius), (90, 90, 90), 1, cv2.LINE_AA)
    for index, value in enumerate(values):
        angle = 2.0 * np.pi * (index + 0.5) / len(values)
        end = center + np.array([math.cos(angle), math.sin(angle)]) * radius * value / maximum
        cv2.line(canvas, tuple(center.astype(int)), tuple(end.astype(int)), (60, 220, 255), 3, cv2.LINE_AA)
    cv2.putText(canvas, "apparent motion direction", (110, 32), cv2.FONT_HERSHEY_SIMPLEX, 0.62, (245, 245, 245), 2, cv2.LINE_AA)
    cv2.putText(canvas, "+X right", (size - 105, size // 2 - 8), cv2.FONT_HERSHEY_SIMPLEX, 0.42, (190, 190, 190), 1, cv2.LINE_AA)
    cv2.putText(canvas, "+Y down", (size // 2 + 10, size - 20), cv2.FONT_HERSHEY_SIMPLEX, 0.42, (190, 190, 190), 1, cv2.LINE_AA)
    cv2.imwrite(str(path), canvas)


def foam_mask(
    frame: np.ndarray,
    roi: tuple[int, int, int, int],
    saturation_threshold: int,
    value_threshold: int,
) -> np.ndarray:
    x, y, width, height = roi
    hsv = cv2.cvtColor(frame[y:y + height, x:x + width], cv2.COLOR_BGR2HSV)
    mask = ((hsv[:, :, 1] <= saturation_threshold) & (hsv[:, :, 2] >= value_threshold)).astype(np.uint8) * 255
    kernel = np.ones((3, 3), dtype=np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
    return cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel)


def save_foam_mask_pages(
    frames: list[np.ndarray],
    times: np.ndarray,
    roi: tuple[int, int, int, int],
    saturation_threshold: int,
    value_threshold: int,
    output: Path,
    review_frames: int,
) -> list[str]:
    x, y, width, height = roi
    indices = selected_indices(len(frames), review_frames)
    files = []
    pairs_per_page = 12
    for page_index, start in enumerate(range(0, len(indices), pairs_per_page), 1):
        page = indices[start:start + pairs_per_page]
        cell_width, image_height = 300, 190
        sheet = np.full((len(page) * (image_height + 26), cell_width * 2, 3), 16, dtype=np.uint8)
        for row, frame_index in enumerate(page):
            crop = frames[frame_index][y:y + height, x:x + width]
            mask = foam_mask(frames[frame_index], roi, saturation_threshold, value_threshold)
            overlay = crop.copy()
            overlay[mask > 0] = overlay[mask > 0] * 0.35 + np.array([20, 40, 255]) * 0.65
            top = row * (image_height + 26)
            sheet[top:top + image_height, :cell_width] = _resize_cell(crop, cell_width, image_height)
            sheet[top:top + image_height, cell_width:] = _resize_cell(overlay.astype(np.uint8), cell_width, image_height)
            cv2.putText(sheet, f"RAW t={times[frame_index]:.2f}s", (6, top + image_height + 19), cv2.FONT_HERSHEY_SIMPLEX, 0.43, (235, 235, 235), 1, cv2.LINE_AA)
            cv2.putText(sheet, "RED = WHITE-WATER CANDIDATE", (cell_width + 6, top + image_height + 19), cv2.FONT_HERSHEY_SIMPLEX, 0.43, (235, 235, 235), 1, cv2.LINE_AA)
        name = f"foam_mask_review_{page_index:02d}.jpg"
        cv2.imwrite(str(output / name), sheet, [cv2.IMWRITE_JPEG_QUALITY, 92])
        files.append(name)
    return files


def detect_events(times: np.ndarray, values: np.ndarray, limit: int = 6) -> list[dict[str, Any]]:
    values = np.asarray(values, dtype=np.float64)
    if len(values) < 5:
        return []
    smooth = np.convolve(values, np.ones(5) / 5.0, mode="same")
    threshold = float(np.percentile(smooth, 65))
    candidates = [
        index for index in range(2, len(smooth) - 2)
        if smooth[index] >= threshold and smooth[index] >= smooth[index - 1] and smooth[index] > smooth[index + 1]
    ]
    minimum_gap = max(1, round(0.7 / max(float(np.median(np.diff(times))), 1e-6)))
    chosen: list[int] = []
    for index in sorted(candidates, key=lambda item: smooth[item], reverse=True):
        if all(abs(index - previous) >= minimum_gap for previous in chosen):
            chosen.append(index)
        if len(chosen) == limit:
            break
    chosen.sort()
    return [
        {
            "sample_index": int(index),
            "time_s": number(times[index]),
            "value": number(values[index]),
        }
        for index in chosen
    ]


def save_event_sheet(
    frames: list[np.ndarray],
    times: np.ndarray,
    roi: tuple[int, int, int, int],
    events: list[dict[str, Any]],
    path: Path,
) -> None:
    if not events:
        return
    x, y, width, height = roi
    offsets = (-4, -2, 0, 2, 4)
    cell_width, image_height = 250, 170
    sheet = np.full((len(events) * (image_height + 28), len(offsets) * cell_width, 3), 16, dtype=np.uint8)
    for row, event in enumerate(events):
        for column, offset in enumerate(offsets):
            index = int(np.clip(event["sample_index"] + offset, 0, len(frames) - 1))
            crop = frames[index][y:y + height, x:x + width]
            _write_cell(sheet, crop, f"t={times[index]:.2f}s", row * len(offsets) + column, len(offsets), cell_width, image_height)
    cv2.imwrite(str(path), sheet, [cv2.IMWRITE_JPEG_QUALITY, 92])
