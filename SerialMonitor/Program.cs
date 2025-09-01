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
         "repeatfile", "logfile", "logincomingonly", "showascii", "notime", "gaptolerance", "continuousmode", "nogui", "service" });
        static readonly string? version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
        static long lastTimeReceved = 0;
        static bool repeaterEnabled = false;
        static bool repeaterUseHex = false;
        static readonly Dictionary<string, string> repeaterStringMap = new Dictionary<string, string>();
        static readonly HexDataCollection repeaterHexMap = new HexDataCollection();
        static bool logfile = false;
        static bool logincomingonly = false;
        // default log file name
        static readonly string logSystemFilename = logDataFilename = Path.Combine(OperatingSystem.IsLinux() ? "/var/log/serialmonitor" : Directory.GetCurrentDirectory(), "serialmonitor.log");
        static string logDataFilename = $"log_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt";
        static int gapTolerance = 0;
        static bool gapToleranceEnable = false;
        static bool continuousMode = false;
        static bool serviceMode = false;
        static FileSystemWatcher? _watcher;
        static readonly TraceSource trace = new TraceSource("System");
        static readonly TraceSource traceData = new TraceSource("Data");
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
            setting.Port = OperatingSystem.IsWindows() ? "COM1" : "/dev/ttyS1";

            if (args.Length > 0)
            {
                if (args[0].Equals("-?") || args[0].Equals("-help") || args[0].Equals("--help") || args[0].Equals("-h") || args[0].Equals("/?"))
                {
                    continuousMode = true;
                    Console.WriteLine($"SerialMonitor v.{version}");
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

            trace.Switch = new SourceSwitch("SourceSwitch", "All");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Clear();
            trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(logSystemFilename, "System"));

            // parse commandline arguments
            arguments.Parse(args);

            continuousMode = arguments.GetArgument("continuousmode").Enabled
                || arguments.GetArgument("nogui").Enabled
                || arguments.GetArgument("service").Enabled;

            serviceMode = arguments.GetArgument("service").Enabled;

            if (!continuousMode)
                UI.Init();
            else if (!serviceMode)
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
            port.Parity = setting.Parity;
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

            arg = arguments.GetArgument("showascii");
            if (arg.Enabled)
                setting.ShowAscii = true;
            else
                setting.ShowAscii = Config.LoadSetting(Config.SETTING_SHOWASCII) == "1";

            setting.ShowTimeGap = Config.LoadSetting(Config.SETTING_SHOWTIMEGAP) == "1";
            setting.ShowSentData = Config.LoadSetting(Config.SETTING_SHOWSENTDATA) == "1";

            //log option
            arg = arguments.GetArgument("logfile");
            if (arg.Enabled)
            {
                if (arg.Parameter.Length == 0)
                {
                    if (!serviceMode)
                        ConsoleWriteLine(TraceEventType.Warning, "Warning: Log file name not specified. Used " + logDataFilename);
                }
                else
                    logDataFilename = arg.Parameter;

                logfile = true;
            }
            arg = arguments.GetArgument("logincomingonly");
            if (arg.Enabled)
            {
                if (!logfile)
                {
                    logfile = true;
                    if (!serviceMode)
                        ConsoleWriteLine(TraceEventType.Warning, "Warning: Parameter logfile not specified. Log enabled to the file: " + logDataFilename);
                }
                logincomingonly = true;
            }

            //gap argument
            arg = arguments.GetArgument("gaptolerance");
            if (arg.Enabled)
            {
                _ = int.TryParse(arg.Parameter, out gapTolerance);

                if (gapTolerance == 0)
                    ConsoleWriteLine(TraceEventType.Warning, "Warning: Parameter gaptolerance has invalid argument. Gap tolerance must be greater than zero.");
                else
                    gapToleranceEnable = true;
            }

            if (logfile)
            {
                //check path
                string? path = Path.GetDirectoryName(logDataFilename);
                if (path?.Length > 0)
                {
                    if (!Directory.Exists(path))
                        try
                        {
                            Directory.CreateDirectory(path);
                        }
                        catch (Exception ex)
                        {
                            ConsoleWriteLine(TraceEventType.Warning, $"Warning: Cannot create directory {path}. {ex.Message}");
                        }
                }
                else
                {
                    if (OperatingSystem.IsLinux())
                        logDataFilename = Path.Combine("/var/log/serialmonitor", logDataFilename);
                    else
                        logDataFilename = Path.Combine(Directory.GetCurrentDirectory(), logDataFilename);
                }

                if (!IsFileNameValid(logDataFilename))
                {
                    if (!serviceMode)
                    {
                        ConsoleWriteLine("\nPress [Enter] to exit");
                        Console.ReadLine();
                    }
                    return;
                }

                //assign file to listener
                traceData.Switch = new SourceSwitch("SourceSwitch", "All");
                traceData.Listeners.Clear();
                traceData.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(logDataFilename, "Data"));
            }

            /*
            if (args.Length > 0)
            {
                if (Config.SaveStarters(args))
                    ConsoleWriteLine("Program parameters have been saved. Will be used next time you start program.");
                else
                    ConsoleWriteLine("Warning: Program parameters cannot be saved.");
            }
            */

            Argument repeatfile = arguments.GetArgument("repeatfile");
            if (repeatfile.Enabled)
            {
                if (!IsFileNameValid(repeatfile.Parameter))
                {
                    if (!serviceMode)
                    {
                        ConsoleWriteLine("\nPress [Enter] to exit");
                        Console.ReadLine();
                    }
                    return;
                }
                PrepareRepeatFile(repeatfile.Parameter);
            }

            ConsoleWriteLine(TraceEventType.Information, $"Opening port {port.PortName}: baudrate={port.BaudRate}b/s, parity={port.Parity}, databits={port.DataBits}, stopbits={port.StopBits}");

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
            trace.Flush();

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
                UI.PrintAsHexToLogView = !setting.ShowAscii;
                UI.ActionHelp = () => { PrintHelp(); };
                UI.ActionPrint = (print) => { pausePrint = !print; };
                UI.ActionPrintAsHex = (hex) => { setting.ShowAscii = !hex; };
                UI.ActionOpenClose = (close) => { pauseConnection = close; if (close) port.Close(); UI.SetPortStatus(port); UI.SetPinStatus(port); };
                UI.ActionSend = (data) => { UserDataSend(port, data); };
                UI.ActionSendFile = (file) => { UserDataSendFile(port, file); };
                UI.ActionRts = () => { port.RtsEnable = !port.RtsEnable; UI.SetPortStatus(port); UI.SetPinStatus(port); };
                UI.ActionDtr = () => { port.DtrEnable = !port.DtrEnable; UI.SetPortStatus(port); UI.SetPinStatus(port); };
                UI.ActionCommand = (text) => { ProcessCommand(text); };
                UI.ActionSettingLoad = () => { return setting; };
                UI.ActionSettingSave = (setting) =>
                {
                    if (port.PortName != setting.Port || port.BaudRate != setting.BaudRate)
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

                    return Config.SaveSetting(setting.Port, setting.BaudRate, setting.ShowTime, setting.ShowTimeGap, setting.ShowSentData, setting.ShowAscii);
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

            _watcher?.Dispose();
            trace.Flush();
            trace.Close();
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
            {
                PrintHelp();
            }
            else if (text.Equals("exit"))
            {
                UI.Shutdown();
            }
            else if (text.StartsWith("send "))
            {
                UserDataSend(port, text.Substring(5));
            }
            else
            {
                ConsoleWriteLineNoTrace($"Unknown command: {text}");
            }
        }

        /// <summary>
        /// Send data typed by user
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

            if (line.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = true;

            if (hex)
            {
                string prepared = line.Replace("0x", "", StringComparison.OrdinalIgnoreCase);
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
                else
                {
                    ConsoleWriteLine($" Port {port.PortName} opened");
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
            foreach (char c in Path.GetInvalidPathChars())
            {
                if (filePath.Contains(c.ToString()))
                {
                    ConsoleWriteError($"File name {filePath} contains invalid character [{c}]. Enter right file name.");

                    return false;
                }
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (Path.GetFileName(filePath).Contains(c.ToString()))
                {
                    ConsoleWriteError($"File name {filePath} contains invalid character [{c}]. Enter right file name.");

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Event of changed repeat file
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        /// <exception cref="RepeatFileException"></exception>
        private static void OnRepeatFileChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                _watcher!.EnableRaisingEvents = false;

                ConsoleWriteLine($"File {e.FullPath} was changed. Repeat file will be read now in a while...");
                Thread.Sleep(500);
                PrepareRepeatFile(e.FullPath);
            }

            finally
            {
                _watcher!.EnableRaisingEvents = true;
            }
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
                    if (_watcher == null)
                    {
                        var directory = Path.GetDirectoryName(fileName);
                        if (string.IsNullOrEmpty(directory))
                            directory = Directory.GetCurrentDirectory();
                        var fileNameFiltered = Path.GetFileName(fileName);
                        _watcher = new FileSystemWatcher(directory, fileNameFiltered);
                        _watcher.NotifyFilter = NotifyFilters.LastWrite;
                        _watcher.Changed += new FileSystemEventHandler(OnRepeatFileChanged);
                        _watcher.EnableRaisingEvents = true;
                    }
                    string[] lines = File.ReadAllLines(fileName);

                    if (lines.Length == 0)
                    {
                        ConsoleWriteError($"No line in file {fileName}");
                        return;
                    }

                    ConsoleWriteLine($"File {fileName} opened and {lines.Length} lines has been read");

                    repeaterStringMap.Clear();
                    repeaterHexMap.Clear();

                    // check format file
                    // first line without comment
                    string? startLine = lines.FirstOrDefault(x => !x.Trim().StartsWith('#'));
                    if (startLine == null)
                    {
                        ConsoleWriteError($"No line without comment ('#') in file {fileName}");
                        return;
                    }

                    int linesWithData = 0;

                    Regex reg = new Regex(@"^(?!\s*$)(?:(0x[0-9A-Fa-f]{1,2})*|(\$\d+)*|(\@.+)*| )+$");
                    // match hex string
                    if (reg.IsMatch(startLine))
                    {
                        ConsoleWriteLine("First line corresponds hex format. File will be read and packets compared as HEX.");
                        // remove empty lines
                        lines = lines.Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('#')).ToArray();
                        HexData? ask = null;
                        //check whole file
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];

                            if (reg.IsMatch(line))
                            {
                                if (++linesWithData % 2 == 1)
                                    ask = HexData.Create(line);
                                else
                                    repeaterHexMap.TryAdd(ask!, HexData.Create(line));
                            }
                            else
                            {
                                throw new RepeatFileException("Line {0} not coresponds to hex format.", i);
                            }
                        }

                        repeaterUseHex = true;
                        ConsoleWriteLine($"{repeaterHexMap.Count} pairs ask/answer ready");
                    }
                    else
                    {
                        reg = new Regex("^([0-9A-Fa-f])+$");
                        // match hex string
                        if (reg.IsMatch(startLine))
                        {
                            ConsoleWriteLine("First line corresponds hex format. File will be read and packets compared as HEX.");
                            // remove empty lines
                            lines = lines.Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('#')).ToArray();
                            HexData? ask = null;
                            //check whole file
                            for (int i = 0; i < lines.Length; i++)
                            {
                                var line = lines[i];

                                if (line.Length % 2 == 1)
                                {
                                    throw new RepeatFileException("Line {0} has odd number of characters.", i);
                                }

                                if (reg.IsMatch(line))
                                {
                                    var data = HexData.Create(line);
                                    if (++linesWithData % 2 == 1)
                                        ask = data;
                                    else
                                        repeaterHexMap.TryAdd(ask!, data);
                                }
                                else
                                {
                                    throw new RepeatFileException("Line {0} not coresponds to hex format.", i);
                                }
                            }

                            repeaterUseHex = true;
                            ConsoleWriteLine($"{repeaterHexMap.Count} pairs ask/answer ready");
                        }
                        else
                        {
                            // non hex string
                            ConsoleWriteLine("First line not corresponds hex format. File will be read and packets compared as ASCII.");
                            string ask = string.Empty;
                            //check whole file
                            for (int i = 0; i < lines.Length; i++)
                            {
                                var line = lines[i].Trim();
                                // empty or commented line
                                if (line.Length == 0 || line.StartsWith('#'))
                                    continue;
                                if (++linesWithData % 2 == 1)
                                    ask = line;
                                else
                                    repeaterStringMap.TryAdd(ask, line);
                            }

                            ConsoleWriteLine($"{repeaterStringMap.Count} pairs ask/answer ready");
                        }
                    }

                    if (linesWithData % 2 == 1)
                        ConsoleWriteError($"Odd number of lines in file {fileName} with code. One line ask, one line answer.");

                    repeaterEnabled = true;
                }
                catch (FormatException ex)
                {
                    ConsoleWriteError($"Format mismatch in file {fileName}");
                    ConsoleWriteError(ex.Message);
                }
                catch (Exception ex)
                {
                    ConsoleWriteError($"Cannot read file {fileName}");
                    ConsoleWriteError(ex.Message);
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
            try
            {
                do
                {
                    byteCount = port.BytesToRead;
                    Thread.Sleep(2);
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

            // print time since last receive only if not disabled
            if (lastTimeReceved > 0)
            {
                double sinceLastReceive = ((double)(time.Ticks - lastTimeReceved) / 10000);
                applyGapTolerance = (gapToleranceEnable && sinceLastReceive <= gapTolerance);

                if (setting.ShowTimeGap && (!gapToleranceEnable || !applyGapTolerance))
                    ConsoleWriteCommunication(ConsoleColor.Magenta, "\n+" + sinceLastReceive.ToString("F3") + " ms");
            }

            // Write to output
            string line = "";

            if (!applyGapTolerance)
            {
                if (setting.ShowTimeGap || setting.ShowTime)
                    ConsoleWriteCommunication(ConsoleColor.Yellow, "\n");
                if (setting.ShowTime)
                    ConsoleWriteCommunication(ConsoleColor.Yellow, time.ToString());
            }

            if (setting.ShowTime || applyGapTolerance || !gapToleranceEnable)
                ConsoleWriteCommunication(ConsoleColor.Yellow, " ");

            if (setting.ShowAscii)
                line = ASCIIEncoding.ASCII.GetString(incoming, 0, byteCount);
            else
                line = string.Join(' ', incoming.Take(byteCount).Select(x => $"0x{x:X2}"));
            
            ConsoleWriteCommunication(ConsoleColor.Yellow, line);

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
                //HexData ask = string.Join("", incoming.Take(byteCount).Select(x => x.ToString("X2")));

                if (repeaterHexMap.TryGetValue(incoming, byteCount, out var answer))
                {
                    var data = answer;

                    if (!setting.ShowTime)
                        ConsoleWriteCommunication(ConsoleColor.Green, string.Join("\n ", Array.ConvertAll(data, x => $"0x{x:X2}")));
                    else
                        ConsoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + string.Join(" ", Array.ConvertAll(data, x => $"0x{x:X2}")));

                    return data;
                }
                else
                {
                    ConsoleWriteLineNoTrace("\nRepeater: Unknown ask");
                }
            }
            else
            {
                string ask = ASCIIEncoding.ASCII.GetString(incoming, 0, byteCount);

                if (repeaterStringMap.ContainsKey(ask))
                {
                    string answer = repeaterStringMap[ask];

                    if (!setting.ShowTime)
                        ConsoleWriteCommunication(ConsoleColor.Green, "\n" + answer);
                    else
                        ConsoleWriteCommunication(ConsoleColor.Green, "\n" + DateTime.Now.TimeOfDay.ToString() + " " + answer);

                    return ASCIIEncoding.ASCII.GetBytes(answer);
                }
                else
                {
                    ConsoleWriteLineNoTrace("Repeater: Unknown ask");
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
            ConsoleWriteLine(TraceEventType.Error, text);
        }

        /// <summary>
        /// Print
        /// </summary>
        /// <param name="message"></param>
        private static void ConsoleWrite(string message)
        {
            if (!continuousMode)
                UI.Write(message);
            else if (!serviceMode)
                Console.Write(message);
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
            else if (!serviceMode)
            {
                if (Console.CursorLeft > 0)
                    Console.WriteLine("");
                Console.WriteLine(message);
            }
            else
            {
                Trace(TraceEventType.Information, message);
            }
        }

        /// <summary>
        /// Print single line
        /// </summary>
        /// <param name="color"></param>
        /// <param name="message"></param>
        private static void ConsoleWriteLine(TraceEventType eventType, string message)
        {
            if (!continuousMode)
            {
                UI.WriteLine(message,
                    eventType == TraceEventType.Critical ? ConsoleColor.DarkRed
                    : eventType == TraceEventType.Error ? ConsoleColor.Red
                    : eventType == TraceEventType.Warning ? ConsoleColor.Yellow
                    : ConsoleColor.White
                    );
            }
            else if (!serviceMode || eventType <= TraceEventType.Information)
            {
                Console.ForegroundColor = eventType == TraceEventType.Critical ? ConsoleColor.DarkRed
                    : eventType == TraceEventType.Error ? ConsoleColor.Red
                    : eventType == TraceEventType.Warning ? ConsoleColor.Yellow
                    : ConsoleColor.White;
                Console.WriteLine(message);
                Console.ResetColor();
            }
            else
            {
                Trace(eventType, message);
            }
        }

        /// <summary>
        /// Print single line without trace log
        /// </summary>
        /// <param name="message"></param>
        private static void ConsoleWriteLineNoTrace(string message)
        {
            if (!continuousMode)
                UI.WriteLine(message);
            else if (!serviceMode)
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
            else if (!serviceMode)
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
                else if (!serviceMode)
                {
                    Console.ForegroundColor = color;
                    ConsoleWrite(message);
                    Console.ResetColor();
                }
            }

            if (logfile)
            {
                // log all received data (yellov color) or others if enabled
                if (!logincomingonly || color == ConsoleColor.Yellow)
                    TraceData(string.Format(message));
            }
        }

        /// <summary>
        /// Move console cursor in current line
        /// </summary>
        /// <param name="moveBy"></param>
        private static void ConsoleCursorLeft(int moveBy)
        {
            if (continuousMode)
            {
                var newposition = Console.CursorLeft + moveBy;
                if (newposition > 0 && newposition < Console.BufferWidth)
                {
                    Console.CursorLeft += moveBy;
                }
            }
        }

        /// <summary>
        /// Set cursor to left border
        /// </summary>
        private static void ConsoleCursorLeftReset()
        {
            if (continuousMode)
                Console.CursorLeft = 0;
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
            ConsoleWriteLineNoTrace("-nogui: start program in normal console mode (scrolling list). Not with text GUI");
            ConsoleWriteLineNoTrace("-service: start program in normal console mode with minimal verbose");

            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("Example: serialmonitor COM1");
            ConsoleWriteLineNoTrace("         serialmonitor COM1 -baudrate 57600 -parity odd -databits 7 -stopbits 1.5");
            ConsoleWriteLineNoTrace("         serialmonitor COM83 -baudrate 19200 -repeatfile protocol.txt");

            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("Program shortcuts:");
            ConsoleWriteLineNoTrace("F1: print help");
            ConsoleWriteLineNoTrace("F2: setup program");
            ConsoleWriteLineNoTrace("F3: toggle between data print format (HEX / ASCII)");
            ConsoleWriteLineNoTrace("F4: pause/resume connection to serial port");
            ConsoleWriteLineNoTrace("F5: send a data (in HEX format if data start with 0x otherwise ASCII is send)");
            ConsoleWriteLineNoTrace("F6: send a file");

            ConsoleWriteLineNoTrace("^Q: program exit");
            ConsoleWriteLineNoTrace("F11 or ^1: toggle RTS pin");
            ConsoleWriteLineNoTrace("F12 or ^2: toggle DTR pin");
            ConsoleWriteLineNoTrace("^P: pause / resume print on screen");

            ConsoleWriteLineNoTrace("");
            ConsoleWriteLineNoTrace("In program commands:");
            ConsoleWriteLineNoTrace("help: print help");
            ConsoleWriteLineNoTrace("send <message>: send message");
            ConsoleWriteLineNoTrace("exit: exit the program");

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
            port.Close();

            Config.SaveSetting(setting.Port, setting.BaudRate, setting.ShowTime, setting.ShowTimeGap, setting.ShowSentData, setting.ShowAscii);
            Config.SaveHistory(UI.CommandHistory);
            Config.SaveFileList(UI.FileHistory);

            trace.Close();
            UI.Exit();
        }

        /// <summary>
        /// Log system message
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="message"></param>
        private static void Trace(TraceEventType eventType, string message)
        {
            if (trace.Switch.ShouldTrace(eventType))
            {
                string tracemessage = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.FFF")}\t[{eventType}]\t{string.Format(message)}";
                foreach (TraceListener listener in trace.Listeners)
                {
                    listener.WriteLine(tracemessage);
                    listener.Flush();
                }
            }
        }

        /// <summary>
        /// Log data message
        /// </summary>
        /// <param name="message"></param>
        private static void TraceData(string message)
        {
            foreach (TraceListener listener in traceData.Listeners)
            {
                listener.WriteLine(message);
                listener.Flush();
            }
        }
    }
}
