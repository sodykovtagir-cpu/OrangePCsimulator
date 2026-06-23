using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PC.Component;
using UnityEngine;

public class LedDisplay : Device
{
	private enum DisplayType
	{
		Clock = 0,
		Custom = 1,
		Temperature = 2
	}

	private abstract class MatrixAnimation
	{
		protected Material mat;

		public MatrixAnimation(Material mat)
		{
			this.mat = mat;
		}

		public abstract void Refresh();
	}

	private abstract class GenerativeAnimation : MatrixAnimation
	{
		private Texture2D texture;

		private static readonly byte[,] numbers = new byte[,]
		{
			// 0
			{ 0b01111110, 0b01000010, 0b01111110, 0b00000000 },
			// 1
			{ 0b00000000, 0b00000000, 0b01111110, 0b00000000 },
			// 2
			{ 0b01111010, 0b01001010, 0b01001110, 0b00000000 },
			// 3
			{ 0b01001010, 0b01001010, 0b01111110, 0b00000000 },
			// 4
			{ 0b00001110, 0b00001000, 0b01111110, 0b00000000 },
			// 5
			{ 0b01001110, 0b01001010, 0b01111010, 0b00000000 },
			// 6
			{ 0b01111110, 0b01001010, 0b01111010, 0b00000000 },
			// 7
			{ 0b00000010, 0b00000010, 0b01111110, 0b00000000 },
			// 8
			{ 0b01111110, 0b01001010, 0b01111110, 0b00000000 },
			// 9
			{ 0b01001110, 0b01001010, 0b01111110, 0b00000000 }
		};

		public GenerativeAnimation(Vector2Int targetResolution, Material mat)
			: base(mat)
        {
            this.mat = mat;
			texture = new Texture2D(targetResolution.x, targetResolution.y, UnityEngine.TextureFormat.RGBA32, false);
			texture.filterMode = FilterMode.Point;
			ResetScreen();
			texture.Apply();
			if (mat != null) mat.mainTexture = texture;
        }

		public void NumericDisplay(int input, int maxDigit)
		{
			ResetScreen();
			if (maxDigit <= 0)
			{
				if (texture != null) texture.Apply();
				return;
			}

			for (int d = 0; d < maxDigit; d++)
			{
				int exp = maxDigit - 1 - d;
				int div = (int)UnityEngine.Mathf.Pow(10f, exp);
				int v = div != 0 ? input / div : input;
				int digit = UnityEngine.Mathf.Abs(v % 10);
				var colOn = d > 1 ? new UnityEngine.Color(1f, 0f, 0f, 1f) : new UnityEngine.Color(0f, 1f, 1f, 1f);

				for (int col = 0; col < 4; col++)
				{
					byte mask = numbers[digit,col];
					for (int row = 0; row < 8; row++)
					{
						if ((mask & (1 << (7 - row))) != 0)
							texture.SetPixel(d * 4 + col, row, colOn);
					}
				}
			}

			if (texture != null) texture.Apply();
		}

		public void ResetScreen()
		{
			if (texture == null) return;
			int w = texture.width;
			int h = texture.height;
			var colors = new UnityEngine.Color[w * h];
			var black = new UnityEngine.Color(0f, 0f, 0f, 1f);
			for (int i = 0; i < colors.Length; i++) colors[i] = black;
			texture.SetPixels(colors);
		}
	}

	private class ClockAnimation : GenerativeAnimation
	{
		public ClockAnimation(Vector2Int targetResolution, Material mat)
			: base(targetResolution, mat)
		{}

		public override void Refresh()
		{
			var now = System.DateTime.Now;
			NumericDisplay(now.Minute * 100 + now.Second, 4);
		}
	}

	private class TextureAnimation : MatrixAnimation
	{
		private Texture2D[] textures;

		private int index;

		public TextureAnimation(Texture2D[] textures, Material mat) : base(mat)
		{
			this.textures = textures;
		}

		public override void Refresh()
		{
			if (textures == null || textures.Length == 0) return;
			if (mat == null) return;

			mat.mainTexture = textures[index];
			index = (index + 1) % textures.Length;
		}
	}

	private class TemperatureAnimation : GenerativeAnimation
	{
		private ICooler cooler;

		public TemperatureAnimation(GameObject obj, Vector2Int targetResolution, Material mat)
			: base(targetResolution, mat)
        {
			cooler = obj.GetComponentInParent<ICooler>();
        }

		public override void Refresh()
		{
			if (cooler == null) return;
			int value = (int)cooler.Temperature;
			value = System.Math.Min(value, 99);
			NumericDisplay(value, 2);
		}
	}

	[SerializeField]
	private Renderer screen;

	[SerializeField]
	private DisplayType type;

	public Vector2Int targetResolution = new Vector2Int(16, 8);

	[SerializeField]
	private Texture2D[] customTextures;

	[SerializeField]
	private float refreshInterval = 0.5f;

	private Material mat;

	private MatrixAnimation anim;

	private string data;

	void Start()
	{
		mat = screen.material;
		switch (type)
		{
			case DisplayType.Clock:
				anim = new ClockAnimation(targetResolution, mat);
				break;
			case DisplayType.Custom:
				anim = new TextureAnimation(customTextures, mat);
				break;
			case DisplayType.Temperature:
				anim = new TemperatureAnimation(gameObject, targetResolution, mat);
				break;
		}
		InvokeRepeating("Refresh", 0f, refreshInterval);
	}

	public void Refresh()
	{
		anim?.Refresh();
	}

	
	public void UploadPattern(IEnumerable<Texture2D> source, float refreshInterval)
	{
		customTextures = source.Select(x => FormatConverter.CloneTexture(x)).ToArray();
		anim = new TextureAnimation(customTextures,mat);

		var mov = new FormatConverter.Mov();
		mov.texs = customTextures;
		mov.interval = refreshInterval;
		data = FormatConverter.MovToString(mov);

		this.refreshInterval = refreshInterval;
		CancelInvoke("Refresh");
		InvokeRepeating("Refresh", 0f, refreshInterval);
	}

	public override void ToData(JObject jObject)
	{
		base.ToData(jObject);
		if (data == null) return;
		jObject["dat"] = data;
	}

	public override void FromData(JObject jObject)
	{
		base.FromData(jObject);

		if (jObject != null && jObject.TryGetValue("dat", out var token))
		{
			var dat = token.ToString();
			data = dat;

			var mov = FormatConverter.StringToMov(dat, true);
			if (mov != null)
			{
				customTextures = mov.texs;
				type = DisplayType.Custom;
				refreshInterval = mov.interval;
			}
		}
	}
}
