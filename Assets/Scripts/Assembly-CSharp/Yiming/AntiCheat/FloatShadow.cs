using System;
using System.Runtime.InteropServices;

namespace Yiming.AntiCheat
{
    public struct FloatShadow : IFormattable
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public int i;

            public static int ToInt(float value) => new FloatIntUnion { f = value }.i;
            public static float ToFloat(int value) => new FloatIntUnion { i = value }.f;
        }

        private int encrypted;
        private int check;

        public FloatShadow(float value)
        {
            int raw = FloatIntUnion.ToInt(value);
            int salt = GenerateSalt(raw);
            encrypted = raw ^ salt;
            check = salt;
        }

        private static int GenerateSalt(int baseValue)
        {
            return baseValue ^ Environment.TickCount;
        }

        private float Value
        {
            get
            {
                int raw = encrypted ^ check;
                if ((raw ^ check) != encrypted)
                {
                    CheatingDetector.OnCheatDetected();
                }
                return FloatIntUnion.ToFloat(raw);
            }
            set
            {
                int raw = FloatIntUnion.ToInt(value);
                int salt = GenerateSalt(raw);
                encrypted = raw ^ salt;
                check = salt;
            }
        }

        public static implicit operator FloatShadow(float value)
        {
            return new FloatShadow(value);
        }

        public static implicit operator float(FloatShadow value)
        {
            return value.Value;
        }

        public override string ToString() => Value.ToString();
        public string ToString(string format) => Value.ToString(format);
        public string ToString(IFormatProvider provider) => Value.ToString(provider);
        public string ToString(string format, IFormatProvider provider) => Value.ToString(format, provider);
    }
}
