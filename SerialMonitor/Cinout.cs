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

      static int rts, cts, dtr, dsr, cd, brk;
      static string port;
      static bool isOpen;
      static int baudrate;
      static bool showAscii = false, pausePrint = false, pauseConnection = false;

      static bool sendType = false;
      static bool sendFile = false;

      public static List<string> CommandHistory = new List<string>();
      static StringBuilder inBuffer = new StringBuilder();
      static string sendLineMessage;

      public static void Init()
      {
         Console.CursorVisible = false;

         DefaultFore = Console.ForegroundColor;

         Messages = new List<Line>();
         Messages.Add(new Line("", DefaultFore));
         lockobj = new object();

         WritePortStatus("", false, 0);
         WritePinStatus(-1, -1, -1, -1, -1, -1);
      }

      public static void Render()
      {
         Console.Clear();

         borders();
         writePortStatus();
         writePinStatus();
         printMessages();
         writeMenuBar();

         if(sendType || sendFile)
            printSendLine();
      }

      protected static void borders()
      {
         if(Service)
            return;

         Console.ResetColor();
         Console.SetCursorPosition(0, 0);

         if(Console.WindowWidth < 20)
         {
            Console.WriteLine("Window is too small!!!!");
            return;
         }
         string name = " " + Application.ProductName + " v." + Application.ProductVersion.Substring(0, Application.ProductVersion.Length - 2) + " ";

         Console.WriteLine("+" + new string('-', Console.WindowWidth - 2) + "+");
         if(Console.WindowWidth > name.Length)
         {
            Console.SetCursorPosition(Console.WindowWidth/2 - name.Length/2, 0);
            Console.Write(name);
         }

         Console.SetCursorPosition(0, 1);
         Console.Write("|" + new string(' ', Console.WindowWidth - 2) + "|");
         Console.SetCursorPosition(0, 2);
         Console.Write("|" + new string(' ', Console.WindowWidth - 2) + "|");
         Console.WriteLine("+" + new string('-', Console.WindowWidth - 2) + "+");

         originalheight = Console.WindowHeight;
         originalwidth = Console.WindowWidth;
      }

      public static void WritePortStatus(string port, bool isOpen, int baudrate)
      {
         Cinout.port = port;
         Cinout.isOpen = isOpen;
         Cinout.baudrate = baudrate;

         Render();
      }

      protected static void writePortStatus()
      {
         Console.ResetColor();
         Console.SetCursorPosition(2, 1);
         Console.Write("Port:   " + port + "  ");
         if(isOpen)
         {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Opened");
         }
         else
         {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Closed");
         }

         Console.ResetColor();

         Console.Write("  Speed: {0}b/s", baudrate);
      }

      public static void WritePinStatus(int rts, int cts, int dtr, int dsr, int cd, int brk)
      {
         Cinout.rts = rts;
         Cinout.cts = cts;
         Cinout.dtr = dtr;
         Cinout.dsr = dsr;
         Cinout.cd = cd;
         Cinout.brk = brk;

         Render();
      }

      protected static void writePinStatus()
      {
         Console.ResetColor();
         Console.SetCursorPosition(2, 2);
         Console.Write("Pins: ");

         printPin("RTS", rts, ConsoleColor.Green);
         printPin("CTS", cts, ConsoleColor.Green);
         printPin("DTR", dtr, ConsoleColor.Green);
         printPin("DSR", dsr, ConsoleColor.Green);
         printPin("CD", cd, ConsoleColor.Green);
         printPin("BREAK", brk, ConsoleColor.Red);
      }

      protected static void printPin(string pin, int state, ConsoleColor activeColor)
      {
         Console.Write("  {0}", pin);

         if(state == 1)
            Console.ForegroundColor = activeColor;

         Console.Write(" ({0})", (state < 0 || state > 1) ? "?" : state.ToString());

         Console.ResetColor();
      }

      protected static void writeMenuBar()
      {
         Console.SetCursorPosition(0, Console.WindowHeight-1);

         if(sendType || sendFile)
         {
            Console.ResetColor();
            Console.Write("Enter"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write(" Send ");
            Console.ResetColor();
            Console.Write("Esc"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write(" Return ");
         }
         else
         {
            Console.ResetColor();
            Console.Write("F1"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write("Help ");
            Console.ResetColor();
            Console.Write("F2"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; if(!pausePrint) Console.Write("NoPrint "); else Console.Write("Print   ");
            Console.ResetColor();
            Console.Write("F3"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; if(showAscii) Console.Write("Hex   "); else Console.Write("Ascii ");
            Console.ResetColor();
            Console.Write("F4"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; if(!pauseConnection) Console.Write("Close  "); else Console.Write("Resume ");
            Console.ResetColor();
            Console.Write("F5"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write("Send ");
            Console.ResetColor();
            Console.Write("F6"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write("SendFile ");
            Console.ResetColor();
            Console.Write("F10"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write("Exit ");
            Console.ResetColor();
            Console.Write("F11"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write("RTS ");
            Console.ResetColor();
            Console.Write("F12"); Console.BackgroundColor = ConsoleColor.Cyan; Console.ForegroundColor = ConsoleColor.Black; Console.Write("DTR ");
         }

         Console.ResetColor();
      }

      public static void WriteMenuBar(bool showAscii, bool pausePrint, bool pauseConnection)
      {
         Cinout.showAscii = showAscii;
         Cinout.pausePrint = pausePrint;
         Cinout.pauseConnection = pauseConnection;

         Render();
      }

      public static void WriteLine(string Message, object[] parameters)
      {
         WriteLine(DefaultFore, Message, parameters);
      }

      public static void WriteLine(ConsoleColor activeColor, string Message, object[] parameters)
      {
         if(Service)
            return;

         Write(activeColor, true, Message, parameters);
         Write(activeColor, false, "\n", parameters);
      }

      public static void Write(string Message, object[] parameters)
      {
         Write(DefaultFore, false, Message, parameters);
      }

      public static void Write(ConsoleColor activeColor, string Message, object[] parameters)
      {
         Write(activeColor, false, Message, parameters);
      }

      public static void Write(ConsoleColor activeColor, bool onNewLine, string Message, object[] parameters)
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

                  if(i==0 && onNewLine && (Messages[Messages.Count - 1].Text.Length > 0))
                  {
                     if(l.Length <= Console.WindowWidth)
                        Messages.Add(new Line(l, activeColor));
                     else
                        splitAndPrintLongLines(activeColor, l);
                  }
                  else
                  {
                     if(l.Length + Messages[Messages.Count - 1].Text.Length <= Console.WindowWidth)
                     {
                        Messages[Messages.Count - 1].Text += l;
                        if(l.Length > 0)
                           Messages[Messages.Count - 1].Color = activeColor;
                     }
                     else
                     {
                        int currentLeft = Console.WindowWidth - Messages[Messages.Count - 1].Text.Length;

                        Messages[Messages.Count - 1].Text += l.Substring(0, currentLeft);

                        if(l.Length > 0)
                           Messages[Messages.Count - 1].Color = activeColor;

                        splitAndPrintLongLines(activeColor, l.Substring(currentLeft));
                     }
                  }

                  if(i < lines.Length - 1)
                     Messages.Add(new Line("", activeColor));
               }
            }
            else
            {
               if(onNewLine && (Messages[Messages.Count - 1].Text.Length > 0))
               {
                  if(msg.Length <= Console.WindowWidth)
                     Messages.Add(new Line(msg, activeColor));
                  else
                     splitAndPrintLongLines(activeColor, msg);
               }
               else
               {
                  if(msg.Length + Messages[Messages.Count - 1].Text.Length <= Console.WindowWidth)
                  {
                     Messages[Messages.Count - 1].Text += msg;
                     if(msg.Length > 0)
                        Messages[Messages.Count - 1].Color = activeColor;
                  }
                  else
                  {
                     int currentLeft = Console.WindowWidth - Messages[Messages.Count - 1].Text.Length;

                     Messages[Messages.Count - 1].Text += msg.Substring(0, currentLeft);

                     if(msg.Length > 0)
                        Messages[Messages.Count - 1].Color = activeColor;

                     splitAndPrintLongLines(activeColor, msg.Substring(currentLeft));
                  }
               }
            }

            Render();
         }
      }

      protected static void printMessages()
      {
         int rows = Console.WindowHeight - 4 - 1 - 1;

         if(rows <= 0)
            return;

         if(Messages.Count > rows)
            Messages.RemoveRange(0, Messages.Count - rows);

         for(int i = 0;i < Messages.Count;i++)
         {
            Console.SetCursorPosition(0, 4 + i);

            Console.ForegroundColor = Messages[i].Color;
            if(Console.WindowWidth >= Messages[i].Text.Length)
               Console.Write(Messages[i].Text + new string(' ', Console.WindowWidth - Messages[i].Text.Length));
            else
               Console.Write(Messages[i].Text);
         }
      }

      protected static void splitAndPrintLongLines(ConsoleColor activeColor, string msg)
      {
         string line = msg;
         while(line.Length > 0)
         {
            if(line.Length > Console.WindowWidth)
            {
               Messages.Add(new Line(line.Substring(0, Console.WindowWidth), activeColor));

               line = line.Substring(Console.WindowWidth);
            }
            else
            {
               Messages.Add(new Line(line, activeColor));
               break;
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

      #region send file

      public static void StartSendFile()
      {
         sendFile = true;
         inBuffer = new StringBuilder();
         sendLineMessage = "File to send: ";
         printSendLine();

         Console.CursorVisible = true;

         Render();
      }

      public static void EndSendFile()
      {
         sendFile = false;
         Console.CursorVisible = false;

         Render();
      }

      #endregion

      #region send data type

      public static void StartSendDataType()
      {
         sendType = true;
         inBuffer = new StringBuilder();
         sendLineMessage = "Message to send: ";

         Console.CursorVisible = true;

         Render();
      }

      public static void EndSendDataType()
      {
         sendType = false;
         Console.CursorVisible = false;

         Render();
      }
      #endregion

      protected static void printSendLine()
      {
         Console.SetCursorPosition(0, Console.WindowHeight-2);
         Console.Write(new string(' ', Console.WindowWidth));
         Console.SetCursorPosition(0, Console.WindowHeight-2);

         string begin = sendLineMessage;
         Console.Write(begin);

         if(begin.Length + inBuffer.Length < Console.WindowWidth)
         {
            Console.Write(inBuffer.ToString());
            Console.SetCursorPosition(begin.Length + inBuffer.Length, Console.WindowHeight-2);
         }
         else
         {
            Console.Write(inBuffer.ToString().Substring(begin.Length + inBuffer.Length-Console.WindowWidth+1));
            Console.SetCursorPosition(Console.WindowWidth-1, Console.WindowHeight-2);
         }
      }

      protected static void putSendDataChar(char c)
      {
         inBuffer.Append(c);

         printSendLine();
      }

      protected static void removeSendDataChar()
      {
         inBuffer.Remove(inBuffer.Length-1, 1);

         printSendLine();
      }

      protected static void putSendDataLine(string s)
      {
         if(inBuffer.Length > 0)
            inBuffer.Remove(0, inBuffer.Length);

         inBuffer.Append(s);

         printSendLine();
      }

      /// <summary>
      /// Command key decoder
      /// </summary>
      /// <returns></returns>
      public static CommandEnum ConsoleReadCommand(bool hideCursor)
      {
         if(Console.KeyAvailable)
         {
            ConsoleKeyInfo k = Console.ReadKey(hideCursor);

            //command keys
            if(k.Key == ConsoleKey.F1)
               return CommandEnum.HELP;
            else if(k.Key == ConsoleKey.F2)
               return CommandEnum.PAUSE;
            else if(k.Key == ConsoleKey.F3)
               return CommandEnum.FORMAT;
            else if(k.Key == ConsoleKey.F4)
               return CommandEnum.CONNECT;
            else if(k.Key == ConsoleKey.F5)
               return CommandEnum.SEND;
            else if(k.Key == ConsoleKey.F6)
               return CommandEnum.SEND_FILE;
            else if(k.Key == ConsoleKey.F10)
               return CommandEnum.EXIT;
            else if(k.Key == ConsoleKey.F11)
               return CommandEnum.RTS;
            else if(k.Key == ConsoleKey.F12)
               return CommandEnum.DTR;
            else if(k.KeyChar != 0)
               putSendDataChar(k.KeyChar);
         }

         return CommandEnum.NONE;
      }

      /// <summary>
      /// Read typed char to console
      /// </summary>
      /// <returns></returns>
      public static string ConsoleReadLine(bool hideCursor)
      {
         ConsoleKeyInfo k = Console.ReadKey(hideCursor);
         int historyPosition = CommandHistory.Count;

         while(k.Key != ConsoleKey.Enter && k.Key != ConsoleKey.Escape)
         {
            if(k.Key == ConsoleKey.Backspace)
            {
               if(inBuffer.Length > 0)
               {
                  removeSendDataChar();
               }
            }
            else if(k.Key == ConsoleKey.UpArrow)
            {
               if(historyPosition-1 >= 0)
               {
                  string hist = CommandHistory[--historyPosition];

                  Console.CursorLeft = 0;
                  Console.Write(new string(' ', Console.WindowWidth-1));
                  Console.CursorLeft = 0;
                  Console.Write(hist);

                  putSendDataLine(hist);
               }
            }
            else if(k.Key == ConsoleKey.DownArrow)
            {
               if(historyPosition+1 < CommandHistory.Count)
               {
                  string hist = CommandHistory[++historyPosition];

                  Console.CursorLeft = 0;
                  Console.Write(new string(' ', Console.WindowWidth-1));
                  Console.CursorLeft = 0;
                  Console.Write(hist);

                  putSendDataLine(hist);
               }
            }
            else if(k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.RightArrow)
            {

            }
            else if(k.KeyChar != 0)
            {
               putSendDataChar(k.KeyChar);
            }

            k = Console.ReadKey(hideCursor);
         }

         if(k.Key == ConsoleKey.Enter)
         {
            string line = inBuffer.ToString();
            inBuffer.Remove(0, inBuffer.Length);

            if(CommandHistory.Count == 0 || !CommandHistory[CommandHistory.Count-1].Equals(line))
               CommandHistory.Add(line);

            return line;
         }

         return null;
      }
   }
}
