//---------------------------------------------------------------------------
//
// Name:        Program.cs
// Author:      Vita Tucek
// Created:     20.2.2022
// License:     MIT
// Description: UI
//
//---------------------------------------------------------------------------

using System.Diagnostics;
using System.IO.Ports;
using Terminal.Gui;

namespace SerialMonitor
{
    internal class UI
    {
        static readonly RingBuffer<LogRecord> lines = new RingBuffer<LogRecord>(500);
        // port label
        static readonly Label portLabel = new Label() { Text = "Port: ", X = 1, Y = 0 };
        static readonly Label pinsLabel = new Label() { Text = "Pins: ", X = 1, Y = 1 };
        static readonly Label portName = new Label() { Text = "???", X = 8, Y = 0 };
        static readonly Label portStatus = new Label() { Text = "Closed", X = Pos.Right(portName) + 2, Y = 0 };
        static readonly Label portParameters = new Label() { Text = $"0000000b/s", X = Pos.Right(portStatus) + 2, Y = 0 };
        static readonly Label pinRTS = new Label() { Text = "RTS(?)", X = 8, Y = 1 };
        static readonly Label pinCTS = new Label() { Text = "CTS(?)", X = Pos.Right(pinRTS) + 2, Y = 1 };
        static readonly Label pinDTR = new Label() { Text = "DTR(?)", X = Pos.Right(pinCTS) + 2, Y = 1 };
        static readonly Label pinDSR = new Label() { Text = "DSR(?)", X = Pos.Right(pinDTR) + 2, Y = 1 };
        static readonly Label pinCD = new Label() { Text = "CD(?)", X = Pos.Right(pinDSR) + 2, Y = 1 };
        static readonly Label pinBreak = new Label() { Text = "Break(?)", X = Pos.Right(pinCD) + 2, Y = 1 };

        static readonly Label timeLabel = new Label() { Text = "", X = 50, Y = 0 };
        static readonly Label debugLabel = new Label() { Text = "", X = 35, Y = 0 };

        private static bool printAsHexToLogView = true;
        // properties
        public static bool PrintToLogView { get; set; } = true;
        public static bool PrintAsHexToLogView { get => printAsHexToLogView; set { printAsHexToLogView = value; SetBottomMenuText(); } }
        public static bool RequestPortClose { get; set; } = false;
        public static List<string> CommandHistory { get; } = new List<string>();
        public static List<string> FileHistory { get; } = new();

        // menu
        static readonly Label menu = new Label() { X = 0, Y = 0 };
        public static Action? ActionHelp;
        public static Action<bool>? ActionPrint;
        public static Action<bool>? ActionPrintAsHex;
        public static Action<bool>? ActionOpenClose;
        public static Action<string?>? ActionSend;
        public static Action<string?>? ActionSendFile;
        public static Action<string?>? ActionFilter;
        public static Action? ActionRts;
        public static Action? ActionDtr;
        public static Action<string?>? ActionCommand;
        public static Func<Setting>? ActionSettingLoad;
        public static Func<Setting, bool>? ActionSettingSave;

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
                Title = $"SerialMonitor v.{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)}"
            };

            // hotkeys
            Application.QuitKey = Key.CtrlMask | Key.q;
            win.KeyUp += (e) =>
            {
                e.Handled = ProcessHotKey(e.KeyEvent);
            };
            // set colorscheme
            win.ColorScheme = Colors.TopLevel;
            // top frame
            var frameStatus = new FrameView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 4,
            };

            frameStatus.Add(portLabel, pinsLabel, portName, portStatus, portParameters,
                pinRTS, pinCTS, pinDTR, pinDSR, pinCD, pinBreak, timeLabel, debugLabel);

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
            // command textfield
            var commandlabel = new Label(">")
            {
                X = 0,
                Y = Pos.Bottom(frameData),
            };
            commandlabel.ColorScheme = Colors.Dialog;
            var commandline = new TextField()
            {
                X = 1,
                Y = Pos.Bottom(frameData),
                Width = Dim.Fill(),

            };
            int commandId = -1;
            commandline.KeyPress += (e) =>
            {
                switch (e.KeyEvent.Key)
                {
                    case Key.CursorUp:
                        e.Handled = true;
                        if (!CommandHistory.Any())
                            return;
                        if (commandId == -1)
                            commandId = CommandHistory.Count - 1;
                        else if (commandId == 0)
                            return;
                        else
                            commandId--;
                        commandline.Text = CommandHistory[commandId];
                        break;
                    case Key.CursorDown:
                        e.Handled = true;
                        if (!CommandHistory.Any())
                            return;
                        if (commandId < 0 || commandId + 1 >= CommandHistory.Count)
                        {
                            if (!commandline.Text.IsEmpty)
                                commandline.Text = "";
                            return;
                        }
                        commandline.Text = CommandHistory[++commandId];
                        break;
                    case Key.Enter:
                        e.Handled = true;
                        commandId = -1;
                        string? text = commandline.Text.ToString();
                        if (string.IsNullOrEmpty(text))
                            return;
                        commandline.Text = "";
                        if (!CommandHistory.Any() || !CommandHistory.Last<string>().Equals(text))
                            CommandHistory.Add(text);
                        ActionCommand?.Invoke(text);
                        break;
                }
            };
            // compose  
            frameData.Add(logView);
            win.Add(frameStatus, frameData, commandlabel, commandline);
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

            logView.RowRender += (e) =>
            {
                if (e.Row == logView.SelectedItem)
                    return;
                /*
                if (e.Row % 2 == 0)
                    e.RowAttribute = new Terminal.Gui.Attribute(Color.Green, Color.Black);
                else
                    e.RowAttribute = new Terminal.Gui.Attribute(Color.Black, Color.Green);
                */
            };

            // add shortcut menu
            Application.Top.Add(menu);
            menu.Y = Pos.Bottom(Application.Top) - 1;
            SetBottomMenuText();
            // bind log data
            logView.SetSource(lines);
            commandline.SetFocus();
        }

        public static void Exit()
        {
            Application.Driver.Suspend();
            Application.Refresh();
        }

        public static void Shutdown()
        {
            Application.RequestStop();
        }

        private static bool ProcessHotKey(KeyEvent keyEvent)
        {
            if (keyEvent.Key == Key.F1)
            {
                ActionHelp?.Invoke();
            }
            else if (keyEvent.Key == Key.F2)
            {
                var ok = new Button("Ok");
                var cancel = new Button("Cancel");
                var dialog = new Dialog("Setting", 50, 11, ok, cancel);
                var lbPort = new Label("Port:") { X = 1, Y = 1 };
                var lbSpeed = new Label("Baud rate:") { X = 1, Y = 2 };
                var lbParity = new Label("Parity:") { X = 1, Y = 3 };
                var tbPort = new TextField() { X = 15, Y = 1, Width = 15, Text = "" };
                var tbSpeed = new TextField() { X = 15, Y = 2, Width = 10, Text = "" };
                var tbParity = new TextField() { X = 15, Y = 3, Width = 10, Text = "" };
                var cbTime = new CheckBox("Show transaction time") { X = 1, Y = 5 };
                var cbTimeGap = new CheckBox("Show time between 2 transactions") { X = 1, Y = 6 };
                var cbSent = new CheckBox("Show sent data") { X = 1, Y = 7 };

                dialog.Add(lbPort, lbSpeed, lbParity, tbPort, tbSpeed, tbParity, cbTime, cbTimeGap, cbSent);
                dialog.ColorScheme = Colors.Dialog;

                if (ActionSettingLoad == null)
                    return false;

                Setting setting = ActionSettingLoad.Invoke();

                tbPort.Text = setting.Port;
                tbSpeed.Text = setting.BaudRate.ToString();
                tbParity.Text = setting.Parity.ToString();
                cbTime.Checked = setting.ShowTime;
                cbTimeGap.Checked = setting.ShowTimeGap;
                cbSent.Checked = setting.ShowSentData;

                ok.Clicked += () =>
                {
                    var port = tbPort.Text.ToString();
                    if (string.IsNullOrEmpty(port) || !int.TryParse(tbSpeed.Text.ToString(), out int baudrate)
                        || !Enum.TryParse<Parity>(tbParity.Text.ToString(), true, out Parity parity))
                        return;

                    setting.Port = port;
                    setting.BaudRate = baudrate;
                    setting.Parity = parity;
                    setting.ShowTime = cbTime.Checked;
                    setting.ShowTimeGap = cbTimeGap.Checked;
                    setting.ShowSentData = cbSent.Checked;

                    if (ActionSettingSave?.Invoke(setting) == true)
                    {
                        Application.RequestStop();
                    }
                };
                cancel.Clicked += () =>
                {
                    Application.RequestStop();
                };
                Application.Run(dialog);
            }
            else if (keyEvent.Key == Key.F3)
            {
                PrintAsHexToLogView = !PrintAsHexToLogView;
                ActionPrintAsHex?.Invoke(PrintAsHexToLogView);
            }
            else if (keyEvent.Key == Key.F4)
            {
                // dialog
                bool filterpressed = false;
                var filter = new Button("Filter");
                var cancel = new Button("Cancel");
                var clear = new Button("Clear");
                var dialog = new Dialog("Filter data that start with...", 50, 6, filter, cancel, clear);
                var textField = new TextField()
                {
                    X = Pos.Center(),
                    Y = Pos.Center(),
                    Width = Dim.Fill() - 2
                };
                textField.KeyUp += (key) =>
                {
                    if (key.KeyEvent.Key == Key.Enter)
                    {
                        filterpressed = true;
                        Application.RequestStop();
                    }
                };
                dialog.Add(textField);

                filter.Clicked += () =>
                {
                    filterpressed = true;
                    Application.RequestStop();
                };
                cancel.Clicked += () =>
                {
                    Application.RequestStop();
                };
                clear.Clicked += () =>
                {
                    textField.Text = "";
                    filterpressed = true;
                    Application.RequestStop();
                };
                if (lines.Filtered)
                    textField.Text = lines.Filter;
                textField.SetFocus();
                Application.Run(dialog);

                if (filterpressed)
                {
                    var filterText = textField.Text.ToString();
                    lines.SetFilter(filterText);
                    SetBottomMenuText();
                    ActionFilter?.Invoke(filterText);
                }
            }
            else if (keyEvent.Key == Key.F5)
            {
                // dialog
                bool sendpressed = false;
                var send = new Button("Send");
                var cancel = new Button("Cancel");
                var dialog = new Dialog("Send message", 50, 6, send, cancel);
                var textField = new TextField()
                {
                    X = Pos.Center(),
                    Y = Pos.Center(),
                    Width = Dim.Fill() - 2
                };
                textField.KeyUp += (key) =>
                {
                    if (key.KeyEvent.Key == Key.Enter && !textField.Text.IsEmpty)
                    {
                        sendpressed = true;
                        Application.RequestStop();
                    }
                };
                dialog.Add(textField);

                send.Clicked += () =>
                {
                    if (textField.Text.IsEmpty)
                    {
                        MessageBox.Query("Warning", "Fill text line.", "OK");
                    }
                    else
                    {
                        sendpressed = true;
                        Application.RequestStop();
                    }
                };
                cancel.Clicked += () =>
                {
                    Application.RequestStop();
                };
                textField.SetFocus();
                Application.Run(dialog);

                if (sendpressed)
                    ActionSend?.Invoke(textField.Text.ToString());
            }
            else if (keyEvent.Key == Key.F6)
            {
                // dialog
                bool sendpressed = false;
                var browse = new Button("Browse");
                var send = new Button("Send") { Enabled = FileHistory.Count > 0 };
                var cancel = new Button("Cancel");
                var remove = new Button("Remove") { Enabled = FileHistory.Count > 0 };
                var dialog = new Dialog("Send file", 50, 10, browse, send, remove, cancel);

                var fileList = new ListView() { X = 1, Y = 1, Height = Dim.Fill() - 2, Width = Dim.Fill() - 2 };
                fileList.SetSource(FileHistory);
                fileList.ColorScheme = Colors.TopLevel;
                dialog.Add(fileList);

                browse.Clicked += () =>
                {
                    bool okpressed = false;
                    var ok = new Button("Ok");
                    var cancel = new Button("Cancel");
                    var fileDlg = new Dialog("File to send...", 50, 6, ok, cancel);
                    var filePathField = new TextField()
                    {
                        X = Pos.Center(),
                        Y = Pos.Center(),
                        Width = Dim.Fill() - 2
                    };
                    filePathField.KeyUp += (key) =>
                    {
                        if (key.KeyEvent.Key == Key.Enter && !filePathField.Text.IsEmpty)
                        {
                            if (File.Exists(filePathField.Text.ToString()))
                            {
                                okpressed = true;
                                Application.RequestStop();
                            }
                            else
                            {
                                MessageBox.Query("Warning", "File does not exist.", "OK");
                            }
                        }
                    };
                    fileDlg.Add(filePathField);

                    ok.Clicked += () =>
                    {
                        if (File.Exists(filePathField.Text.ToString()))
                        {
                            okpressed = true;
                            Application.RequestStop();
                        }
                        else
                        {
                            MessageBox.Query("Warning", "File does not exist.", "OK");
                        }
                    };
                    cancel.Clicked += () =>
                    {
                        Application.RequestStop();
                    };
                    filePathField.SetFocus();
                    Application.Run(fileDlg);
                    if (okpressed)
                    {
                        FileHistory.Add(filePathField.Text.ToString());
                        remove.Enabled = send.Enabled = true;
                    }
                };
                send.Clicked += () =>
                {
                    sendpressed = true;
                    Application.RequestStop();
                };
                remove.Clicked += () =>
                {
                    if (fileList.SelectedItem > -1 && FileHistory.Count > 0)
                    {
                        FileHistory.RemoveAt(fileList.SelectedItem);
                        fileList.SelectedItem = FileHistory.Count - 1;

                        remove.Enabled = send.Enabled = FileHistory.Count > 0;
                    }
                };
                cancel.Clicked += () =>
                {
                    Application.RequestStop();
                };

                Application.Run(dialog);

                if (sendpressed)
                    // Send file data
                    ActionSendFile?.Invoke(FileHistory[fileList.SelectedItem]);
            }
            else if (keyEvent.Key == Key.F9)
            {
                RequestPortClose = !RequestPortClose;
                ActionOpenClose?.Invoke(RequestPortClose);
                SetBottomMenuText();
            }
            else if (keyEvent.Key == Key.F11 || keyEvent.Key == (Key.D1 | Key.CtrlMask))
            {
                ActionRts?.Invoke();
            }
            else if (keyEvent.Key == Key.F12 || keyEvent.Key == (Key.D2 | Key.CtrlMask))
            {
                ActionDtr?.Invoke();
            }
            else if (keyEvent.Key == (Key.P | Key.CtrlMask))
            {
                PrintToLogView = !PrintToLogView;
                ActionPrint?.Invoke(PrintToLogView);
                SetBottomMenuText();
            }
            else
            {
                return false;
            }

            return true;
        }

        private static void SetBottomMenuText()
        {
            menu.Text = $" F1 Help | F2 Setup | F3 {(!PrintAsHexToLogView ? "Hex " : "Text")} | F4 {(lines.Filtered ? "FILTER" : "Filter")} | F5 Send | F6 SendFile | F9 {(!RequestPortClose ? "Close" : "Open ")} | F11 RTS | F12 DTR | ^P {(!PrintToLogView ? "Print" : "Pause")} | ^Q Exit";
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
            portParameters.Text = $"{port.BaudRate}b/s  Parity: {port.Parity}";
        }

        internal static void SetPinStatus(SerialPort port)
        {
            if (!port.IsOpen)
                return;

            pinRTS.Text = $"RTS({(port.RtsEnable ? 1 : "0")})";
            pinCTS.Text = $"CTS({(port.CtsHolding ? 1 : "0")})";
            pinDTR.Text = $"DTR({(port.DtrEnable ? 1 : "0")})";
            pinDSR.Text = $"DSR({(port.DsrHolding ? 1 : "0")})";
            pinCD.Text = $"CD({(port.CDHolding ? 1 : "0")})";
            pinBreak.Text = $"Break({(port.BreakState ? 1 : "0")})";
        }

        internal static void WriteLine(string message, LogRecordType type, TraceEventType level = TraceEventType.Information)
        {
            if (!PrintToLogView)
                return;
            bool added = false;
            if (logView.GetCurrentWidth(out var width) && width > 0)
            {
                if (message.Length > width)
                {
                    foreach (var chunk in message.Chunk(width).Select(x => new string(x)))
                        lines.Add(new LogRecord(chunk, type, level));
                    added = true;
                }
            }
            if (!added)
            {
                if (message.Length == 0)
                    lines.Add(new LogRecord(" ", type, level));
                else
                    lines.Add(new LogRecord(message, type, level));
            }

            if (!logView.IsInitialized)
                return;

            if (logView.SelectedItem >= lines.Count - 2)
                logView.MoveDown();
        }

        private static void WriteInternally(string message,LogRecordType type, TraceEventType level)
        {
            if (!PrintToLogView)
                return;
            if (lines.IsEmpty)
            {
                WriteLine(message, type, level);
                return;
            }

            if (logView.GetCurrentWidth(out var width))
            {
                if (lines.Last.Length + message.Length < width)
                {
                    lines.Last.Append(message);
                }
                else
                {
                    var messages = lines.Last.Text.Concat(message).Chunk(width).Select(x => new string(x)).ToList();
                    lines.Last.Text = messages.First();
                    foreach (var chunk in messages.Skip(1))
                        WriteLine(chunk, type, level);
                }
            }
            else
            {
                lines.Last.Append(message);
            }
        }

        internal static void Write(string message, LogRecordType type, TraceEventType level)
        {
            if (message.Contains("\r\n") || message.Contains('\n'))
            {
                var msgLines = message.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < msgLines.Length; i++)
                {
                    if (i == 0)
                        WriteInternally(msgLines[i], type, level);
                    else
                        WriteLine(msgLines[i], type, level);
                }
            }
            else
            {
                WriteInternally(message, type, level);
            }
        }
    }
}
