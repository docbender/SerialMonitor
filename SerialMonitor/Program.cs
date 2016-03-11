﻿//---------------------------------------------------------------------------
//
// Name:        Program.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Main program
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Forms;

namespace SerialMonitor
{
   class Program
   {
      static ArgumentCollection arguments = new ArgumentCollection(new string[] { "baudrate", "parity", "databits", "stopbits", 
         "repeatfile", "logfile", "logincomingonly", "showascii", "notime", "gaptolerance", "continuousmode" });
      static string version = Application.ProductVersion.Substring(0, Application.ProductVersion.Length-2);
      static long lastTimeReceved = 0;
      static bool repeaterEnabled = false;
      static bool repeaterUseHex = false;
      static bool showAscii = false;
      static Dictionary<string, string> repeaterMap;
      static bool logfile = false;
      static bool logincomingonly = false;
      static string logFilename = "";
      static bool noTime = false;
      static int gapTolerance = 0;
      static bool gapToleranceEnable = false;
      static bool continuousMode = false;
      /// <summary>
      /// Flag for stop printing communication data. Log into file will continue.
      /// </summary>
      static bool pausePrint = false;
      /// <summary>
      /// Flag for pause / resume connection
      /// </summary>
      static bool pauseConnection = false;
      static StringBuilder inBuffer = new StringBuilder();

      /// <summary>
      /// Main loop
      /// </summary>
      /// <param name="args"></param>
      static void Main(string[] args)
      {
         string portName = IsRunningOnMono() ? "/dev/ttyS1" : "COM1";

         if(args.Length > 0)
         {
            if(args[0].Equals("-?") || args[0].Equals("-help") || args[0].Equals("--help") || args[0].Equals("?") || args[0].Equals("/?"))
            {
               consoleWriteLineNoTrace("SerialMonitor v." + version);
               printHelp();
               Console.WriteLine("\nPress [Enter] to exit");
               Console.ReadLine();
               return;
            }
            else
               portName = args[0];
         }
         else
         {
            string[] savedArgs = Config.Load();

            if(savedArgs!=null)
            {
               portName = savedArgs[0];
               args = savedArgs;
            }
         }

         arguments.Parse(args);

         continuousMode = arguments.GetArgument("continuousmode").Enabled;

         if(!continuousMode)
            Cinout.Init();
         else
            consoleWriteLineNoTrace("SerialMonitor v." + version);

         showAscii = arguments.GetArgument("showascii").Enabled;
         noTime = arguments.GetArgument("notime").Enabled;

         int baudrate = 9600;
         Parity parity = Parity.None;
         int dataBits = 8;
         StopBits stopBits = StopBits.One;

         Argument arg = arguments.GetArgument("baudrate");
         if(arg.Enabled)
            int.TryParse(arg.Parameter, out baudrate);

         arg = arguments.GetArgument("parity");
         if(arg.Enabled)
         {
            if(arg.Parameter.ToLower().Equals("odd"))
               parity = Parity.Odd;
            else if(arg.Parameter.ToLower().Equals("even"))
               parity = Parity.Even;
            else if(arg.Parameter.ToLower().Equals("mark"))
               parity = Parity.Mark;
            else if(arg.Parameter.ToLower().Equals("space"))
               parity = Parity.Space;
         }

         arg = arguments.GetArgument("databits");
         if(arg.Enabled)
            int.TryParse(arg.Parameter, out dataBits);

         arg = arguments.GetArgument("stopbits");
         if(arg.Enabled)
         {
            if(arg.Parameter.ToLower().Equals("1"))
               stopBits = StopBits.One;
            else if(arg.Parameter.ToLower().Equals("1.5"))
               stopBits = StopBits.OnePointFive;
            else if(arg.Parameter.ToLower().Equals("2"))
               stopBits = StopBits.Two;
            else if(arg.Parameter.ToLower().Equals("0"))
               stopBits = StopBits.None;
         }

         SerialPort port = new SerialPort(portName, baudrate, parity, dataBits, stopBits);

#if __MonoCS__
#else
         port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
         port.ErrorReceived += new SerialErrorReceivedEventHandler(port_ErrorReceived);
         port.PinChanged += new SerialPinChangedEventHandler(port_PinChanged);
#endif

         //log option
         arg = arguments.GetArgument("logfile");
         if(arg.Enabled)
         {
            if(arg.Parameter.Length == 0)
            {
               logFilename = "log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +".txt";
               consoleWriteLine("Warning: Log file name not specified. Used " + logFilename);
            }
            else
               logFilename = arg.Parameter;

            logfile = true;
         }
         arg = arguments.GetArgument("logincomingonly");
         if(arg.Enabled)
         {
            if(!logfile)
            {
               logfile = true;
               logFilename = "log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +".txt";
               consoleWriteLine("Warning: Parameter logfile not specified. Log enabled to the file: " + logFilename);
               logfile = true;
            }
            logincomingonly = true;
         }

         //gap argument
         arg = arguments.GetArgument("gaptolerance");
         if(arg.Enabled)
         {
            int.TryParse(arg.Parameter, out gapTolerance);

            if(gapTolerance == 0)
               consoleWriteLine("Warning: Parameter gaptolerance has invalid argument. Gap tolerance must be greater than zero.");
            else
               gapToleranceEnable = true;
         }


         if(logfile)
         {
            //check path
            string path = System.IO.Path.GetDirectoryName(logFilename);
            if(path.Length == 0)
               logFilename = System.IO.Directory.GetCurrentDirectory() + "\\" + logFilename;
            else
            {
               if(!System.IO.Directory.Exists(path))
                  System.IO.Directory.CreateDirectory(path);
            }

            if(!isFileNameValid(logFilename))
            {
               consoleWriteLine("\nPress [Enter] to exit");
               Console.ReadLine();

               return;
            }

            //assign file to listener
            if(Trace.Listeners["Default"] is DefaultTraceListener)
               ((DefaultTraceListener)Trace.Listeners["Default"]).LogFileName = logFilename;
         }

         if(args.Length > 0)
         {
            if(Config.Save(args))
               consoleWriteLine("Program parameters have been saved. Will be used next time you start program.");
            else
               consoleWriteLine("Warning: Program parameters cannot be saved.");
         }

         Argument repeatfile = arguments.GetArgument("repeatfile");
         if(repeatfile.Enabled)
         {
            if(!isFileNameValid(repeatfile.Parameter))
            {
               consoleWriteLine("\nPress [Enter] to exit");
               Console.ReadLine();

               return;
            }
            PrepareRepeatFile(repeatfile.Parameter);
         }

         consoleWriteLine("Opening port {0}: baudrate={1}b/s, parity={2}, databits={3}, stopbits={4}", port.PortName, port.BaudRate.ToString(), port.Parity.ToString(), port.DataBits.ToString(), port.StopBits.ToString());

         if(!continuousMode)
            Cinout.WritePortStatus(port.PortName, false, port.BaudRate);

         portConnectInfinite(port);

         if(port.IsOpen)
         {
            consoleWriteLine("Port {0} opened", portName);
            if(!continuousMode)
            {
               Cinout.WritePortStatus(port.PortName, true, port.BaudRate);
               Cinout.WritePinStatus(port.RtsEnable ? 1 : 0, port.CtsHolding ? 1 : 0, port.DtrEnable ? 1 : 0, port.DsrHolding ? 1 : 0, port.CDHolding ? 1 : 0, port.BreakState ? 1 : 0);
            }
         }
         else
            return;

#if __MonoCS__
         SerialPinChange _pinsStatesNow = 0;
         SerialPinChange _pinsStatesOld = 0;
#endif
         bool exit = false;

         while(!exit)
         {
#if __MonoCS__
            if(port.BytesToRead > 0)
               port_DataReceived(port, null);
            else
            {
               _pinsStatesNow = (SerialPinChange)(Convert.ToInt32(port.CtsHolding) * ((int)SerialPinChange.CtsChanged) 
                  | Convert.ToInt32(port.CDHolding) * ((int)SerialPinChange.CDChanged) 
                  | Convert.ToInt32(port.DsrHolding) * ((int)SerialPinChange.DsrChanged) 
                  | Convert.ToInt32(port.BreakState) * ((int)SerialPinChange.Break)); 

               if(_pinsStatesNow != _pinsStatesOld)
               {
                  SerialPinChange _pinsStatesChange = _pinsStatesNow ^ _pinsStatesOld;

                  port_PinChanged(port, _pinsStatesChange);
                  _pinsStatesOld = _pinsStatesNow;
               }
               Thread.Sleep(100);
            }
#else
            Thread.Sleep(100);
#endif
            Command cmd = consoleReadCommand();
            if(cmd == Command.EXIT)
            {
               port.Close();
               exit = true;
               break;
            }

            switch(cmd)
            {
               case Command.PAUSE:
                  pausePrint = !pausePrint;
                  break;
               case Command.CONNECT:
                  connectCommand(port);
                  break;
               case Command.HELP:
                  printHelp();
                  break;
               case Command.SEND:
                  consoleWriteLine("Type data to send: ");
                  string line = consoleReadLine();
                  consoleWriteLine("Sent: {0}", line);
                  break;
            }

            if(!pauseConnection && !port.IsOpen)
            {
               return;
            }
         }
      }

      /// <summary>
      /// Connecting to port in infinite loop
      /// </summary>
      /// <param name="port"></param>
      private static void portConnectInfinite(SerialPort port)
      {
         do
         {
            if(!portConnect(port))
            {
               string waitText = "Waiting 5s to reconnect...";
               consoleWrite(waitText);

               for(int i=0;i<5;i++)
               {
                  for(int j=0;j<4;j++)
                  {
                     Thread.Sleep(250);

                     consoleWrite(j==0 ? "/" : j==1 ? "-" : j==2 ? "\\" : "|");
                     consoleCursorLeft(-1);
                  }

                  consoleWrite(".");
               }

               consoleCursorLeftReset();
               consoleWrite(new string(' ', waitText.Length + 5));
               consoleCursorLeftReset();
            }

            Command cmd = consoleReadCommand();
            if(cmd == Command.EXIT)
            {
               port.Close();
               break;
            }

         }
         while(!port.IsOpen);
      }

      /// <summary>
      /// Connecting to port
      /// </summary>
      /// <param name="port"></param>
      /// <returns></returns>
      private static bool portConnect(SerialPort port)
      {
         try
         {
            port.Open();
         }
         catch(System.IO.IOException ex)
         {
            consoleWriteError("Cannot open port " + port.PortName + ". " + ex.Message);
            consoleWrite("  Available ports: ");
            consoleWriteLine(string.Join(",", SerialPort.GetPortNames()));

            return false;
         }
         catch(Exception ex)
         {
            consoleWriteError("Cannot open port " + port.PortName + ". " + ex.Message);

            return false;
         }

         return true;
      }

      /// <summary>
      /// Close port
      /// </summary>
      /// <param name="port"></param>
      /// <returns></returns>
      private static void portClose(SerialPort port)
      {
         if(port.IsOpen)
            port.Close();
      }

      /// <summary>
      /// Provide connect (pause/resume) command
      /// </summary>
      private static void connectCommand(SerialPort port)
      {
         pauseConnection = !pauseConnection;

         if(pauseConnection)
         {
            consoleWriteLine(" Connection paused. Port closed.");
            port.Close();

            if(!continuousMode)
               Cinout.WritePortStatus(port.PortName, false, port.BaudRate);
         }
         else
         {
            consoleWriteLine(" Resuming connection...");

            portConnectInfinite(port);

            if(port.IsOpen)
            {
               consoleWriteLine(" Connection resumed");

               if(!continuousMode)
               {
                  Cinout.WritePortStatus(port.PortName, true, port.BaudRate);
                  Cinout.WritePinStatus(port.RtsEnable ? 1 : 0, port.CtsHolding ? 1 : 0, port.DtrEnable ? 1 : 0, port.DsrHolding ? 1 : 0, port.CDHolding ? 1 : 0, port.BreakState ? 1 : 0);
               }
            }
         }
      }

      /// <summary>
      /// Validating file name(path)
      /// </summary>
      /// <param name="filePath"></param>
      /// <returns></returns>
      private static bool isFileNameValid(string filePath)
      {
         foreach(char c in System.IO.Path.GetInvalidPathChars())
         {
            if(filePath.Contains(c.ToString()))
            {
               consoleWriteError("File name {0} contains invalid character [{1}]. Enter right file name.", filePath, c);

               return false;
            }
         }

         foreach(char c in System.IO.Path.GetInvalidFileNameChars())
         {
            if(System.IO.Path.GetFileName(filePath).Contains(c.ToString()))
            {
               consoleWriteError("File name {0} contains invalid character [{1}]. Enter right file name.", filePath, c);

               return false;
            }
         }

         return true;
      }

      /// <summary>
      /// Procedure to read data from repeat file
      /// </summary>
      /// <param name="fileName"></param>
      private static void PrepareRepeatFile(string fileName)
      {
         if(!File.Exists(fileName))
            consoleWriteError("File {0} was not found", fileName);
         else
         {
            try
            {
               string[] lines = File.ReadAllLines(fileName);

               if(lines.Length == 0)
                  consoleWriteError("Zero lines in file {0}", fileName);

               consoleWriteLine("File {0} opened and {1} lines has been read", fileName, lines.Length);

               repeaterMap = new Dictionary<string, string>();

               //check format file
               string startLine = lines[0];
               bool hex;
               int linesWithData = 0;
               string ask = "";

               Regex reg = new Regex("^(0x[0-9A-Fa-f]{1,2}\\s*)+$");

               if(reg.IsMatch(startLine))
               {
                  hex = true;
                  consoleWriteLine("First line corresponds hex format. File will be read and packets compared as HEX.");

                  Regex regWhite = new Regex("\\s+");

                  //check whole file
                  for(int i = 0;i < lines.Length;i++)
                  {
                     if(lines[i].Trim().Length > 0)
                     {
                        if(reg.IsMatch(lines[i]))
                        {
                           if(++linesWithData % 2 == 1)
                              ask = regWhite.Replace(lines[i].Replace("0x", ""), "");
                           else
                              repeaterMap.Add(ask, regWhite.Replace(lines[i].Replace("0x", ""), ""));
                        }
                        else
                        {
                           repeaterMap = null;
                           throw new RepeatFileException("Line {0} not coresponds to hex format.", i);
                        }
                     }
                  }

                  repeaterUseHex = true;
               }
               else
               {
                  reg = new Regex("^([0-9A-Fa-f])+$");

                  if(reg.IsMatch(startLine))
                  {
                     hex = true;
                     consoleWriteLine("First line corresponds hex format. File will be read and packets compared as HEX.");

                     //check whole file
                     for(int i = 0;i < lines.Length;i++)
                     {
                        if(lines[i].Trim().Length > 0)
                        {
                           if(lines[i].Length % 2 == 1)
                           {
                              repeaterMap = null;
                              throw new RepeatFileException("Line {0} has odd number of characters.", i);
                           }

                           if(reg.IsMatch(lines[i]))
                           {
                              if(++linesWithData % 2 == 1)
                                 ask = lines[i];
                              else
                                 repeaterMap.Add(ask, lines[i]);
                           }
                           else
                           {
                              repeaterMap = null;
                              throw new RepeatFileException("Line {0} not coresponds to hex format.", i);
                           }
                        }
                     }

                     repeaterUseHex = true;
                  }
                  else
                  {
                     hex = false;
                     consoleWriteLine("First line not corresponds hex format. File will be read and packets compared as ASCII.");

                     //check whole file
                     for(int i = 0;i < lines.Length;i++)
                     {
                        if(lines[i].Trim().Length > 0)
                        {
                           if(++linesWithData % 2 == 1)
                              ask = lines[i];
                           else
                              repeaterMap.Add(ask, lines[i]);
                        }
                     }
                  }
               }

               if(linesWithData % 2 == 1)
                  consoleWriteError("Odd number of lines in file {0} with code. One line ask, one line answer.", fileName);

               repeaterEnabled = true;

               consoleWriteLine("{0} pairs ask/answer ready", repeaterMap.Count);
            }
            catch(Exception ex)
            {
               consoleWriteError("Cannot read file {0}", fileName);
               consoleWriteError(ex.ToString());
            }
         }
      }


#if __MonoCS__  
      /// <summary>
      /// Event on serial port "pin was changed"
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="pinsStatesChange"></param>    
      static void port_PinChanged(object sender, SerialPinChange pinsStatesChange)
      {
#else
      /// <summary>
      /// Event on serial port "pin was changed"
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      static void port_PinChanged(object sender, SerialPinChangedEventArgs e)
      {
         SerialPinChange pinsStatesChange = e.EventType;
#endif
         SerialPort port = ((SerialPort)sender);

         if(continuousMode)
         {
            Console.ForegroundColor = ConsoleColor.Cyan;

            //TODO: change enumerator SerialPinChange printing???
            consoleWriteLine("Pin {0} changed", pinsStatesChange.ToString());

            writePinState("RTS", port.RtsEnable);
            writePinState("CTS", port.CtsHolding);
            writePinState("DTR", port.DtrEnable);
            writePinState("DSR", port.DsrHolding);
            writePinState("CD", port.CDHolding);
            writePinState("Break", port.BreakState);

            consoleWrite("\n");

            Console.ResetColor();
         }
         else
            Cinout.WritePinStatus(port.RtsEnable ? 1 : 0, port.CtsHolding ? 1 : 0, port.DtrEnable ? 1 : 0, port.DsrHolding ? 1 : 0, port.CDHolding ? 1 : 0, port.BreakState ? 1 : 0);
      }

      /// <summary>
      /// Print serialport pin state
      /// </summary>
      /// <param name="name"></param>
      /// <param name="state"></param>
      static void writePinState(string name, bool state)
      {
         Console.ForegroundColor = state ? ConsoleColor.Green : ConsoleColor.White;

         consoleWrite("{0}({1})  ", name, state ? "1" : "0");
      }

      /// <summary>
      /// Serial port OnError event
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      static void port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
      {
         consoleWriteError(e.EventType.ToString());
      }

      /// <summary>
      /// Serial port event when new data arrived
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      static void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
      {
         SerialPort port = ((SerialPort)sender);
         byte[] incoming;

         try
         {
            incoming = new byte[port.BytesToRead];

            port.Read(incoming, 0, incoming.Length);
         }
         catch(Exception ex)
         {
            consoleWriteError(ex.ToString());
            return;
         }

         TimeSpan time = DateTime.Now.TimeOfDay;
         bool applyGapTolerance = false;

         //print time since last receive only if not disabled
         if(lastTimeReceved > 0)
         {
            double sinceLastReceive = ((double)(time.Ticks - lastTimeReceved) / 10000);
            applyGapTolerance = (gapToleranceEnable && sinceLastReceive <= gapTolerance);

            if(!noTime && (!gapToleranceEnable || !applyGapTolerance))
               consoleWriteCommunication(ConsoleColor.Magenta, "\n+" + sinceLastReceive.ToString("F3") + " ms");
         }

         //Write to output
         string line = "";

         if(showAscii)
         {
            if(noTime || applyGapTolerance)
               line = ASCIIEncoding.ASCII.GetString(incoming);
            else
               line = time.ToString() + " " + ASCIIEncoding.ASCII.GetString(incoming);
         }
         else
         {
            if(noTime || applyGapTolerance)
               line = string.Join(" ", Array.ConvertAll(incoming, x => "0x" + x.ToString("X2")));
            else
               line = time.ToString() + " " + string.Join(" ", Array.ConvertAll(incoming, x => "0x" + x.ToString("X2")));
         }

         if(applyGapTolerance)
            consoleWriteCommunication(ConsoleColor.Yellow, line);
         else
         {
            consoleWriteCommunication(ConsoleColor.Yellow, "\n");
            consoleWriteCommunication(ConsoleColor.Yellow, line);
         }


         lastTimeReceved = time.Ticks;

         if(repeaterEnabled)
         {
            byte[] data = data2Send(incoming);

            if(data != null)
            {
               try
               {
                  port.Write(data, 0, data.Length);
               }
               catch(Exception ex)
               {
                  consoleWriteError(ex.ToString());
               }
            }
         }
      }

      /// <summary>
      /// prepare data as answer
      /// </summary>
      /// <param name="incoming"></param>
      /// <returns></returns>
      static byte[] data2Send(byte[] incoming)
      {
         if(repeaterUseHex)
         {
            string ask = string.Join("", Array.ConvertAll(incoming, x => x.ToString("X2")));

            if(repeaterMap.ContainsKey(ask))
            {
               string answer = repeaterMap[ask];
               byte[] data = new byte[answer.Length / 2];

               for(int i = 0;i < data.Length;i++)
               {
                  data[i] = Convert.ToByte(answer.Substring(2 * i, 2), 16);
               }

               if(noTime)
                  consoleWriteCommunication(ConsoleColor.Green, string.Join("\n ", Array.ConvertAll(data, x => "0x" + x.ToString("X2"))));
               else
                  consoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + string.Join(" ", Array.ConvertAll(data, x => "0x" + x.ToString("X2"))));

               return data;
            }
            else
            {
               consoleWriteLine("Repeater: Unknown ask");
            }
         }
         else
         {
            string ask = ASCIIEncoding.ASCII.GetString(incoming);

            if(repeaterMap.ContainsKey(ask))
            {
               string answer = repeaterMap[ask];

               if(noTime)
                  consoleWriteCommunication(ConsoleColor.Green, "\n" + answer);
               else
                  consoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + answer);

               return ASCIIEncoding.ASCII.GetBytes(answer);
            }
            else
            {
               consoleWriteLine("Repeater: Unknown ask");
            }
         }

         return null;
      }

      /// <summary>
      /// Print error
      /// </summary>
      /// <param name="text"></param>
      /// <param name="arg"></param>
      private static void consoleWriteError(string text, params object[] arg)
      {
         consoleWriteLine(ConsoleColor.Red, text, arg);
      }

      /// <summary>
      /// Print
      /// </summary>
      /// <param name="message"></param>
      /// <param name="parameters"></param>
      private static void consoleWrite(string message, params object[] parameters)
      {
         if(!continuousMode)
            Cinout.Write(message, parameters);
         else
            Console.Write(message, parameters);

         if(logfile && !logincomingonly)
            Trace.Write(string.Format(message, parameters));
      }

      /// <summary>
      /// Print single line
      /// </summary>
      /// <param name="message"></param>
      /// <param name="parameters"></param>
      private static void consoleWriteLine(string message, params object[] parameters)
      {
         if(!continuousMode)
            Cinout.WriteLine(message, parameters);
         else
            Console.WriteLine(message, parameters);

         if(logfile && !logincomingonly)
            Trace.WriteLine(string.Format(message, parameters));
      }

      private static void consoleWriteLine(ConsoleColor color, string message, params object[] parameters)
      {
         if(!continuousMode)
            Cinout.WriteLine(color, message, parameters);
         else
         {
            Console.ForegroundColor = color;
            Console.WriteLine(message, parameters);
            Console.ResetColor();
         }

         if(logfile && !logincomingonly)
            Trace.WriteLine(string.Format(message, parameters));
      }

      /// <summary>
      /// Print single line without trace log
      /// </summary>
      /// <param name="message"></param>
      /// <param name="parameters"></param>
      private static void consoleWriteLineNoTrace(string message, params object[] parameters)
      {
         if(!continuousMode)
            Cinout.WriteLine(message, parameters);
         else
            Console.WriteLine(message, parameters);
      }

      /// <summary>
      /// Print line that is involved in communication
      /// </summary>
      /// <param name="message"></param>
      /// <param name="parameters"></param>
      private static void consoleWriteCommunication(ConsoleColor color, string message, params object[] parameters)
      {
         if(!pausePrint)
         {
            if(!continuousMode)
               Cinout.Write(color, message, parameters);
            else
            {
               Console.ForegroundColor = color;
               consoleWrite(message, parameters);
               Console.ResetColor();
            }
         }

         if(logfile)
            Trace.Write(string.Format(message, parameters));
      }

      /// <summary>
      /// Print line that is involved in communication
      /// </summary>
      /// <param name="message"></param>
      /// <param name="parameters"></param>
      private static void consoleWriteLineCommunication(string message, params object[] parameters)
      {
         if(!pausePrint)
         {
            if(!continuousMode)
               Cinout.WriteLine(message, parameters);
            else
               consoleWriteLine(message, parameters);
         }

         if(logfile)
            Trace.WriteLine(string.Format(message, parameters));
      }

      private static void consoleCursorLeft(int moveBy)
      {
         if(continuousMode)
            Console.CursorLeft += moveBy;
         else
            Cinout.CursorLeft(moveBy);
      }

      private static void consoleCursorLeftReset()
      {
         if(continuousMode)
            Console.CursorLeft = 0;
         else
            Cinout.CursorLeftReset();
      }

      /// <summary>
      /// Print help
      /// </summary>
      private static void printHelp()
      {
         consoleWriteLineNoTrace("");
         consoleWriteLineNoTrace("Usage: serialmonitor PortName [<switch> parameter]");
         consoleWriteLineNoTrace("");
         consoleWriteLineNoTrace("Switches:");
         consoleWriteLineNoTrace("-baudrate {baud rate}: set port baud rate. Default 9600kbps.");
         consoleWriteLineNoTrace("-parity {used parity}: set used port parity. Default none. Available parameters odd, even, mark and space.");
         consoleWriteLineNoTrace("-databits {used databits}: set data bits count. Default 8 data bits.");
         consoleWriteLineNoTrace("-stopbits {used stopbits}: set stop bits count. Default 1 stop bit. Available parameters 0, 1, 1.5 and 2.");
         consoleWriteLineNoTrace("-repeatfile {file name}: enable repeat mode with protocol specified in file");
         consoleWriteLineNoTrace("-logfile {file name}: set file name for log into that file");
         consoleWriteLineNoTrace("-logincomingonly: log into file would be only incoming data");
         consoleWriteLineNoTrace("-showascii: communication would be show in ASCII format (otherwise HEX is used)");
         consoleWriteLineNoTrace("-notime: time information about incoming data would not be printed");
         consoleWriteLineNoTrace("-gaptolerance {time gap in ms}: messages received within specified time gap will be printed together");

         consoleWriteLineNoTrace("");
         consoleWriteLineNoTrace("Example: serialmonitor COM1");
         consoleWriteLineNoTrace("         serialmonitor COM1 -baudrate 57600 -parity odd -databits 7 -stopbits 1.5");
         consoleWriteLineNoTrace("         serialmonitor COM83 -baudrate 19200 -repeatfile protocol.txt");

         consoleWriteLineNoTrace("");
         consoleWriteLineNoTrace("In program commands:");
         consoleWriteLineNoTrace("F1: print help");
         consoleWriteLineNoTrace("F2: pause/resume print on screen");
         consoleWriteLineNoTrace("F4: pause/resume connection to serial port");
         consoleWriteLineNoTrace("F5 {data to send}: send specified data (in HEX format if data start with 0x otherwise ASCII is send)");
         consoleWriteLineNoTrace("F10 or ^C: program exit");

         consoleWriteLineNoTrace("");
         consoleWriteLineNoTrace("In program color schema:");
         Console.ForegroundColor = ConsoleColor.Cyan;
         consoleWriteLineNoTrace("Control pin status changed");
         Console.ForegroundColor = ConsoleColor.Green;
         consoleWriteLineNoTrace("Control pin ON");
         Console.ForegroundColor = ConsoleColor.White;
         consoleWriteLineNoTrace("Control pin OFF");
         Console.ForegroundColor = ConsoleColor.Magenta;
         consoleWriteLineNoTrace("Time between received data");
         Console.ForegroundColor = ConsoleColor.Yellow;
         consoleWriteLineNoTrace("Received data");
         Console.ForegroundColor = ConsoleColor.Green;
         consoleWriteLineNoTrace("Sended data");
         Console.ForegroundColor = ConsoleColor.Red;
         consoleWriteLineNoTrace("Error");

         Console.ResetColor();
      }

      /// <summary>
      /// Check Mono runtime
      /// </summary>
      /// <returns></returns>
      public static bool IsRunningOnMono()
      {
         return Type.GetType("Mono.Runtime") != null;
      }

      enum Command
      {
         NONE,
         EXIT,
         PAUSE,
         HELP,
         SEND,
         CONNECT
      };

      /// <summary>
      /// 
      /// </summary>
      /// <returns></returns>
      private static Command consoleReadCommand()
      {
         if(Console.KeyAvailable)
         {
            ConsoleKeyInfo k = Console.ReadKey();

            //command keys
            if(k.Key == ConsoleKey.F1)
               return Command.HELP;
            else if(k.Key == ConsoleKey.F2)
               return Command.PAUSE;
            else if(k.Key == ConsoleKey.F4)
               return Command.CONNECT;
            else if(k.Key == ConsoleKey.F5)
               return Command.SEND;
            else if(k.Key == ConsoleKey.F10)
               return Command.EXIT;
            else if(k.KeyChar != 0)
               inBuffer.Append(k.KeyChar);

            //byte[] data = null;
            //string sendTest = line.Substring(5).TrimStart(' ');


            //if(sendTest.StartsWith("0x"))
            //{
            //   Regex regWhite = new Regex("\\s+");

            //   sendTest = regWhite.Replace(sendTest.Replace("0x", ""), "");
            //   data = new byte[sendTest.Length / 2];

            //   for(int i = 0;i < data.Length;i++)
            //   {
            //      data[i] = Convert.ToByte(sendTest.Substring(2 * i, 2), 16);
            //   }
            //}
            //else
            //{
            //   data = ASCIIEncoding.ASCII.GetBytes(sendTest);
            //}

            //if(data != null)
            //{
            //   port.Write(data, 0, data.Length);
            //}
         }

         return Command.NONE;
      }


      private static string consoleReadLine()
      {
         ConsoleKeyInfo k = Console.ReadKey();

         while(k.Key != ConsoleKey.Enter)
         {
            if(k.KeyChar != 0)
               inBuffer.Append(k.KeyChar);

            k = Console.ReadKey();
         }

         string line = inBuffer.ToString();
         inBuffer.Remove(0, inBuffer.Length);

         return line;
      }
   }
}
