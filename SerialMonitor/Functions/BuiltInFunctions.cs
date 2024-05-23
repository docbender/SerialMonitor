//---------------------------------------------------------------------------
//
// Name:        BuiltInFunctions.cs
// Author:      Vita Tucek
// Created:     23.5.2024
// License:     MIT
// Description: System functions
//
//---------------------------------------------------------------------------

namespace SerialMonitor.Functions
{
    internal class BuiltInFunctions
    {
        public static readonly string[] Available = [nameof(Crc16)];

        public static bool IsAvailable(string functionName)
        {
            return Available.Any(a => a.Equals(functionName, StringComparison.OrdinalIgnoreCase));
        }

        public static IFunction Get(string functionName)
        {
            var f = Available.First(x => x.Equals(functionName, StringComparison.OrdinalIgnoreCase));

            if (f.Equals("Crc16"))
                return Crc16.Instance;

            throw new NotImplementedException("Function {functionName} is not implemented.");
        }
    }
}
