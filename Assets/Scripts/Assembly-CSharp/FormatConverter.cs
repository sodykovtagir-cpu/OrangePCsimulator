using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class FormatConverter
{
	public class Mov
	{
		public Texture2D[] texs;

		public float interval;

		public Mov() {}

		public Mov(Texture2D[] texs, float interval)
        {
			this.texs = texs;
			this.interval = interval;
        }
	}

	public static string MovToString(Mov mov)
	{
		using (var stream = new MemoryStream())
		using (var writer = new BinaryWriter(stream))
		{
			writer.Write(mov.interval);

			var texs = mov.texs;
			var count = texs != null ? texs.Length : 0;
			writer.Write(count);

			for (int i = 0; i < count; i++)
			{
				var bytes = ImageConversion.EncodeToPNG(texs[i]);
				writer.Write(bytes.Length);
				writer.Write(bytes);
			}

			writer.Flush();
			return Convert.ToBase64String(stream.ToArray());
		}
	}

	public static Mov StringToMov(string data, bool markNonReadable)
	{
		var bytes = Convert.FromBase64String(data);
		using (var ms = new MemoryStream(bytes))
		using (var br = new BinaryReader(ms))
		{
			var mov = new Mov();
			mov.interval = br.ReadSingle();
			int count = br.ReadInt32();
			mov.texs = new Texture2D[count];
			for (int i = 0; i < count; i++)
			{
				int len = br.ReadInt32();
				var frame = br.ReadBytes(len);
				mov.texs[i] = BytesToTexture(frame, markNonReadable);
			}
			return mov;
		}
	}

	public static Texture2D BytesToTexture(byte[] data, bool markNonReadable)
	{
		var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
		tex.filterMode = FilterMode.Point;
		ImageConversion.LoadImage(tex, data, markNonReadable);
		return tex;
	}

	public static Texture2D StringToTexture(string data)
	{
		var bytes = Convert.FromBase64String(data);
		return BytesToTexture(bytes, false);
	}

	public static Texture2D CloneTexture(Texture2D source)
	{
		if (source == null) return null;
		int width = source.width;
		int height = source.height;
		var format = source.format;
		var tex = new Texture2D(width, height, format, false);
		tex.filterMode = FilterMode.Point;
        Graphics.CopyTexture(source, tex);
		return tex;
	}
}
