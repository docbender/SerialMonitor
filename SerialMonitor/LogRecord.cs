using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SerialMonitor
{
    public enum LogRecordType
    {        
        DataReceived,
        DataSent,
        ControlPinChanged,
        ControlPinOn,
        ControlPinOff,
        Time,
        Error,
        Warning,
        Default,
    }

    internal class LogRecord
    {
        public static bool ShowTime { get; set; } = true;
        public LogRecord(string text, LogRecordType type, TraceEventType level)
        {
            Text = text;
            Level = level;
            Type = type;
            TimeStamp = DateTime.Now;
        }
        public string Text { get; set; }
        public TraceEventType Level { get; }
        public LogRecordType Type { get; }
        public DateTime TimeStamp { get; private set; }
        public int Length { get => Text.Length; }

        public static LogRecord operator +(LogRecord left, LogRecord right) => (new LogRecord(left.Text + right.Text, left.Type, left.Level));
        public static LogRecord operator +(LogRecord left, string right) => (new LogRecord(left.Text + right, left.Type, left.Level));
        public static LogRecord operator +(LogRecord operand) => operand;

        public void Append(string message)
        {
            Text += message;
        }

        public override string ToString()
        {
            return Render();
        }

        private string Render()
        {
            if (ShowTime && (Type == LogRecordType.DataSent || Type == LogRecordType.DataReceived))
                return $"{TimeStamp:HH:mm:ss.fff} {Text}";

            return Text;
        }
    }
}
