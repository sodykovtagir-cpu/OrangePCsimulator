using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PC.Component.Software
{
    /// <summary>Bounded GIF87a/GIF89a decoder for in-game .gif files.</summary>
    internal static class GifFrames
    {
        public static bool TryDecode(byte[] bytes, out Texture2D[] frames, out float[] delays)
        {
            frames = null;
            delays = null;
            if (bytes == null || bytes.Length < 13 || bytes.Length > 24000000) return false;
            var pictures = new List<Texture2D>();
            var timings = new List<float>();
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (var input = new BinaryReader(stream))
                {
                    string header = System.Text.Encoding.ASCII.GetString(input.ReadBytes(6));
                    if (header != "GIF87a" && header != "GIF89a") return false;
                    int width = input.ReadUInt16(), height = input.ReadUInt16();
                    if (width < 1 || height < 1 || width > 2048 || height > 2048 ||
                        (long)width * height > 4000000) return false;
                    byte screen = input.ReadByte();
                    input.ReadByte(); // background index
                    input.ReadByte(); // pixel aspect ratio
                    Color32[] palette = (screen & 0x80) != 0 ? ReadPalette(input, 2 << (screen & 7)) : null;
                    var canvas = new Color32[width * height];
                    int disposal = 0, transparent = -1;
                    float delay = .1f;
                    while (stream.Position < stream.Length && pictures.Count < 96 &&
                           (long)(pictures.Count + 1) * width * height <= 12000000)
                    {
                        int kind = input.ReadByte();
                        if (kind == 0x3B) break;
                        if (kind == 0x21)
                        {
                            int label = input.ReadByte();
                            if (label == 0xF9)
                            {
                                if (input.ReadByte() != 4) throw new InvalidDataException();
                                int packed = input.ReadByte();
                                disposal = (packed >> 2) & 7;
                                delay = Mathf.Clamp(input.ReadUInt16() / 100f, .04f, 10f);
                                transparent = input.ReadByte();
                                if ((packed & 1) == 0) transparent = -1;
                                if (input.ReadByte() != 0) throw new InvalidDataException();
                            }
                            else ReadBlocks(input, stream);
                            continue;
                        }
                        if (kind != 0x2C) throw new InvalidDataException();
                        int left = input.ReadUInt16(), top = input.ReadUInt16();
                        int w = input.ReadUInt16(), h = input.ReadUInt16();
                        if (w < 1 || h < 1 || left + w > width || top + h > height) throw new InvalidDataException();
                        int flags = input.ReadByte();
                        bool interlaced = (flags & 0x40) != 0;
                        Color32[] colors = (flags & 0x80) != 0 ? ReadPalette(input, 2 << (flags & 7)) : palette;
                        if (colors == null) throw new InvalidDataException();
                        int minSize = input.ReadByte();
                        byte[] compressed = ReadBlocks(input, stream);
                        byte[] pixels = DecodeLzw(compressed, minSize, w * h);
                        var previous = disposal == 3 ? (Color32[])canvas.Clone() : null;
                        int offset = 0;
                        if (interlaced)
                        {
                            int[] starts = { 0, 4, 2, 1 }, steps = { 8, 8, 4, 2 };
                            for (int pass = 0; pass < 4; pass++)
                                for (int y = starts[pass]; y < h; y += steps[pass])
                                    DrawRow(canvas, width, left, top + y, w, pixels, ref offset, colors, transparent);
                        }
                        else
                            for (int y = 0; y < h; y++)
                                DrawRow(canvas, width, left, top + y, w, pixels, ref offset, colors, transparent);
                        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        texture.filterMode = FilterMode.Bilinear;
                        var bottomUp = new Color32[canvas.Length];
                        for (int y = 0; y < height; y++)
                            Array.Copy(canvas, y * width, bottomUp, (height - y - 1) * width, width);
                        texture.SetPixels32(bottomUp);
                        texture.Apply();
                        pictures.Add(texture);
                        timings.Add(delay);
                        if (disposal == 2)
                            for (int y = top; y < top + h; y++)
                                Array.Clear(canvas, y * width + left, w);
                        else if (previous != null) canvas = previous;
                        disposal = 0; transparent = -1; delay = .1f;
                    }
                }
                if (pictures.Count == 0) return false;
                frames = pictures.ToArray(); delays = timings.ToArray();
                return true;
            }
            catch
            {
                foreach (var picture in pictures) if (picture != null) UnityEngine.Object.Destroy(picture);
                return false;
            }
        }

        private static Color32[] ReadPalette(BinaryReader input, int length)
        {
            var result = new Color32[length];
            for (int i = 0; i < length; i++)
                result[i] = new Color32(input.ReadByte(), input.ReadByte(), input.ReadByte(), 255);
            return result;
        }

        private static byte[] ReadBlocks(BinaryReader input, Stream stream)
        {
            using (var result = new MemoryStream())
            {
                while (stream.Position < stream.Length)
                {
                    int count = input.ReadByte();
                    if (count == 0) return result.ToArray();
                    if (result.Length + count > 24000000) throw new InvalidDataException();
                    byte[] chunk = input.ReadBytes(count);
                    if (chunk.Length != count) throw new EndOfStreamException();
                    result.Write(chunk, 0, count);
                }
                throw new EndOfStreamException();
            }
        }

        private static void DrawRow(Color32[] canvas, int width, int left, int y, int count,
            byte[] pixels, ref int offset, Color32[] palette, int transparent)
        {
            for (int x = 0; x < count; x++)
            {
                if (offset >= pixels.Length) throw new InvalidDataException();
                int index = pixels[offset++];
                if (index >= palette.Length) throw new InvalidDataException();
                if (index != transparent) canvas[y * width + left + x] = palette[index];
            }
        }

        private static byte[] DecodeLzw(byte[] packed, int minSize, int expected)
        {
            if (minSize < 2 || minSize > 8) throw new InvalidDataException();
            int clear = 1 << minSize, end = clear + 1, next = end + 1;
            int size = minSize + 1, bits = 0, buffer = 0, cursor = 0;
            var dictionary = new byte[4096][];
            for (int i = 0; i < clear; i++) dictionary[i] = new[] { (byte)i };
            byte[] previous = null;
            var output = new byte[expected];
            int written = 0;
            while (written < expected)
            {
                while (bits < size)
                {
                    if (cursor >= packed.Length) throw new EndOfStreamException();
                    buffer |= packed[cursor++] << bits;
                    bits += 8;
                }
                int code = buffer & ((1 << size) - 1);
                buffer >>= size; bits -= size;
                if (code == clear) { next = end + 1; size = minSize + 1; previous = null; continue; }
                if (code == end) break;
                byte[] entry;
                if (code < next && dictionary[code] != null) entry = dictionary[code];
                else if (code == next && previous != null)
                {
                    entry = new byte[previous.Length + 1];
                    Array.Copy(previous, entry, previous.Length);
                    entry[previous.Length] = previous[0];
                }
                else throw new InvalidDataException();
                if (written + entry.Length > expected) throw new InvalidDataException();
                Array.Copy(entry, 0, output, written, entry.Length); written += entry.Length;
                if (previous != null && next < 4096)
                {
                    byte[] added = new byte[previous.Length + 1];
                    Array.Copy(previous, added, previous.Length);
                    added[previous.Length] = entry[0];
                    dictionary[next++] = added;
                    if (next == 1 << size && size < 12) size++;
                }
                previous = entry;
            }
            if (written != expected) throw new InvalidDataException();
            return output;
        }
    }
}
