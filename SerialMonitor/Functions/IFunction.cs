using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
