using System;
using System.Collections.Generic;
using System.Text;

namespace SerialMonitor
{
   /// <summary>
   /// Command enumerator
   /// </summary>
   enum CommandEnum
   {
      NONE,
      EXIT,
      PAUSE,
      HELP,
      SEND,
      SEND_FILE,
      CONNECT,
      RTS,
      DTR,
      FORMAT
   };
}
