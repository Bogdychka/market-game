using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Market.DebugTools.Editor
{
    /// <summary>
    /// Turns captured frames into files an agent can actually consume: one labelled contact sheet
    /// (many viewpoints or many parameter values in a single image) and per-shot diff heatmaps.
    /// Labels are burned into the pixels with a built-in 5x7 font, because a grid of unlabelled
    /// cells is guesswork - the value under each cell has to be visible in the image itself.
    /// </summary>
    public static class ShaderVisionSheet
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;

        // Classic 5x7 bitmap font: 5 columns per glyph, bit 0 = top row.
        private static readonly Dictionary<char, byte[]> Font = BuildFont();

        /// <summary>Writes a PNG from bottom-up Color32 rows.</summary>
        public static void WritePng(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Composes frames into a labelled grid, reading order left-to-right and top-to-bottom.
        /// All frames must share the capture size.
        /// </summary>
        public static void WriteContactSheet(string path, IReadOnlyList<ShaderVisionFrame> frames, int columns)
        {
            if (frames == null || frames.Count == 0)
                return;

            columns = Mathf.Clamp(columns, 1, frames.Count);
            int rows = Mathf.CeilToInt(frames.Count / (float)columns);
            int cellWidth = frames[0].Width;
            int cellHeight = frames[0].Height;
            int sheetWidth = cellWidth * columns;
            int sheetHeight = cellHeight * rows;

            var sheet = new Color32[sheetWidth * sheetHeight];
            var background = new Color32(18, 18, 20, 255);
            for (int i = 0; i < sheet.Length; i++)
                sheet[i] = background;

            for (int index = 0; index < frames.Count; index++)
            {
                ShaderVisionFrame frame = frames[index];
                if (frame.Width != cellWidth || frame.Height != cellHeight)
                    continue;

                int column = index % columns;
                int row = index / columns;
                int originX = column * cellWidth;
                // Texture rows run bottom-up, so row 0 of the grid is the TOP band of the sheet.
                int originY = sheetHeight - (row + 1) * cellHeight;

                for (int y = 0; y < cellHeight; y++)
                {
                    Array.Copy(
                        frame.Pixels,
                        y * cellWidth,
                        sheet,
                        (originY + y) * sheetWidth + originX,
                        cellWidth);
                }

                int scale = cellWidth >= 480 ? 2 : 1;
                DrawLabel(sheet, sheetWidth, sheetHeight, originX + 4, originY + 4, frame.Label, scale);
                DrawCellBorder(sheet, sheetWidth, sheetHeight, originX, originY, cellWidth, cellHeight);
            }

            WritePng(path, sheetWidth, sheetHeight, sheet);
        }

        /// <summary>
        /// Per-pixel comparison against a baseline PNG. Returns false when the baseline is missing
        /// or a different size - a diff between mismatched captures would be noise, not a signal.
        /// </summary>
        public static bool WriteDiff(
            string baselinePath,
            ShaderVisionFrame frame,
            string diffPath,
            ShaderVisionShotReport report)
        {
            if (!File.Exists(baselinePath))
                return false;

            var baseline = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                if (!baseline.LoadImage(File.ReadAllBytes(baselinePath), false))
                    return false;
                if (baseline.width != frame.Width || baseline.height != frame.Height)
                    return false;

                Color32[] previous = baseline.GetPixels32();
                var heatmap = new Color32[previous.Length];
                double sum = 0d;
                int maxDelta = 0;
                int changed = 0;

                for (int i = 0; i < previous.Length; i++)
                {
                    Color32 a = previous[i];
                    Color32 b = frame.Pixels[i];
                    int delta = Mathf.Max(
                        Mathf.Abs(a.r - b.r),
                        Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

                    sum += delta;
                    if (delta > maxDelta) maxDelta = delta;
                    if (delta > 2) changed++;

                    // Amplify x4 so a subtle-but-real change is still legible as a shape.
                    byte hot = (byte)Mathf.Min(255, delta * 4);
                    heatmap[i] = new Color32(hot, (byte)(hot / 3), (byte)(hot / 8), 255);
                }

                report.compared = true;
                report.meanAbsDiff = (float)(sum / Mathf.Max(1, previous.Length) / 255d);
                report.maxAbsDiff = maxDelta / 255f;
                report.changedPct = 100f * changed / Mathf.Max(1, previous.Length);

                WritePng(diffPath, frame.Width, frame.Height, heatmap);
                report.diffFile = Path.GetFileName(diffPath);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseline);
            }
        }

        private static void DrawCellBorder(Color32[] target, int width, int height, int x, int y, int cellWidth, int cellHeight)
        {
            var line = new Color32(90, 90, 96, 255);
            for (int i = 0; i < cellWidth; i++)
            {
                SetPixel(target, width, height, x + i, y, line);
                SetPixel(target, width, height, x + i, y + cellHeight - 1, line);
            }

            for (int i = 0; i < cellHeight; i++)
            {
                SetPixel(target, width, height, x, y + i, line);
                SetPixel(target, width, height, x + cellWidth - 1, y + i, line);
            }
        }

        private static void DrawLabel(Color32[] target, int width, int height, int x, int y, string text, int scale)
        {
            if (string.IsNullOrEmpty(text))
                return;

            text = text.ToUpperInvariant();
            int padding = 2 * scale;
            int barWidth = text.Length * (GlyphWidth + 1) * scale + padding * 2;
            int barHeight = GlyphHeight * scale + padding * 2;

            var backdrop = new Color32(0, 0, 0, 255);
            for (int by = 0; by < barHeight; by++)
            {
                for (int bx = 0; bx < barWidth; bx++)
                    SetPixel(target, width, height, x + bx, y + by, backdrop);
            }

            var ink = new Color32(255, 240, 140, 255);
            int cursor = x + padding;
            foreach (char c in text)
            {
                if (!Font.TryGetValue(c, out byte[] glyph))
                {
                    cursor += (GlyphWidth + 1) * scale;
                    continue;
                }

                for (int column = 0; column < GlyphWidth; column++)
                {
                    byte bits = glyph[column];
                    for (int row = 0; row < GlyphHeight; row++)
                    {
                        if ((bits & (1 << row)) == 0)
                            continue;

                        // Glyph row 0 is the top row; texture y grows upwards.
                        int px = cursor + column * scale;
                        int py = y + padding + (GlyphHeight - 1 - row) * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            for (int sx = 0; sx < scale; sx++)
                                SetPixel(target, width, height, px + sx, py + sy, ink);
                        }
                    }
                }

                cursor += (GlyphWidth + 1) * scale;
            }
        }

        private static void SetPixel(Color32[] target, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            target[y * width + x] = color;
        }

        private static Dictionary<char, byte[]> BuildFont()
        {
            var font = new Dictionary<char, byte[]>();
            void Add(char c, int a, int b, int d, int e, int f) => font[c] = new[] { (byte)a, (byte)b, (byte)d, (byte)e, (byte)f };

            Add(' ', 0x00, 0x00, 0x00, 0x00, 0x00);
            Add('-', 0x08, 0x08, 0x08, 0x08, 0x08);
            Add('_', 0x40, 0x40, 0x40, 0x40, 0x40);
            Add('.', 0x00, 0x60, 0x60, 0x00, 0x00);
            Add(',', 0x00, 0x50, 0x30, 0x00, 0x00);
            Add('/', 0x20, 0x10, 0x08, 0x04, 0x02);
            Add(':', 0x00, 0x36, 0x36, 0x00, 0x00);
            Add('=', 0x14, 0x14, 0x14, 0x14, 0x14);
            Add('+', 0x08, 0x08, 0x3E, 0x08, 0x08);
            Add('#', 0x14, 0x7F, 0x14, 0x7F, 0x14);
            Add('%', 0x23, 0x13, 0x08, 0x64, 0x62);
            Add('(', 0x00, 0x1C, 0x22, 0x41, 0x00);
            Add(')', 0x00, 0x41, 0x22, 0x1C, 0x00);
            Add('|', 0x00, 0x00, 0x7F, 0x00, 0x00);
            Add('0', 0x3E, 0x51, 0x49, 0x45, 0x3E);
            Add('1', 0x00, 0x42, 0x7F, 0x40, 0x00);
            Add('2', 0x42, 0x61, 0x51, 0x49, 0x46);
            Add('3', 0x21, 0x41, 0x45, 0x4B, 0x31);
            Add('4', 0x18, 0x14, 0x12, 0x7F, 0x10);
            Add('5', 0x27, 0x45, 0x45, 0x45, 0x39);
            Add('6', 0x3C, 0x4A, 0x49, 0x49, 0x30);
            Add('7', 0x01, 0x71, 0x09, 0x05, 0x03);
            Add('8', 0x36, 0x49, 0x49, 0x49, 0x36);
            Add('9', 0x06, 0x49, 0x49, 0x29, 0x1E);
            Add('A', 0x7E, 0x11, 0x11, 0x11, 0x7E);
            Add('B', 0x7F, 0x49, 0x49, 0x49, 0x36);
            Add('C', 0x3E, 0x41, 0x41, 0x41, 0x22);
            Add('D', 0x7F, 0x41, 0x41, 0x22, 0x1C);
            Add('E', 0x7F, 0x49, 0x49, 0x49, 0x41);
            Add('F', 0x7F, 0x09, 0x09, 0x09, 0x01);
            Add('G', 0x3E, 0x41, 0x49, 0x49, 0x7A);
            Add('H', 0x7F, 0x08, 0x08, 0x08, 0x7F);
            Add('I', 0x00, 0x41, 0x7F, 0x41, 0x00);
            Add('J', 0x20, 0x40, 0x41, 0x3F, 0x01);
            Add('K', 0x7F, 0x08, 0x14, 0x22, 0x41);
            Add('L', 0x7F, 0x40, 0x40, 0x40, 0x40);
            Add('M', 0x7F, 0x02, 0x0C, 0x02, 0x7F);
            Add('N', 0x7F, 0x04, 0x08, 0x10, 0x7F);
            Add('O', 0x3E, 0x41, 0x41, 0x41, 0x3E);
            Add('P', 0x7F, 0x09, 0x09, 0x09, 0x06);
            Add('Q', 0x3E, 0x41, 0x51, 0x21, 0x5E);
            Add('R', 0x7F, 0x09, 0x19, 0x29, 0x46);
            Add('S', 0x46, 0x49, 0x49, 0x49, 0x31);
            Add('T', 0x01, 0x01, 0x7F, 0x01, 0x01);
            Add('U', 0x3F, 0x40, 0x40, 0x40, 0x3F);
            Add('V', 0x1F, 0x20, 0x40, 0x20, 0x1F);
            Add('W', 0x3F, 0x40, 0x38, 0x40, 0x3F);
            Add('X', 0x63, 0x14, 0x08, 0x14, 0x63);
            Add('Y', 0x07, 0x08, 0x70, 0x08, 0x07);
            Add('Z', 0x61, 0x51, 0x49, 0x45, 0x43);
            return font;
        }
    }
}
