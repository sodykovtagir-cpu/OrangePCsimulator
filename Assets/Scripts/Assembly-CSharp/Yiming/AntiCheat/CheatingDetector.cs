using System;
using System.Runtime.CompilerServices;

namespace Yiming.AntiCheat
{
	public static class CheatingDetector
	{
		public static event Action CheatDetected;

		public static void OnCheatDetected()
		{
			CheatDetected.Invoke();
		}
	}
}
