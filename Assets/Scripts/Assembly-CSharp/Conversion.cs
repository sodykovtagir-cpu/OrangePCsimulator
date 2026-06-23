public static class Conversion
{
	public static string Size(int i)
	{
		if (i < 1000)
		return i.ToString() + "MB";
		if (i < 1_000_000)
		return (i / 1000f).ToString("0.#") + "GB";
		return (i / 1_000_000f).ToString("0.#") + "TB";
	}

	public static float Map(float s, float a1, float a2, float b1, float b2)
	{
		return ((s - a1) * (b2 - b1)) / (a2 - a1) + b1;
	}
}
