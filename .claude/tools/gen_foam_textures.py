"""Generate seamless grayscale foam masks for the water shader.

Everything here is periodic (Perlin lattice wraps at the octave frequency, Worley uses
minimum-image distance on a torus), so every output tiles without a seam at any tiling rate.
Output: 1024x1024 8-bit PNG + a hand-authored .meta so Unity imports them as linear
SingleChannel (BC4) masks instead of sRGB color.

Run:  python .claude/tools/gen_foam_textures.py [--out <dir>] [--sheet <preview.png>]
"""

from __future__ import annotations

import argparse
import hashlib
import pathlib

import numpy as np
from PIL import Image, ImageDraw

SIZE = 1024


# ---------------------------------------------------------------- noise basis


def _fade(t: np.ndarray) -> np.ndarray:
    return t * t * t * (t * (t * 6 - 15) + 10)


def perlin(rng: np.random.Generator, size: int, fx: int, fy: int | None = None) -> np.ndarray:
    """Periodic Perlin noise in 0..1. Lattice is fx x fy cells, so it wraps exactly."""
    fy = fx if fy is None else fy
    ang = rng.uniform(0.0, 2.0 * np.pi, (fy, fx))
    gx, gy = np.cos(ang), np.sin(ang)

    def axis(freq: int):
        c = np.arange(size, dtype=np.float64) / size * freq
        i0 = np.floor(c).astype(np.int64)
        f = c - i0
        return i0 % freq, (i0 + 1) % freq, f, _fade(f)

    x0, x1, fxr, ux = axis(fx)
    y0, y1, fyr, uy = axis(fy)
    X0, X1, FX, UX = x0[None, :], x1[None, :], fxr[None, :], ux[None, :]
    Y0, Y1, FY, UY = y0[:, None], y1[:, None], fyr[:, None], uy[:, None]

    def dot(iy, ix, dx, dy):
        return gx[iy, ix] * dx + gy[iy, ix] * dy

    n00 = dot(Y0, X0, FX, FY)
    n10 = dot(Y0, X1, FX - 1.0, FY)
    n01 = dot(Y1, X0, FX, FY - 1.0)
    n11 = dot(Y1, X1, FX - 1.0, FY - 1.0)
    nx0 = n00 + UX * (n10 - n00)
    nx1 = n01 + UX * (n11 - n01)
    return np.clip((nx0 + UY * (nx1 - nx0)) * 0.72 + 0.5, 0.0, 1.0)


def fbm(rng, size, fx, octaves, fy=None, gain=0.5, lacunarity=2, ridged=False) -> np.ndarray:
    """fBm with separate X/Y frequency (fx != fy stretches the features).

    Octaves stop once a lattice cell drops below 4 px: past that Perlin aliases into
    uniform pepper instead of adding detail, which is what wrecks a foam mask.
    """
    fy = fx if fy is None else fy
    total = np.zeros((size, size))
    amp, norm = 1.0, 0.0
    for _ in range(octaves):
        if max(fx, fy) * 4 > size:
            break
        n = perlin(rng, size, int(fx), int(fy))
        if ridged:
            n = 1.0 - np.abs(n * 2.0 - 1.0)
        total += amp * n
        norm += amp
        amp *= gain
        fx *= lacunarity
        fy *= lacunarity
    return total / max(norm, 1e-6)


def worley(rng: np.random.Generator, size: int, cells: int):
    """Periodic jittered-grid Worley. Returns (F1, F2) in cell-relative units."""
    px = (np.arange(cells)[None, :] + rng.random((cells, cells))) / cells
    py = (np.arange(cells)[:, None] + rng.random((cells, cells))) / cells
    u = (np.arange(size) + 0.5) / size
    X, Y = u[None, :], u[:, None]
    ci = np.floor(X * cells).astype(np.int64)
    cj = np.floor(Y * cells).astype(np.int64)

    f1 = np.full((size, size), 9.0)
    f2 = np.full((size, size), 9.0)
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            ii = (ci + ox) % cells
            jj = (cj + oy) % cells
            dx = X - px[jj, ii]
            dy = Y - py[jj, ii]
            # minimum image convention: the torus, not the plane
            dx -= np.round(dx)
            dy -= np.round(dy)
            d = np.sqrt(dx * dx + dy * dy) * cells
            f2 = np.minimum(f2, np.maximum(f1, d))
            f1 = np.minimum(f1, d)
    return f1, f2


def sample_wrap(img: np.ndarray, X: np.ndarray, Y: np.ndarray) -> np.ndarray:
    """Bilinear sample with wrap - used for domain warping without breaking tiling."""
    size = img.shape[0]
    x0 = np.floor(X).astype(np.int64)
    y0 = np.floor(Y).astype(np.int64)
    fx = X - x0
    fy = Y - y0
    x0 %= size
    y0 %= size
    x1 = (x0 + 1) % size
    y1 = (y0 + 1) % size
    return (
        img[y0, x0] * (1 - fx) * (1 - fy)
        + img[y0, x1] * fx * (1 - fy)
        + img[y1, x0] * (1 - fx) * fy
        + img[y1, x1] * fx * fy
    )


def warp(rng, img, amount, freq=3, octaves=3):
    size = img.shape[0]
    wx = fbm(rng, size, freq, octaves) - 0.5
    wy = fbm(rng, size, freq, octaves) - 0.5
    gx, gy = np.meshgrid(np.arange(size, dtype=np.float64), np.arange(size, dtype=np.float64))
    return sample_wrap(img, gx + wx * amount, gy + wy * amount)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def stretch(rng, size, fx, fy, octaves, **kw):
    """Anisotropic fBm: features run along the axis with the LOWER frequency."""
    return fbm(rng, size, fx, octaves, fy=fy, **kw)


def normalize(img, lo=0.5, hi=99.5):
    a, b = np.percentile(img, lo), np.percentile(img, hi)
    if b - a < 1e-6:
        return np.clip(img, 0.0, 1.0)
    return np.clip((img - a) / (b - a), 0.0, 1.0)


# ---------------------------------------------------------------- the six maps


def ocean_clumps(size=SIZE) -> np.ndarray:
    """Realistic ocean whitecap foam: torn patches, bubble grain, dark drained pores."""
    rng = np.random.default_rng(20260807)
    base = warp(rng, fbm(rng, size, 3, 5), amount=size * 0.06, freq=3)
    base = normalize(base, 1.0, 99.0)
    coverage = smoothstep(0.30, 0.60, base)
    # ragged rim: the edge dissolves into clumps instead of fading out smoothly
    rim = np.clip(smoothstep(0.16, 0.42, base) - coverage, 0.0, 1.0)
    coverage = np.clip(coverage + rim * smoothstep(0.40, 0.72, fbm(rng, size, 16, 3)), 0.0, 1.0)

    # Two pore scales only. A third, finer one reads as sensor pepper, not as foam.
    # Both are gated by their own low-frequency mask, otherwise the whole sheet turns
    # into an even honeycomb and the patch silhouette disappears under it.
    f1, _ = worley(rng, size, 34)
    pores = smoothstep(0.80, 0.20, f1) * smoothstep(0.34, 0.70, fbm(rng, size, 6, 3))
    f1b, _ = worley(rng, size, 70)
    micro = smoothstep(0.62, 0.22, f1b) * smoothstep(0.40, 0.75, fbm(rng, size, 9, 3))
    tears = smoothstep(0.55, 0.95, fbm(rng, size, 7, 4, ridged=True))
    grain = 0.66 + 0.34 * fbm(rng, size, 14, 3)

    foam = coverage * grain * (1.0 - 0.85 * pores) * (1.0 - 0.45 * micro) * (1.0 - 0.55 * tears)
    foam += 0.22 * smoothstep(0.58, 0.92, base)  # thick crest cores read brightest
    return normalize(foam, 0.5, 99.5)


def bubbles(size=SIZE) -> np.ndarray:
    """Fine bubble/detail mask: mid-grey churn with drained dark bubbles of mixed size."""
    rng = np.random.default_rng(70712026)
    churn = 0.5 + 0.62 * (fbm(rng, size, 10, 4) - 0.5)
    fibre = stretch(rng, size, 20, 64, 3)  # faint fibrous streaking through the churn
    churn = churn * 0.80 + fibre * 0.24

    # A fixed radius gives an evenly spaced polka-dot grid. Driving the cutoff with
    # low-frequency noise makes bubbles vary in size and clump, which is what real
    # foam does - dense rafts next to nearly drained water.
    f1, _ = worley(rng, size, 46)
    t_big = 0.10 + 0.42 * fbm(rng, size, 5, 3)
    big = smoothstep(t_big + 0.10, t_big - 0.04, f1)
    f1b, _ = worley(rng, size, 96)
    t_small = 0.08 + 0.34 * fbm(rng, size, 8, 3)
    small = smoothstep(t_small + 0.08, t_small - 0.03, f1b)

    out = churn * (1.0 - 0.78 * big) * (1.0 - 0.45 * small)
    out += 0.14 * smoothstep(0.70, 0.95, fbm(rng, size, 22, 3))  # bright bubble caps
    return normalize(out, 1.0, 99.0)


def toon_cells(size=SIZE) -> np.ndarray:
    """Stylised foam: white cell borders on black, wobbled so they read hand-drawn."""
    rng = np.random.default_rng(31415)
    f1, f2 = worley(rng, size, 11)
    edge = f2 - f1
    width = 0.10 + 0.09 * fbm(rng, size, 6, 3)  # borders thicken and thin along their run
    lines = 1.0 - smoothstep(0.0, 1.0, edge / width)
    lines = warp(rng, lines, amount=size * 0.020, freq=6, octaves=4)
    # break the borders so they are not a perfect net
    lines *= smoothstep(0.22, 0.55, fbm(rng, size, 14, 3))
    lines = smoothstep(0.10, 0.45, lines)
    blobs = smoothstep(0.86, 0.94, warp(rng, fbm(rng, size, 26, 3), size * 0.01, 8))
    return np.clip(lines + blobs * 0.9, 0.0, 1.0)


def toon_blobs(size=SIZE) -> np.ndarray:
    """Stylised foam: thick outlined organic blobs (contour of a warped field)."""
    rng = np.random.default_rng(2718281)
    field = warp(rng, fbm(rng, size, 3, 3), amount=size * 0.09, freq=3, octaves=3)
    field = normalize(field, 1.0, 99.0)
    thickness = 0.055 + 0.030 * fbm(rng, size, 5, 2)  # the line breathes along its run
    outline = 1.0 - smoothstep(0.55, 1.0, np.abs(field - 0.50) / thickness)

    field2 = warp(rng, fbm(rng, size, 5, 3), amount=size * 0.06, freq=4, octaves=3)
    field2 = normalize(field2, 1.0, 99.0)
    outline2 = 1.0 - smoothstep(0.55, 1.0, np.abs(field2 - 0.56) / 0.045)

    droplets = smoothstep(0.88, 0.96, warp(rng, fbm(rng, size, 12, 2), size * 0.02, 6))
    return np.clip(outline + outline2 * 0.9 + droplets, 0.0, 1.0)


def stream_streaks(size=SIZE) -> np.ndarray:
    """River/wake foam: filaments stretched along +X, with slow cross-flow tearing."""
    rng = np.random.default_rng(1123581)
    fine = stretch(rng, size, 6, 110, 2, ridged=True)
    mid = stretch(rng, size, 4, 46, 2, ridged=True)
    coarse = stretch(rng, size, 3, 16, 2)

    streaks = fine * 0.40 + mid * 0.42 + coarse * 0.34
    # shear the filaments so they are not a perfectly parallel comb
    gx, gy = np.meshgrid(np.arange(size, dtype=np.float64), np.arange(size, dtype=np.float64))
    shear = (fbm(rng, size, 2, 2, fy=4) - 0.5) * size
    streaks = sample_wrap(streaks, gx, gy + shear * 0.05)

    coverage = smoothstep(0.28, 0.78, stretch(rng, size, 2, 9, 2))
    out = streaks * (0.40 + 0.60 * coverage)
    return normalize(out, 1.0, 99.0)


def waterfall_bands(size=SIZE) -> np.ndarray:
    """Cascade foam: near-vertical aerated ribbons that shear apart as they fall."""
    rng = np.random.default_rng(6180339)
    ribbons = stretch(rng, size, 40, 4, 2, ridged=True)  # long in Y, tight in X
    fine = stretch(rng, size, 110, 6, 2, ridged=True)
    field = ribbons * 0.60 + fine * 0.42

    gx, gy = np.meshgrid(np.arange(size, dtype=np.float64), np.arange(size, dtype=np.float64))
    sway = (fbm(rng, size, 3, 2, fy=2) - 0.5) * size
    field = sample_wrap(field, gx + sway * 0.05, gy)

    # horizontal tears where the sheet detaches into spray
    breaks = smoothstep(0.30, 0.70, stretch(rng, size, 3, 10, 2))
    spray = smoothstep(0.68, 0.95, fbm(rng, size, 24, 3))
    out = field * (0.42 + 0.58 * breaks) + spray * 0.20
    return normalize(out, 1.0, 99.0)


TEXTURES = [
    ("T_WaterFoam_OceanClumps", ocean_clumps, "Realistic ocean whitecap patches"),
    ("T_WaterFoam_Bubbles", bubbles, "Fine bubble / breakup detail"),
    ("T_WaterFoam_ToonCells", toon_cells, "Toon cell-border foam"),
    ("T_WaterFoam_ToonBlobs", toon_blobs, "Toon outlined blob foam"),
    ("T_WaterFoam_StreamStreaks", stream_streaks, "Stream / wake filaments (+X flow)"),
    ("T_WaterFoam_WaterfallBands", waterfall_bands, "Waterfall ribbons (+Y flow)"),
]


# ---------------------------------------------------------------- Unity import


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 10
  textureShape: 1
  singleChannelComponent: 1
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def stable_guid(name: str) -> str:
    return hashlib.md5(("MarketGame/WaterFoam/" + name).encode("utf-8")).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--out",
        default="Assets/_Project/Art/Textures/Water/Foam",
        help="output directory for the PNGs (relative to the project root)",
    )
    parser.add_argument("--sheet", default="", help="optional contact-sheet preview path")
    parser.add_argument("--size", type=int, default=SIZE)
    args = parser.parse_args()

    out_dir = pathlib.Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    previews = []
    for name, fn, label in TEXTURES:
        data = fn(args.size)
        img = Image.fromarray((np.clip(data, 0, 1) * 255.0 + 0.5).astype(np.uint8), mode="L")
        png = out_dir / f"{name}.png"
        img.save(png, optimize=True)
        (out_dir / f"{name}.png.meta").write_text(META.format(guid=stable_guid(name)), encoding="utf-8")
        print(f"{png}  ({png.stat().st_size // 1024} KB)  {label}")
        previews.append((img, label))

    if args.sheet:
        cell = 460
        pad, header = 14, 26
        sheet = Image.new("RGB", (cell * 3 + pad * 4, (cell + header) * 2 + pad * 3), (24, 26, 30))
        draw = ImageDraw.Draw(sheet)
        for i, (img, label) in enumerate(previews):
            cx = pad + (i % 3) * (cell + pad)
            cy = pad + (i // 3) * (cell + header + pad)
            sheet.paste(img.resize((cell, cell), Image.LANCZOS).convert("RGB"), (cx, cy))
            draw.text((cx + 2, cy + cell + 6), label, fill=(226, 232, 240))
        sheet.save(args.sheet)
        print(f"sheet -> {args.sheet}")


if __name__ == "__main__":
    main()
