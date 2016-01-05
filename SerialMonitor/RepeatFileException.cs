using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SerialMonitor
{
   class RepeatFileException : Exception
   {
      public RepeatFileException(string message, params object[] parameters)
         : base(string.Format(message, parameters))
      {
      }
   }
}
