//---------------------------------------------------------------------------
//
// Name:        Argument.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Command line argument class
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace SerialMonitor
{
   class Argument
   {
      private bool enabled;
      private string parameter;
      private string name;

      /// <summary>
      /// Create argument by name
      /// </summary>
      /// <param name="name"></param>
      public Argument(string name)
      {
         this.name = name;
         this.parameter = "";
         this.enabled = false;
      }

      /// <summary>
      /// Argument name
      /// </summary>
      public string Name
      {
         get
         {
            return name;
         }
      }

      /// <summary>
      /// Is argument enabled (is properly defined)
      /// </summary>
      public bool Enabled
      {
         get
         {
            return enabled;
         }
         set
         {
            enabled = value;
         }
      }

      /// <summary>
      /// Argument parameter
      /// </summary>
      public string Parameter
      {
         get
         {
            return parameter;
         }
         set
         {
            parameter = value;
         }
      }
   }
}
