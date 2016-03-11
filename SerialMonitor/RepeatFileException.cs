//---------------------------------------------------------------------------
//
// Name:        RepeatFileException.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Exception class
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
