//---------------------------------------------------------------------------
//
// Name:        Crc8.cs
// Author:      Vita Tucek
// Created:     18.10.2025
// License:     MIT
// Description: CRC8 calculation
//
//---------------------------------------------------------------------------

namespace SerialMonitor.Functions
{
    public class Crc8 : FunctionBase
    {
        public Crc8(int position) : base(position)
        {
        }

        public Crc8(int position, int start, int end) : base(position, start, end)
        {
        }

        public override int Size => 1;

        /// <summary>
        /// Compute over specified length
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte ComputeCrc(byte[] data, int position, int start, int end)
        {
            int dataend = end < position ? end : position - 1;
            int crc = 0;
            int i, j;

            for (j = start; j <= dataend; j++)
            {
                crc ^= (data[j] << 8);

                for (i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc ^= (0x1070 << 3);

                    crc <<= 1;
                }
            }
            return (byte)(crc >> 8);
        }

        public override void Compute(byte[] data)
        {
            var crc = Crc8.ComputeCrc(data, Position, Start, End);
            data[Position] = crc;
        }

        /// <summary>
        /// Verify data with CRC included
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static bool Verify(byte[] data)
        {
            return Verify(data, data.Length);
        }

        /// <summary>
        /// Verify data with CRC included
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static bool Verify(byte[] data, int length)
        {
            if (length < 3)
                return false;
            byte computed = ComputeCrc(data, length - 1, 0, length - 1);
            byte received = data[length - 1];

            return computed == received;
        }
    }
}
