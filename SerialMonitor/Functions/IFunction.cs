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
        /// Data space needed in the packet
        /// </summary>
        int Size { get; }
        /// <summary>
        /// Function position in the packet
        /// </summary>
        int Position { get; }
        /// <summary>
        /// Start byte of the computing in the packet. Could be also lower range of the function.
        /// </summary>
        int Start { get; }
        /// <summary>
        /// End byte of the computing in the packet. Could be also higher range of the function.
        /// </summary>
        int End { get; }
        /// <summary>
        /// Compute function over data in source from Start to End or Position. Result is put at Position.
        /// </summary>
        /// <param name="data"></param>
        void Compute(byte[] data);
    }
}
