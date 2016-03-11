//---------------------------------------------------------------------------
//
// Name:        Cinout.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Console GUI
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SerialMonitor
{
   class Cinout
   {
      class Line
      {
         public string Text;
         public ConsoleColor Color;

         public Line(string text, ConsoleColor color)
         {
            Text = text;
            Color = color;
         }
      }

      static List<Line> Messages;
      static object lockobj;
      static int originalheight = 0, originalwidth = 0;
      static bool Service = false;
      static ConsoleColor DefaultFore = ConsoleColor.Gray;

      public static void Init()
      {
         DefaultFore = Console.ForegroundColor;

         Messages = new List<Line>();
         Messages.Add(new Line("", DefaultFore));
         lockobj = new object();

         Borders();
      }

      public static void Borders()
      {
         if(Service)
            return;

         Console.SetCursorPosition(0, 0);

         if(Console.WindowWidth < 20)
         {
            Console.WriteLine("Window is too small!!!!");
            return;
         }

         int rightSpaceLeft = Console.WindowWidth - 10 - 13 - 17 - 10 - 6;
         if(rightSpaceLeft < 0)
            rightSpaceLeft = 0;
         Console.WriteLine("+" + new string('-', 10) + "+" + new string('-', 13) + "+" + new string('-', 17) + "+" + new string('-', 10) + "+" + new string('-', rightSpaceLeft) + "+");
         for(int i = 1;i < 3;i++)
         {
            Console.SetCursorPosition(0, i);
            Console.Write("|");
            Console.SetCursorPosition(Console.WindowWidth - 1, i);
            Console.Write("|");
         }
         Console.WriteLine("+" + new string('-', 10) + "+" + new string('-', 13) + "+" + new string('-', 17) + "+" + new string('-', 10) + "+" + new string('-', rightSpaceLeft) + "+");

         Console.SetCursorPosition(0, 4);
         Console.Write("|" + new string(' ', Console.WindowWidth - 2) + "|");
         Console.SetCursorPosition(0, 5);
         Console.Write("|" + new string(' ', Console.WindowWidth - 2) + "|");
         Console.WriteLine("+" + new string('-', Console.WindowWidth - 2) + "+");

         Console.SetCursorPosition(2, 1);
         Console.Write("F1 help  | F2 no print | F4 close/resume |          |");
         Console.SetCursorPosition(2, 2);
         Console.Write("F5 send  | F7 RTS pin  | F8 DTR pin      | F10 exit |");

         Console.SetCursorPosition(Console.WindowWidth - 16, 1);
         Console.Write(Application.ProductName);
         Console.SetCursorPosition(Console.WindowWidth - 14, 2);
         Console.Write("v." + Application.ProductVersion.Substring(0, Application.ProductVersion.Length - 2));

         WritePortStatus("", false, 0);
         WritePinStatus(-1, -1, -1, -1, -1, -1);

         Console.SetCursorPosition(0, 6);

         originalheight = Console.WindowHeight;
         originalwidth = Console.WindowWidth;
      }

      public static void WritePortStatus(string port, bool isOpen, int baudrate)
      {
         Console.SetCursorPosition(2, 4);
         Console.Write("Port:   " + port + "  " + (isOpen ? "Opened" : "Closed") + "  Speed: {0}b/s", baudrate);
      }

      public static void WritePinStatus(int rts, int cts, int dtr, int dsr, int cd, int brk)
      {
         Console.SetCursorPosition(2, 5);
         Console.Write("Pins: ");

         printPin("RTS", rts, ConsoleColor.Green);
         printPin("CTS", cts, ConsoleColor.Green);
         printPin("DTR", dtr, ConsoleColor.Green);
         printPin("DSR", dsr, ConsoleColor.Green);
         printPin("CD", cd, ConsoleColor.Green);
         printPin("BREAK", brk, ConsoleColor.Red);
      }

      private static void printPin(string pin, int state, ConsoleColor activeColor)
      {
         Console.Write("  {0}", pin);

         if(state == 1)
            Console.ForegroundColor = activeColor;

         Console.Write(" ({0})", (state < 0 || state > 1) ? "?" : state.ToString());

         Console.ResetColor();
      }

      public static void WriteLine(string Message, object[] parameters)
      {
         WriteLine(DefaultFore, Message, parameters);
      }

      public static void WriteLine(ConsoleColor activeColor, string Message, object[] parameters)
      {
         if(Service)
            return;

         Write(activeColor, Message, parameters);
         Write(activeColor, "\n", parameters);
      }

      public static void Write(string Message, object[] parameters)
      {
         Write(DefaultFore, Message, parameters);
      }

      public static void Write(ConsoleColor activeColor, string Message, object[] parameters)
      {
         if(Service)
            return;

         lock(lockobj)
         {
            string msg = string.Format(Message, parameters);
            if(msg.Contains("\n"))
            {
               string[] lines = msg.Split('\n');

               for(int i = 0;i < lines.Length;i++)
               {
                  string l = lines[i];

                  Messages[Messages.Count - 1].Text += l;
                  Messages[Messages.Count - 1].Color = activeColor;

                  if(i < lines.Length - 1)
                     Messages.Add(new Line("", activeColor));
               }
            }
            else
            {
               Messages[Messages.Count - 1].Text += msg;
               Messages[Messages.Count - 1].Color = activeColor;
            }

            int rows = Console.WindowHeight - 7 - 1;

            if(rows <= 0)
               return;

            if(originalheight != Console.WindowHeight || originalwidth != Console.WindowWidth)
            {
               Console.Clear();
               Borders();
            }

            if(Messages.Count > rows)
               Messages.RemoveRange(0, Messages.Count - rows);

            for(int i = 0;i < Messages.Count;i++)
            {
               Console.SetCursorPosition(0, 7 + i);

               Console.ForegroundColor = Messages[i].Color;
               if(Console.WindowWidth >= Messages[i].Text.Length)
                  Console.Write(Messages[i].Text + new string(' ', Console.WindowWidth - Messages[i].Text.Length));
               else
                  Console.Write(Messages[i].Text);
            }
         }
      }

      public static void CursorLeft(int moveBy)
      {
         if(moveBy > 0)
         {
            Messages[Messages.Count - 1].Text += new string(' ', moveBy);
         }
         else if(moveBy < 0)
         {
            if(Messages[Messages.Count - 1].Text.Length + moveBy >= 0)
            {
               Messages[Messages.Count - 1].Text = Messages[Messages.Count - 1].Text.Substring(0, Messages[Messages.Count - 1].Text.Length + moveBy);
            }
         }
      }

      public static void CursorLeftReset()
      {
         Messages[Messages.Count - 1].Text = "";
      }
   }
}
