//---------------------------------------------------------------------------
//
// Name:        IFunction.cs
// Author:      Vita Tucek
// Created:     23.5.2024
// License:     MIT
// Description: Functions interface
//
//---------------------------------------------------------------------------

namespace SerialMonitor.Functions
{
    public interface IFunction
    {
        /// <summary>
        /// Data space needed in packet
        /// </summary>
        int Size { get; }
        /// <summary>
        /// Compute function over data in source from 0 to position. Result is put at position.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="position"></param>
        void Compute(byte[] data, int position);

        static IFunction Instance { get; }
    }
}
