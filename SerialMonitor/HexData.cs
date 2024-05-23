//---------------------------------------------------------------------------
//
// Name:        HexData.cs
// Author:      Vita Tucek
// Created:     23.5.2024
// License:     MIT
// Description: Hexadecimal repeat data definition
//
//---------------------------------------------------------------------------

using SerialMonitor.Functions;
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace SerialMonitor
{
    internal class HexData
    {
        private static readonly Regex regWhite = new Regex("\\s+");
        /// <summary>
        /// Properties
        /// </summary>
        public ushort[]? MyProperty { get; private set; }
        /// <summary>
        /// Variable ID and Index in MyProperty array collection
        /// </summary>
        public Dictionary<byte,int>? Variables { get; private set; }
        /// <summary>
        /// Index in Property array and function ID collection
        /// </summary>
        public Dictionary<int, IFunction>? Functions { get; private set; }

        private HexData()
        {

        }

        /// <summary>
        /// Create data instance
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static HexData Create(string data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var hexData = new HexData();
            var trimmed = data.Trim().Replace("0x", "");

            // delimited
            if (regWhite.IsMatch(trimmed))
            {
                var bytes = regWhite.Split(trimmed);

                var functions = bytes.Select((x, i) => new { Index = i, Item = x })
                    .Where(x => x.Item.StartsWith('#')).Select(x => new { x.Index, Item = x.Item[1..] });

                if (!functions.Any())
                {
                    hexData.MyProperty = bytes.Select(x => GetSingleByte(x)).ToArray();
                }
                else
                {
                    foreach (var f in functions)
                    {
                        if (!BuiltInFunctions.IsAvailable(f.Item))
                            throw new RepeatFileException($"Function {f.Item} is not supported");
                    }
                    // create functions
                    hexData.Functions = functions.ToDictionary(x => x.Index, x => BuiltInFunctions.Get(x.Item));
                    // alocate properties (depend on function space needed)
                    hexData.MyProperty = new ushort[bytes.Length - hexData.Functions.Count + hexData.Functions.Sum(x => x.Value.Size)];
                    // fill properties
                    int move = 1;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        // function exists
                        if (!hexData.Functions.TryGetValue(i, out var func))
                        {
                            hexData.MyProperty[i] = GetSingleByte(bytes[i]);
                            move = 1;
                        }
                        else
                        {
                            hexData.MyProperty[i] = (ushort)0x200u;
                            if (func.Size > 1)
                            {
                                for(int j=1;j<func.Size;j++)
                                    hexData.MyProperty[++i] = (ushort)(0x200u + j);
                            }
                        }
                    }
                }
            }
            else
            {
                // every byte has 2 chars
                hexData.MyProperty = new ushort[trimmed.Length / 2];
                for (int i = 0; i < trimmed.Length; i++)
                {
                    var singlenumber = trimmed.Substring(i, 2);
                    hexData.MyProperty[i] = GetSingleByte(singlenumber);
                }
            }

            hexData.Variables = hexData.MyProperty
                .Select((x, i) => new { Index = i, Item = x })
                .Where(x => IsVariable(x.Item)).ToDictionary(x => GetVariableId(x.Item), x => x.Index);

            return hexData;
        }

        /// <summary>
        /// Create data instance from data packet
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static HexData Create(byte[] data, int length)
        {
            var hexData = new HexData();
            hexData.MyProperty = new ushort[length];
            for (int i = 0; i < length; i++)
            {
                hexData.MyProperty[i] = data[i];
            }

            return hexData;
        }

        /// <summary>
        /// Variable checker
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool IsVariable(ushort data)
        {
            return ((data & 0x100u) == 0x100u);
        }

        /// <summary>
        /// Return lower byte
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte GetVariableId(ushort data)
        {
            return Convert.ToByte(data & 0xFFu);
        }


        /// <summary>
        /// Function checker
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool IsFunction(ushort data)
        {
            return ((data & 0x2FFu) == 0x200u);
        }

        /// <summary>
        /// Function checker
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool IsFunctionArea(ushort data)
        {
            return ((data & 0x200u) == 0x200u);
        }

        /// <summary>
        /// Return byte representation.
        /// Numeric value is as is.
        /// Variable symbol $xx is converted into 0x01xx
        /// </summary>
        /// <param name="singlenumber"></param>
        /// <returns></returns>
        private static ushort GetSingleByte(string singlenumber)
        {
            return Convert.ToUInt16(singlenumber.StartsWith('$')
                ? (0x100u + byte.Parse(singlenumber[1..]))
                : byte.Parse(singlenumber, NumberStyles.HexNumber));
        }
    }

    /// <summary>
    /// HexData collection
    /// </summary>
    internal class HexDataCollection
    {
        private readonly Dictionary<HexData, Tuple<HexData, HexData>> _data = new Dictionary<HexData, Tuple<HexData, HexData>>(
            new HexDataEqualityComparer());

        /// <summary>
        /// Clear collection
        /// </summary>
        public void Clear()
        {
            _data.Clear();
        }
        /// <summary>
        /// Count
        /// </summary>
        public int Count => _data.Count;
        /// <summary>
        /// Add data/value data into collection
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryAdd(HexData key, HexData value)
        {
            return _data.TryAdd(key, new Tuple<HexData, HexData>(key, value));
        }
        /// <summary>
        /// Get value by hex data
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(HexData key, out Tuple<HexData, HexData>? value)
        {
            return _data.TryGetValue(key, out value);
        }
        /// <summary>
        /// Get value by raw data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(byte[] data, int length, out byte[] value)
        {
            var hexkey = HexData.Create(data, length);

            if (!_data.TryGetValue(hexkey, out var output))
            {
                value = [];
                return false;
            }
            else
            {
                var hexvalue = output.Item2;
                if (hexvalue.MyProperty == null)
                {
                    value = [];
                    return false;
                }

                value = new byte[hexvalue.MyProperty.Length];

                for (int i = 0; i < hexvalue.MyProperty.Length; i++)
                {
                    if (HexData.IsVariable(hexvalue.MyProperty[i]))
                    {
                        // variables not defined
                        if (output.Item1.Variables == null)
                        {
                            value[i] = 0;
                            continue;
                        }
                        // get variable ID from answer
                        var id = HexData.GetVariableId(hexvalue.MyProperty[i]);
                        // look for variable in ask
                        if (!output.Item1.Variables.TryGetValue(id, out var index))
                        {
                            value[i] = 0;
                        }
                        else
                        {
                            // copy data from source from specified index
                            value[i] = data[index];
                        }
                    }
                    else if (hexvalue.Functions != null && HexData.IsFunction(hexvalue.MyProperty[i]))
                    {
                        hexvalue.Functions[i].Compute(value, i);
                    }
                    else if (HexData.IsFunctionArea(hexvalue.MyProperty[i]))
                    {
                        continue;
                    }
                    else
                    {
                        value[i] = Convert.ToByte(hexvalue.MyProperty[i] & 0xFF);
                    }
                }
            }

            return true;
        }
    }

    internal class HexDataEqualityComparer : IEqualityComparer<HexData>
    {
        public bool Equals(HexData? a, HexData? b)
        {
            if (a?.MyProperty == null || b?.MyProperty == null)
                return false;

            if (a.MyProperty.Length != b.MyProperty.Length)
                return false;

            if (a.MyProperty?.SequenceEqual(b.MyProperty!) == true)
                return true;

            // compare with variables
            for (int i = 0; i < a.MyProperty!.Length; i++)
            {
                // variable or function
                if (((a.MyProperty[i] & 0x300u) == 0
                    && (b.MyProperty![i] & 0x300u) == 0)
                    && a.MyProperty[i] != b.MyProperty[i])
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(HexData? p)
        {
            return 0;
        }
    }
}
