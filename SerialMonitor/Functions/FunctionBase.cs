using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialMonitor.Functions
{
    public abstract class FunctionBase : IFunction
    {
        public abstract int Size { get; }

        public int Position { get; }

        public int Start { get; }

        public int End { get; }

        public FunctionBase(int position)
        {
            Position = position;
            Start = 0;
            End = int.MaxValue;
        }

        public FunctionBase(int position, int start, int end) 
        {
            Position = position;
            Start = start;
            End = end;
        }

        public abstract void Compute(byte[] data);
    }
}
