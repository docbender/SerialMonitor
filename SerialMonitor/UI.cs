using System.IO.Ports;
using Terminal.Gui;

namespace SerialMonitor
{
    internal class UI
    {
        static readonly RingBuffer<string> lines = new RingBuffer<string>(500);
        // port label
        static readonly Label portLabel = new Label() { Text = "Port: ", X = 1, Y = 0 };
        static readonly Label pinsLabel = new Label() { Text = "Pins: ", X = 1, Y = 1 };
        static readonly Label portName = new Label() { Text = "???", X = 8, Y = 0 };
        static readonly Label portStatus = new Label() { Text = "Closed", X = Pos.Right(portName) + 2, Y = 0 };
        static readonly Label portSpeed = new Label() { Text = $"Speed: 0b/s", X = Pos.Right(portStatus) + 2, Y = 0 };
        static readonly Label pinRTS = new Label() { Text = "RTS(?)", X = 8, Y = 1 };
        static readonly Label pinCTS = new Label() { Text = "CTS(?)", X = Pos.Right(pinRTS) + 2, Y = 1 };
        static readonly Label pinDTR = new Label() { Text = "DTR(?)", X = Pos.Right(pinCTS) + 2, Y = 1 };
        static readonly Label pinDSR = new Label() { Text = "DSR(?)", X = Pos.Right(pinDTR) + 2, Y = 1 };
        static readonly Label pinCD = new Label() { Text = "CD(?)", X = Pos.Right(pinDSR) + 2, Y = 1 };
        static readonly Label pinBreak = new Label() { Text = "Break(?)", X = Pos.Right(pinCD) + 2, Y = 1 };

        static readonly Label timeLabel = new Label() { Text = "", X = 50, Y = 0 };
        static readonly Label debugLabel = new Label() { Text = "", X = 40, Y = 0 };
        // properties
        public static bool PrintToLogView { get; set; } = true;
        public static bool PrintAsHexToLogView { get; set; } = true;
        public static bool RequestPortClose { get; set; } = false;
        public static List<string> CommandHistory { get; } = new List<string>();

        // menu
        static readonly Label menu = new Label() { X = 0, Y = 0 };
        public static Action? ActionHelp;
        public static Action<bool>? ActionPrint;
        public static Action<bool>? ActionPrintAsHex;
        public static Action<bool>? ActionOpenClose;
        public static Action<string>? ActionSend;
        public static Action? ActionRts;
        public static Action? ActionDtr;

        // data view
        static readonly ListView logView = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        public static void Init()
        {
            Application.Init();
            // main window
            var win = new Window()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                Title = $"SerialMonitor v.{System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(3)}"
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            };

            // hotkeys
            Application.QuitKey = Key.F10;
            win.KeyUp += (e) =>
            {
                processHotKey(e.KeyEvent);
            };
            // set colorscheme
            win.ColorScheme = Colors.TopLevel;
            // top frame
            var frameStatus = new FrameView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 4
            };
            
            frameStatus.Add(portLabel, pinsLabel, portName, portStatus, portSpeed,
                pinRTS, pinCTS, pinDTR, pinDSR, pinCD, pinBreak, debugLabel, timeLabel);

            timeLabel.X = Pos.Right(frameStatus) - 10;
            // data frame
            var frameData = new FrameView()
            {
                X = 0,
                Y = 4,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 1,
                Text = "text"
            };

            // compose
            frameData.Add(logView);
            win.Add(frameStatus, frameData);
            Application.Top.Add(win);
            // scrollbar for textview
            var _scrollBar = new ScrollBarView(logView, true);
            _scrollBar.ChangedPosition += () =>
            {
                /*logView.CursorPosition = new Point(logView.CursorPosition.X, _scrollBar.Position);
                if (textView.CursorPosition.Y != _scrollBar.Position)
                {
                    _scrollBar.Position = logView.CursorPosition.Y;
                }
                logView.SetNeedsDisplay();*/
            };

            logView.DrawContent += (e) =>
            {
                _scrollBar.Size = lines.Count - 1;
                _scrollBar.Position = logView.SelectedItem;
                _scrollBar.Refresh();
            };
            // add shortcut menu
            Application.Top.Add(menu);
            menu.Y = Pos.Bottom(Application.Top) - 1;
            SetBottomMenuText();
            // bind log data
            logView.SetSource(lines);
        }

        private static void processHotKey(KeyEvent keyEvent)
        {
            if (keyEvent.Key == Key.F1)
            {
                ActionHelp?.Invoke();
            }
            else if (keyEvent.Key == Key.F2)
            {
                PrintToLogView = !PrintToLogView;
                ActionPrint?.Invoke(PrintToLogView);
                SetBottomMenuText();
            }
            else if (keyEvent.Key == Key.F3)
            {
                PrintAsHexToLogView = !PrintAsHexToLogView;
                ActionPrintAsHex?.Invoke(PrintAsHexToLogView);
                SetBottomMenuText();
            }
            else if (keyEvent.Key == Key.F4)
            {
                RequestPortClose = !RequestPortClose;
                ActionOpenClose?.Invoke(RequestPortClose);
                SetBottomMenuText();
            }
            else if (keyEvent.Key == Key.F5)
            {    
                // TODO: Send string - dialog needed            
                ActionSend?.Invoke("NotImplemented");
            }
            else if (keyEvent.Key == Key.F6)
            {
                // TODO: Send file - file selection needed
                ActionSend?.Invoke("NotImplemented");
            }
            else if (keyEvent.Key == Key.F11)
            {
                ActionRts?.Invoke();
            }
            else if (keyEvent.Key == Key.F12)
            {
                ActionDtr?.Invoke();
            }
        }

        private static void SetBottomMenuText()
        {
            menu.Text = $" F1 Help | F2 {(!PrintToLogView ? "Print   " : "No Print")} | F3 {(!PrintAsHexToLogView ? "Hex " : "Text")} | F4 {(!RequestPortClose ? "Close" : "Open ")} | F5 Send | F6 SendFile | F10 Exit | F11 RTS | F12 DTR";
        }

        public static void Run(Func<MainLoop, bool> action)
        {
            Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), action);
            Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(1000), (loop) => { timeLabel.Text = DateTime.Now.ToLongTimeString(); return true; });
            Application.Run();
        }

        internal static void SetPortStatus(SerialPort port)
        {
            portName.Text = port.PortName;
            portStatus.Text = port.IsOpen ? "Opened" : "Closed";
            portSpeed.Text = $"{port.BaudRate}b/s";
        }

        internal static void SetPinStatus(SerialPort port)
        {
            pinRTS.Text = $"RTS({(port.RtsEnable ? 1 : "0")})";
            pinCTS.Text = $"CTS({(port.CtsHolding ? 1 : "0")})";
            pinDTR.Text = $"DTR({(port.DtrEnable ? 1 : "0")})";
            pinDSR.Text = $"DSR({(port.DsrHolding ? 1 : "0")})";
            pinCD.Text = $"CD({(port.CDHolding ? 1 : "0")})";
            pinBreak.Text = $"Break({(port.BreakState ? 1 : "0")})";
        }

        internal static void WriteLine(string message, ConsoleColor color = ConsoleColor.White)
        {
            lines.Add(message);
            if (!logView.IsInitialized)
                return;

            if (logView.SelectedItem >= lines.Count-2)
                logView.MoveDown();

            debugLabel.Text = $"xx/{lines.Count}";
        }

        internal static void WriteLine(string message, object[] parameters, ConsoleColor color = ConsoleColor.White)
        {
            WriteLine(string.Format(message, parameters), color);
        }

        internal static void Write(string message, ConsoleColor color = ConsoleColor.White)
        {
            if (lines.IsEmpty)
                lines.Add(message);
            else
                lines.Last += message;
        }

        internal static void Write(string message, object[] parameters, ConsoleColor color = ConsoleColor.White)
        {
            var msg = string.Format(message, parameters);

            if (msg.Contains("\r\n") || msg.Contains('\n'))
            {
                var msgLines = message.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < msgLines.Length; i++)
                {
                    if (i == 0)
                        Write(msgLines[i], color);
                    else
                        WriteLine(msgLines[i], color);
                }
            }
            else
            {
                Write(msg, color);
            }
        }
    }
}
