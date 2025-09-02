//---------------------------------------------------------------------------
//
// Name:        Sum.cs
// Author:      Vita Tucek
// Created:     1.9.2025
// License:     MIT
// Description: Checksum calculation. Calculate a summary of a data portion
//
//---------------------------------------------------------------------------

namespace SerialMonitor.Functions
{
    public class Sum : FunctionBase
    {
        public Sum(int position) : base(position)
        {
        }

        public Sum(int position, int start, int end) : base(position, start, end)
        {
        }

        public override int Size => 1;

        public override void Compute(byte[] data)
        {
            int end = End < Position ? End : Position-1;
            int sum = 0;
            for (int i = Start; i <= end; i++)
            {
                sum += data[i];
            }

            data[Position] = (byte)(sum & 255);
        }
    }
}
