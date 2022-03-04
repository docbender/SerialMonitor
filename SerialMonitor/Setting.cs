//---------------------------------------------------------------------------
//
// Name:        Program.cs
// Author:      Vita Tucek
// Created:     4.3.2022
// License:     MIT
// Description: setting
//
//---------------------------------------------------------------------------

using System.IO.Ports;

namespace SerialMonitor
{
    public class Setting
    {
        public string Port;
        public int BaudRate;
        public Parity Parity;
        public int DataBits;
        public StopBits StopBits;
        public bool ShowTime;
        public bool ShowTimeGap;
        public bool ShowSentData;
        public bool ShowAscii;
    }
}
