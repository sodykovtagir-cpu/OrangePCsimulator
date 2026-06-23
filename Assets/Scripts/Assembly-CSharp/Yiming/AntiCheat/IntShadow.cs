using System;

namespace Yiming.AntiCheat
{
    public struct IntShadow : IFormattable
    {
        private int encrypted;
        private int check;

        public IntShadow(int value)
        {
            int salt = GenerateSalt(value);
            encrypted = value ^ salt;
            check = salt;
        }

        private static int GenerateSalt(int baseValue)
        {
            return baseValue ^ Environment.TickCount;
        }

        private int Value
        {
            get
            {
                int decrypted = encrypted ^ check;
                if ((decrypted ^ check) != encrypted)
                {
                    CheatingDetector.OnCheatDetected();
                }
                return decrypted;
            }
            set
            {
                int salt = GenerateSalt(value);
                encrypted = value ^ salt;
                check = salt;
            }
        }

        public static implicit operator IntShadow(int x)
        {
            return new IntShadow(x);
        }

        public static implicit operator int(IntShadow x)
        {
            return x.Value;
        }

        public override string ToString() => Value.ToString();

        public string ToString(string format) => Value.ToString(format);

        public string ToString(IFormatProvider formatProvider) => Value.ToString(formatProvider);

        public string ToString(string format, IFormatProvider formatProvider) => Value.ToString(format, formatProvider);
    }
}
