using System;
using System.Collections.Generic;
using System.Text;

namespace SerialMonitor
{
   class ArgumentCollection : List<Argument>
   {
      /// <summary>
      /// Constructor with supported argument names as parameter
      /// </summary>
      /// <param name="supportedArguments"></param>
      public ArgumentCollection(string[] supportedArguments)
         : base(supportedArguments.Length)
      {
         foreach(string s in supportedArguments)
         {
            this.Add(new Argument(s));
         }
      }

      /// <summary>
      /// Parse program arguments and set apropriatly supported arguments
      /// </summary>
      /// <param name="args"></param>
      public void Parse(string[] args)
      {
         //if (args.Length < 1)
         //   return;

         //first argument should be always port name - ignore it???
         for (int i = 0; i < args.Length; i++)
         {
            if (args[i].StartsWith("-"))
            {
               string argName = args[i].Substring(1);
               bool notFound = true;

               foreach (Argument a in this)
               {
                  if (argName.Equals(a.Name))
                  {
                     a.Enabled = true;
                     if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        a.Parameter = args[i + 1];

                     notFound = false;
                     break;
                  }
               }

               if (notFound)
               {
                  Console.ForegroundColor = ConsoleColor.Red;
                  Console.WriteLine("Parameter {0} not supported", args[i]);
                  Console.ResetColor();
               }
            }
         }         
      }

      /// <summary>
      /// Return argument by name if found otherwise null
      /// </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public Argument GetArgument(string name)
      {
         foreach (Argument a in this)
         {
            if (a.Name.Equals(name))
            {
               return a;
            }
         }

         return null;
      }
   }
}
