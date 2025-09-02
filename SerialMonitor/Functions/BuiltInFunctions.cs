//---------------------------------------------------------------------------
//
// Name:        BuiltInFunctions.cs
// Author:      Vita Tucek
// Created:     23.5.2024
// License:     MIT
// Description: System functions
//
//---------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SerialMonitor.Functions
{
    internal class BuiltInFunctions
    {
        public static readonly string[] Available = [nameof(Crc16), nameof(Sum), nameof(Rand)];
        private static readonly Regex functionRegex = new Regex(@"^(\w+)(\[(\d*)\.{2}(\d*)\])?$", RegexOptions.Compiled);

        public static bool IsAvailable(string functionName)
        {
            int i = functionName.IndexOf('[');
            if (i >= 0)
                return Available.Any(a => a.Equals(functionName[..i], StringComparison.OrdinalIgnoreCase));
            else
                return Available.Any(a => a.Equals(functionName, StringComparison.OrdinalIgnoreCase));
        }

        public static IFunction Get(string functionName, int position)
        {
            var match = functionRegex.Match(functionName);
            if (!match.Success)
                throw new ArgumentException($"Invalid function definition '{functionName}'.");

            var f = Available.First(x => x.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            // group 1 = name
            // group 2 = range
            // group 3 = start
            // group 4 = end
            int start = match.Groups[2].Success && match.Groups[3].Success && match.Groups[3].Value.Length > 0 ? int.Parse(match.Groups[3].Value) : 0;
            int end = match.Groups[2].Success && match.Groups[4].Success && match.Groups[4].Value.Length > 0 ? int.Parse(match.Groups[4].Value) : int.MaxValue;

            if (f.Equals("Crc16"))
                return new Crc16(position, start, end);
            else if (f.Equals("Sum"))
                return new Sum(position, start, end);
            else if (f.Equals("Rand"))
                return new Rand(position, start, end);

            throw new NotImplementedException($"Function {functionName} is not implemented.");
        }
    }
}
