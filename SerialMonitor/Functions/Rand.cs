//---------------------------------------------------------------------------
//
// Name:        Rand.cs
// Author:      Vita Tucek
// Created:     1.9.2025
// License:     MIT
// Description: Get random number
//
//---------------------------------------------------------------------------

namespace SerialMonitor.Functions
{
    public class Rand : FunctionBase
    {
        private static readonly Random random = new Random();
        public Rand(int position) : base(position)
        {
        }

        public Rand(int position, int start, int end) : base(position, Math.Max(0, start), Math.Min(255, end))
        {
        }

        public override int Size => 1;

        public override void Compute(byte[] data)
        {
            var value = random.Next(Start, End);
            data[Position] = (byte)value;
        }
    }
}
