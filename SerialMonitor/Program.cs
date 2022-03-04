//---------------------------------------------------------------------------
//
// Name:        Program.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Main program
//
//---------------------------------------------------------------------------

using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace SerialMonitor
{
    class Program
    {
        static readonly ArgumentCollection arguments = new ArgumentCollection(new string[] { "baudrate", "parity", "databits", "stopbits",
         "repeatfile", "logfile", "logincomingonly", "showascii", "notime", "gaptolerance", "continuousmode", "nogui" });
        static readonly string? version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
        static long lastTimeReceved = 0;
        static bool repeaterEnabled = false;
        static bool repeaterUseHex = false;
        static bool showAscii = false;
        static readonly Dictionary<string, string> repeaterMap = new Dictionary<string, string>();
        static bool logfile = false;
        static bool logincomingonly = false;
        static string logFilename = "";
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
        /// <summary>
        /// Incoming data buffer
        /// </summary>
        static byte[] incoming = new byte[32];
        /// <summary>
        /// Setting data
        /// </summary>
        static readonly Setting setting = new Setting();
        /// <summary>
        /// Port instance
        /// </summary>
        static readonly SerialPort port = new SerialPort();

        /// <summary>
        /// Main loop
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            DateTime lastTry = DateTime.MinValue;
            setting.Port = System.OperatingSystem.IsWindows() ? "COM1" : "/dev/ttyS1";

            if (args.Length > 0)
            {
                if (args[0].Equals("-?") || args[0].Equals("-help") || args[0].Equals("--help") || args[0].Equals("?") || args[0].Equals("/?"))
                {
                    ConsoleWriteLineNoTrace($"SerialMonitor v.{version}");
                    PrintHelp();
                    Console.WriteLine("\nPress [Enter] to exit");
                    Console.ReadLine();
                    return;
                }
                else
                    setting.Port = args[0];
            }
            else
            {
                string[]? savedArgs = Config.LoadStarters();

                if (savedArgs != null)
                {
                    setting.Port = savedArgs[0];
                    args = savedArgs;
                }
                else
                {
                    string? value = Config.LoadSetting(Config.SETTING_PORT);
                    if (!string.IsNullOrEmpty(value))
                        setting.Port = value;
                }

            }
            // parse commandline arguments
            arguments.Parse(args);

            continuousMode = arguments.GetArgument("continuousmode").Enabled || arguments.GetArgument("nogui").Enabled;

            if (!continuousMode)
                UI.Init();
            else
                ConsoleWriteLineNoTrace($"SerialMonitor v.{version}");
                        
            setting.BaudRate = 9600;
            setting.Parity = Parity.None;
            setting.DataBits = 8;
            setting.StopBits = StopBits.One;

            Argument arg = arguments.GetArgument("baudrate");
            if (arg.Enabled)
            {
                int.TryParse(arg.Parameter, out setting.BaudRate);
            }
            else
            {
                string? value = Config.LoadSetting(Config.SETTING_BAUDRATE);
                if (!string.IsNullOrEmpty(value))
                    int.TryParse(value, out setting.BaudRate);
            }

            arg = arguments.GetArgument("parity");
            if (arg.Enabled)
            {
                if (arg.Parameter.ToLower().Equals("odd"))
                    setting.Parity = Parity.Odd;
                else if (arg.Parameter.ToLower().Equals("even"))
                    setting.Parity = Parity.Even;
                else if (arg.Parameter.ToLower().Equals("mark"))
                    setting.Parity = Parity.Mark;
                else if (arg.Parameter.ToLower().Equals("space"))
                    setting.Parity = Parity.Space;
            }

            arg = arguments.GetArgument("databits");
            if (arg.Enabled)
                int.TryParse(arg.Parameter, out setting.DataBits);

            arg = arguments.GetArgument("stopbits");
            if (arg.Enabled)
            {
                if (arg.Parameter.ToLower().Equals("1"))
                    setting.StopBits = StopBits.One;
                else if (arg.Parameter.ToLower().Equals("1.5"))
                    setting.StopBits = StopBits.OnePointFive;
                else if (arg.Parameter.ToLower().Equals("2"))
                    setting.StopBits = StopBits.Two;
                else if (arg.Parameter.ToLower().Equals("0"))
                    setting.StopBits = StopBits.None;
            }

            port.PortName = setting.Port;
            port.BaudRate = setting.BaudRate;
            port.Parity   = setting.Parity;
            port.DataBits = setting.DataBits;
            port.StopBits = setting.StopBits;
            port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
            port.ErrorReceived += new SerialErrorReceivedEventHandler(port_ErrorReceived);
            port.PinChanged += new SerialPinChangedEventHandler(port_PinChanged);

            arg = arguments.GetArgument("notime");
            if (arg.Enabled)
                setting.ShowTime = false;
            else
                setting.ShowTime = Config.LoadSetting(Config.SETTING_SHOWTIME) != "0";

            showAscii = arguments.GetArgument("showascii").Enabled;            

            //log option
            arg = arguments.GetArgument("logfile");
            if (arg.Enabled)
            {
                if (arg.Parameter.Length == 0)
                {
                    logFilename = "log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
                    ConsoleWriteLine("Warning: Log file name not specified. Used " + logFilename);
                }
                else
                    logFilename = arg.Parameter;

                logfile = true;
            }
            arg = arguments.GetArgument("logincomingonly");
            if (arg.Enabled)
            {
                if (!logfile)
                {
                    logfile = true;
                    logFilename = "log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
                    ConsoleWriteLine("Warning: Parameter logfile not specified. Log enabled to the file: " + logFilename);
                    logfile = true;
                }
                logincomingonly = true;
            }

            //gap argument
            arg = arguments.GetArgument("gaptolerance");
            if (arg.Enabled)
            {
                _ = int.TryParse(arg.Parameter, out gapTolerance);

                if (gapTolerance == 0)
                    ConsoleWriteLine("Warning: Parameter gaptolerance has invalid argument. Gap tolerance must be greater than zero.");
                else
                    gapToleranceEnable = true;
            }


            if (logfile)
            {
                //check path
                string? path = System.IO.Path.GetDirectoryName(logFilename);
                if (path?.Length > 0)
                {
                    if (!System.IO.Directory.Exists(path))
                        try
                        {
                            System.IO.Directory.CreateDirectory(path);
                        }
                        catch (Exception ex)
                        {
                            ConsoleWriteLine($"Warning: Cannot create directory {path}. {ex.Message}");
                        }
                }
                else
                {
                    logFilename = System.IO.Directory.GetCurrentDirectory() + "\\" + logFilename;
                }                

                if (!IsFileNameValid(logFilename))
                {
                    ConsoleWriteLine("\nPress [Enter] to exit");
                    Console.ReadLine();

                    return;
                }

                //assign file to listener
                if (Trace.Listeners["Default"] is DefaultTraceListener listener)
                    listener.LogFileName = logFilename;
            }

            if (args.Length > 0)
            {
                if (Config.SaveStarters(args))
                    ConsoleWriteLine("Program parameters have been saved. Will be used next time you start program.");
                else
                    ConsoleWriteLine("Warning: Program parameters cannot be saved.");
            }

            Argument repeatfile = arguments.GetArgument("repeatfile");
            if (repeatfile.Enabled)
            {
                if (!IsFileNameValid(repeatfile.Parameter))
                {
                    ConsoleWriteLine("\nPress [Enter] to exit");
                    Console.ReadLine();

                    return;
                }
                PrepareRepeatFile(repeatfile.Parameter);
            }

            ConsoleWriteLine($"Opening port {port.PortName}: baudrate={port.BaudRate}b/s, parity={port.Parity}, databits={port.DataBits}, stopbits={port.StopBits}");

            bool exit = false;

            string[]? history = Config.LoadHistory();

            if (history?.Length > 0)
            {
                UI.CommandHistory.AddRange(history);
                history = null;
            }

            string[]? fileList = Config.LoadFileList();

            if (fileList?.Length > 0)
            {
                UI.FileHistory.AddRange(fileList);
                fileList = null;
            }

            if (continuousMode)
            {
                while (!exit)
                {
                    Thread.Sleep(100);
                    if (!pauseConnection && !port.IsOpen)
                    {
                        ConsoleWriteLine(" Port disconnected....");
                        PortConnectInfinite(port);
                    }
                }
            }
            else
            {                
                setting.ShowTimeGap = Config.LoadSetting(Config.SETTING_SHOWTIMEGAP) == "1";
                setting.ShowSentData = Config.LoadSetting(Config.SETTING_SHOWSENTDATA) == "1";                
                UI.ActionHelp = () => { PrintHelp(); };
                UI.ActionPrint = (print) => { pausePrint = !print; };
                UI.ActionPrintAsHex = (hex) => { showAscii = !hex; };
                UI.ActionOpenClose = (close) => { pauseConnection = close; if (close) port.Close(); UI.SetPortStatus(port); };
                UI.ActionSend = (data) => { UserDataSend(port,data); };
                UI.ActionSendFile = (file) => { UserDataSendFile(port, file); };
                UI.ActionRts = () => { port.RtsEnable = !port.RtsEnable; UI.SetPortStatus(port); };
                UI.ActionDtr = () => { port.DtrEnable = !port.DtrEnable; UI.SetPortStatus(port); };
                UI.ActionCommand = (text) => { ProcessCommand(text); };
                UI.ActionSettingLoad = () => { return setting; };
                UI.ActionSettingSave = (setting) => { 
                    if(port.PortName != setting.Port || port.BaudRate != setting.BaudRate)
                    {
                        if (port.IsOpen)
                        {
                            pauseConnection = true;
                            port.Close();
                            port.PortName = setting.Port;
                            port.BaudRate = setting.BaudRate;
                            pauseConnection = false;
                            port.Open();
                        }
                        else
                        {
                            port.PortName = setting.Port;
                            port.BaudRate = setting.BaudRate;
                        }
                        UI.SetPortStatus(port);                        
                    }
                    
                    return Config.SaveSetting(setting.Port, setting.BaudRate, setting.ShowTime, setting.ShowTimeGap, setting.ShowSentData);
                };

                UI.SetPortStatus(port);
                UI.Run((loop) =>
                {
                    if (!port.IsOpen && !pauseConnection)
                    {
                        if (lastTry.AddSeconds(5) <= DateTime.Now)
                        {
                            lastTry = DateTime.Now;
                            if (PortConnect(port))
                            {
                                UI.SetPortStatus(port);
                                UI.SetPinStatus(port);
                            }
                        }
                    }
                    return true;
                });

                Exit();
            }
        }

        /// <summary>
        /// Process command from command line
        /// </summary>
        /// <param name="text"></param>
        private static void ProcessCommand(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (text.Equals("help"))
                PrintHelp();
            else if (text.StartsWith("send "))
                UserDataSend(port, text.Substring(5));
            else
                ConsoleWriteLineNoTrace($"Unknown command: {text}");
        }

        /// <summary>
        /// Send tada typed by user
        /// </summary>
        /// <param name="port"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        private static bool UserDataSend(SerialPort port, string? line)
        {
            if (string.IsNullOrEmpty(line))
            {
                ConsoleWriteError("Nothing to sent.");
                return false;
            }

            bool hex = false;
            byte[] data;

            if (line.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                hex = true;

            if (hex)
            {
                string prepared = line.Replace("0x", "");
                Regex reg = new Regex("^([0-9A-Fa-f]{1,2}\\s*)+$");

                if (!reg.IsMatch(prepared))
                {
                    ConsoleWriteError("Message is not well formated. Data will be not sent.");
                    return false;
                }

                string[] parts = prepared.Split(' ');
                string[] partsReady = new string[parts.Length];
                int recordsLength = 0, bytesLength = 0;

                foreach (string s in parts)
                {
                    if (s.Length > 0)
                    {
                        int bytes = s.Length / 2;

                        if ((s.Length % 2) == 1)
                        {
                            partsReady[recordsLength++] = "0" + s;

                            bytesLength += bytes + 1;
                        }
                        else
                        {
                            partsReady[recordsLength++] = s;

                            bytesLength += bytes;
                        }
                    }
                }

                data = new byte[bytesLength];

                bytesLength = 0;

                for (int i = 0; i < recordsLength; i++)
                {
                    for (int j = 0; j < partsReady[i].Length; j = j + 2)
                    {
                        data[bytesLength++] = Convert.ToByte(partsReady[i].Substring(j, 2), 16);
                    }
                }
            }
            else
            {
                data = ASCIIEncoding.ASCII.GetBytes(line);
            }


            if (data != null)
            {
                try
                {
                    port.Write(data, 0, data.Length);

                    if (setting.ShowSentData)
                    {
                        if (!setting.ShowTime)
                            ConsoleWriteCommunication(ConsoleColor.Green, "\n" + line);
                        else
                            ConsoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + line);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWriteError(ex.ToString());

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static bool UserDataSendFile(SerialPort port, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                ConsoleWriteError("Nothing to sent.");
                return false;
            }

            try
            {
                var data = File.ReadAllBytes(filePath);
                port.Write(data, 0, data.Length);

                ConsoleWriteLineNoTrace(ConsoleColor.White, "File sent.");
            }
            catch (Exception ex)
            {
                ConsoleWriteError(ex.ToString());

                return false;
            }
            return true;
        }

        /// <summary>
        /// Connecting to port in infinite loop
        /// </summary>
        /// <param name="port"></param>
        private static void PortConnectInfinite(SerialPort port)
        {
            do
            {
                if (!PortConnect(port))
                {
                    string waitText = "Waiting 5s to reconnect...";
                    ConsoleWrite(waitText);

                    for (int i = 0; i < 5; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            Thread.Sleep(250);

                            ConsoleWrite(j == 0 ? "/" : j == 1 ? "-" : j == 2 ? "\\" : "|");
                            ConsoleCursorLeft(-1);
                        }

                        ConsoleWrite(".");
                    }

                    ConsoleCursorLeftReset();
                    ConsoleWrite(new string(' ', waitText.Length + 5));
                    ConsoleCursorLeftReset();
                }

                // TODO: 
                /*
                CommandEnum cmd = UI.ConsoleReadCommand(!continuousMode);
                if (cmd == CommandEnum.EXIT)
                {
                    port.Close();
                    break;
                }
                */
            }
            while (!port.IsOpen);
        }

        /// <summary>
        /// Connecting to port
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private static bool PortConnect(SerialPort port)
        {
            try
            {
                port.Open();
            }
            catch (IOException ex)
            {
                ConsoleWriteError("Cannot open " + port.PortName + ". " + ex.Message);
                ConsoleWriteLine("  Available ports: " + string.Join(",", SerialPort.GetPortNames()));

                return false;
            }
            catch (Exception ex)
            {
                ConsoleWriteError("Cannot open port " + port.PortName + ". " + ex.Message);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Close port
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private static void PortClose(SerialPort port)
        {
            if (port.IsOpen)
                port.Close();
        }
        
        /// <summary>
        /// Validating file name(path)
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static bool IsFileNameValid(string filePath)
        {
            foreach (char c in System.IO.Path.GetInvalidPathChars())
            {
                if (filePath.Contains(c.ToString()))
                {
                    ConsoleWriteError($"File name {filePath} contains invalid character [{c}]. Enter right file name.");

                    return false;
                }
            }

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                if (System.IO.Path.GetFileName(filePath).Contains(c.ToString()))
                {
                    ConsoleWriteError($"File name {filePath} contains invalid character [{c}]. Enter right file name.");

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
            if (!File.Exists(fileName))
                ConsoleWriteError($"File {fileName} was not found");
            else
            {
                try
                {
                    string[] lines = File.ReadAllLines(fileName);

                    if (lines.Length == 0)
                        ConsoleWriteError($"Zero lines in file {fileName}");

                    ConsoleWriteLine($"File {fileName} opened and {lines.Length} lines has been read");

                    repeaterMap.Clear();

                    //check format file
                    string startLine = lines[0];
                    int linesWithData = 0;
                    string ask = "";

                    Regex reg = new Regex("^(0x[0-9A-Fa-f]{1,2}\\s*)+$");
                    // match hex string
                    if (reg.IsMatch(startLine))
                    {
                        ConsoleWriteLine("First line corresponds hex format. File will be read and packets compared as HEX.");

                        Regex regWhite = new Regex("\\s+");

                        //check whole file
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Trim().Length > 0)
                            {
                                if (reg.IsMatch(lines[i]))
                                {
                                    if (++linesWithData % 2 == 1)
                                        ask = regWhite.Replace(lines[i].Replace("0x", ""), "");
                                    else
                                        repeaterMap.Add(ask, regWhite.Replace(lines[i].Replace("0x", ""), ""));
                                }
                                else
                                {
                                    throw new RepeatFileException("Line {0} not coresponds to hex format.", i);
                                }
                            }
                        }

                        repeaterUseHex = true;
                    }
                    else
                    {
                        reg = new Regex("^([0-9A-Fa-f])+$");
                        // match hex string
                        if (reg.IsMatch(startLine))
                        {                            
                            ConsoleWriteLine("First line corresponds hex format. File will be read and packets compared as HEX.");

                            //check whole file
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].Trim().Length > 0)
                                {
                                    if (lines[i].Length % 2 == 1)
                                    {
                                        throw new RepeatFileException("Line {0} has odd number of characters.", i);
                                    }

                                    if (reg.IsMatch(lines[i]))
                                    {
                                        if (++linesWithData % 2 == 1)
                                            ask = lines[i];
                                        else
                                            repeaterMap.Add(ask, lines[i]);
                                    }
                                    else
                                    {
                                        throw new RepeatFileException("Line {0} not coresponds to hex format.", i);
                                    }
                                }
                            }

                            repeaterUseHex = true;
                        }
                        else
                        {
                            // non hex string
                            ConsoleWriteLine("First line not corresponds hex format. File will be read and packets compared as ASCII.");

                            //check whole file
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].Trim().Length > 0)
                                {
                                    if (++linesWithData % 2 == 1)
                                        ask = lines[i];
                                    else
                                        repeaterMap.Add(ask, lines[i]);
                                }
                            }
                        }
                    }

                    if (linesWithData % 2 == 1)
                        ConsoleWriteError($"Odd number of lines in file {fileName} with code. One line ask, one line answer.");

                    repeaterEnabled = true;

                    ConsoleWriteLine($"{repeaterMap.Count} pairs ask/answer ready");
                }
                catch (Exception ex)
                {
                    ConsoleWriteError($"Cannot read file {fileName}");
                    ConsoleWriteError(ex.ToString());
                }
            }
        }

        /// <summary>
        /// Event on serial port "pin was changed"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void port_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            if (continuousMode)
                return;

            UI.SetPinStatus((SerialPort)sender);
        }

        /// <summary>
        /// Serial port OnError event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            ConsoleWriteError(e.EventType.ToString());
        }

        /// <summary>
        /// Serial port event when new data arrived
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {            
            SerialPort port = ((SerialPort)sender);
            int byteCount;
            int cycle = 0;

            try
            {
                do
                {
                    byteCount = port.BytesToRead;
                    Thread.Sleep(2);
                    cycle++;
                } while (byteCount < port.BytesToRead);

                if (incoming.Length < byteCount)
                    incoming = new byte[byteCount];
                
                port.Read(incoming, 0, byteCount);
            }
            catch (Exception ex)
            {
                ConsoleWriteError(ex.ToString());
                return;
            }

            TimeSpan time = DateTime.Now.TimeOfDay;
            bool applyGapTolerance = false;

            //print time since last receive only if not disabled
            if (lastTimeReceved > 0)
            {
                double sinceLastReceive = ((double)(time.Ticks - lastTimeReceved) / 10000);
                applyGapTolerance = (gapToleranceEnable && sinceLastReceive <= gapTolerance);

                if (setting.ShowTimeGap && (!gapToleranceEnable || !applyGapTolerance))
                    ConsoleWriteCommunication(ConsoleColor.Magenta, "\n+" + sinceLastReceive.ToString("F3") + " ms");
            }

            //Write to output
            string line = "";

            if (showAscii)
            {
                if (!setting.ShowTime || applyGapTolerance)
                    line = ASCIIEncoding.ASCII.GetString(incoming,0,byteCount);
                else
                    line = time.ToString() + " " + Encoding.ASCII.GetString(incoming, 0, byteCount);
            }
            else
            {
                if (!setting.ShowTime || applyGapTolerance)
                    line = string.Join(" ", incoming.Take(byteCount).Select(x=> $"0x{x:X2}"));
                else
                    line = time.ToString() + " " + string.Join(" ", incoming.Take(byteCount).Select(x => $"0x{x:X2}"));
            }

            if (applyGapTolerance)
                ConsoleWriteCommunication(ConsoleColor.Yellow, line);
            else
            {
                ConsoleWriteCommunication(ConsoleColor.Yellow, "\n");
                ConsoleWriteCommunication(ConsoleColor.Yellow, line);
            }


            lastTimeReceved = time.Ticks;

            if (repeaterEnabled)
            {
                byte[]? data = Data2Send(incoming, byteCount);

                if (data != null)
                {
                    try
                    {
                        port.Write(data, 0, data.Length);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWriteError(ex.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// prepare data as answer
        /// </summary>
        /// <param name="incoming"></param>
        /// <param name="byteCount"></param>
        /// <returns></returns>
        static byte[]? Data2Send(byte[] incoming, int byteCount)
        {
            if (repeaterUseHex)
            {
                string ask = string.Join("", incoming.Take(byteCount).Select(x => x.ToString("X2")));

                if (repeaterMap.ContainsKey(ask))
                {
                    string answer = repeaterMap[ask];
                    byte[] data = new byte[answer.Length / 2];

                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = Convert.ToByte(answer.Substring(2 * i, 2), 16);
                    }

                    if (!setting.ShowTime)
                        ConsoleWriteCommunication(ConsoleColor.Green, string.Join("\n ", Array.ConvertAll(data, x => $"0x{x:X2}")));
                    else
                        ConsoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + string.Join(" ", Array.ConvertAll(data, x => $"0x{x:X2}")));

                    return data;
                }
                else
                {
                    ConsoleWriteLine("Repeater: Unknown ask");
                }
            }
            else
            {
                string ask = ASCIIEncoding.ASCII.GetString(incoming,0,byteCount);

                if (repeaterMap.ContainsKey(ask))
                {
                    string answer = repeaterMap[ask];

                    if (!setting.ShowTime)
                        ConsoleWriteCommunication(ConsoleColor.Green, "\n" + answer);
                    else
                        ConsoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + answer);

                    return ASCIIEncoding.ASCII.GetBytes(answer);
                }
                else
                {
                    ConsoleWriteLine("Repeater: Unknown ask");
                }
            }

            return null;
        }

        /// <summary>
        /// Print error
        /// </summary>
        /// <param name="text"></param>
        private static void ConsoleWriteError(string text)
        {
            ConsoleWriteLine(ConsoleColor.Red, text);
        }

        /// <summary>
        /// Print
        /// </summary>
        /// <param name="message"></param>
        private static void ConsoleWrite(string message)
        {
            if (!continuousMode)
                UI.Write(message);
            else
                Console.Write(message);

            if (logfile && !logincomingonly)
                Trace.Write(message);
        }

        /// <summary>
        /// Print single line
        /// </summary>
        /// <param name="message"></param>
        private static void ConsoleWriteLine(string message)
        {
            if (!continuousMode)
            {
                UI.WriteLine(message);
            }
            else
            {
                if (Console.CursorLeft > 0)
                    Console.WriteLine("");
                Console.WriteLine(message);
            }

            if (logfile && !logincomingonly)
                Trace.WriteLine(string.Format(message));
        }

        /// <summary>
        /// Print single line
        /// </summary>
        /// <param name="color"></param>
        /// <param name="message"></param>
        private static void ConsoleWriteLine(ConsoleColor color, string message)
        {
            if (!continuousMode)
            {
                UI.WriteLine(message, color);
            }
            else
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }

            if (logfile && !logincomingonly)
                Trace.WriteLine(string.Format(message));
        }

        /// <summary>
        /// Print single line without trace log
        /// </summary>
        /// <param name="message"></param>
        private static void ConsoleWriteLineNoTrace(string message)
        {
            if (!continuousMode)
                UI.WriteLine(message);
            else
                Console.WriteLine(message);
        }

        /// <summary>
        /// Print single line without trace log
        /// </summary>
        /// <param name="color"></param>
        /// <param name="message"></param>
        private static void ConsoleWriteLineNoTrace(ConsoleColor color, string message)
        {
            if (!continuousMode)
            {
                UI.WriteLine(message, color);
            }
            else
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Print line that is involved in communication
        /// </summary>
        /// <param name="color"></param>
        /// <param name="message"></param>
        private static void ConsoleWriteCommunication(ConsoleColor color, string message)
        {
            if (!pausePrint)
            {
                if (!continuousMode)
                {
                    UI.Write(message, color);
                }
                else
                {
                    Console.ForegroundColor = color;
                    ConsoleWrite(message);
                    Console.ResetColor();
                }
            }

            if (logfile)
                Trace.Write(string.Format(message));
        }

        /// <summary>
        /// Move console cursor in current line
        /// </summary>
        /// <param name="moveBy"></param>
        private static void ConsoleCursorLeft(int moveBy)
        {
            if (continuousMode)
                Console.CursorLeft += moveBy;
            //else
            //    Cinout.CursorLeft(moveBy);
        }

        /// <summary>
        /// Set cursor to left border
        /// </summary>
        private static void ConsoleCursorLeftReset()
        {
            if (continuousMode)
                Console.CursorLeft = 0;
            //else
            //    Cinout.CursorLeftReset();
        }

        /// <summary>
        /// Print help
        /// </summary>
        private static void PrintHelp()
        {
            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("Usage: serialmonitor PortName [<switch> parameter]");
            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("Switches:");
            ConsoleWriteLineNoTrace("-baudrate {{baud rate}}: set port baud rate. Default 9600kbps.");
            ConsoleWriteLineNoTrace("-parity {{used parity}}: set used port parity. Default none. Available parameters odd, even, mark and space.");
            ConsoleWriteLineNoTrace("-databits {{used databits}}: set data bits count. Default 8 data bits.");
            ConsoleWriteLineNoTrace("-stopbits {{used stopbits}}: set stop bits count. Default 1 stop bit. Available parameters 0, 1, 1.5 and 2.");
            ConsoleWriteLineNoTrace("-repeatfile {{file name}}: enable repeat mode with protocol specified in file");
            ConsoleWriteLineNoTrace("-logfile {{file name}}: set file name for log into that file");
            ConsoleWriteLineNoTrace("-logincomingonly: log into file would be only incoming data");
            ConsoleWriteLineNoTrace("-showascii: communication would be show in ASCII format (otherwise HEX is used)");
            ConsoleWriteLineNoTrace("-notime: time information about incoming data would not be printed");
            ConsoleWriteLineNoTrace("-gaptolerance {{time gap in ms}}: messages received within specified time gap will be printed together");
            ConsoleWriteLineNoTrace("-nogui: start program in normal console mode (scrolling list). Not with primitive text GUI");

            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("Example: serialmonitor COM1");
            ConsoleWriteLineNoTrace("         serialmonitor COM1 -baudrate 57600 -parity odd -databits 7 -stopbits 1.5");
            ConsoleWriteLineNoTrace("         serialmonitor COM83 -baudrate 19200 -repeatfile protocol.txt");

            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("Program shortcuts:");
            ConsoleWriteLineNoTrace("F1: print help");
            ConsoleWriteLineNoTrace("F2: pause/resume print on screen");
            ConsoleWriteLineNoTrace("F3: toggle between data print format (HEX / ASCII)");
            ConsoleWriteLineNoTrace("F4: pause/resume connection to serial port");
            ConsoleWriteLineNoTrace("F5: send specified data (in HEX format if data start with 0x otherwise ASCII is send)");
            ConsoleWriteLineNoTrace("F6: send specified file)");

            ConsoleWriteLineNoTrace("F10: program exit");
            ConsoleWriteLineNoTrace("F11: toggle RTS pin");
            ConsoleWriteLineNoTrace("F12: toggle DTR pin");
            ConsoleWriteLineNoTrace("^F12: setting");

            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("In program commands:");
            ConsoleWriteLineNoTrace("help: print help");
            ConsoleWriteLineNoTrace("send <message>: send message");

            if (continuousMode)
            {
                ConsoleWriteLineNoTrace("");
                ConsoleWriteLineNoTrace("In program color schema:");
                ConsoleWriteLineNoTrace(ConsoleColor.Cyan, "Control pin status changed");
                ConsoleWriteLineNoTrace(ConsoleColor.Green, "Control pin ON");
                ConsoleWriteLineNoTrace(ConsoleColor.White, "Control pin OFF");
                ConsoleWriteLineNoTrace(ConsoleColor.Magenta, "Time between received data");
                ConsoleWriteLineNoTrace(ConsoleColor.Yellow, "Received data");
                ConsoleWriteLineNoTrace(ConsoleColor.Green, "Sended data");
                ConsoleWriteLineNoTrace(ConsoleColor.Red, "Error");

                Console.ResetColor();
            }
        }

        /// <summary>
        /// Call exit actions
        /// </summary>
        private static void Exit()
        {
            Config.SaveHistory(UI.CommandHistory);
            Config.SaveFileList(UI.FileHistory);
        }
    }
}
