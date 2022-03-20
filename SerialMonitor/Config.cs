//---------------------------------------------------------------------------
//
// Name:        Config.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Save / load program configuration
//
//---------------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;

namespace SerialMonitor
{
    class Config
    {
        const string CONFIG_FILE = "serialmonitor.cfg";
        const string START_ARGUMENTS = "StartArgs";
        const string START_ARGUMENTS_REGEX = "(" + START_ARGUMENTS + "=)([^\n]*)";
        const string HISTORY = "CommandHistory";
        const string FILE_LIST = "FileList";
        const string HISTORY_REGEX = "(" + HISTORY + "=)([^\n]*)";
        const string FILE_LIST_REGEX = "(" + FILE_LIST + "=)([^\n]*)";
        public const string SETTING_PORT = "Port";
        public const string SETTING_BAUDRATE = "BaudRate";
        public const string SETTING_SHOWTIME = "ShowTime";
        public const string SETTING_SHOWTIMEGAP = "ShowTimeGap";
        public const string SETTING_SHOWSENTDATA = "ShowSentData";
        public const string SETTING_SHOWASCII = "ShowAscii";        

        static readonly string filePath = Path.Combine(Directory.GetCurrentDirectory(), CONFIG_FILE);

        /// <summary>
        /// Save started parameters
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool SaveStarters(string[] args)
        {
            string cfg = ReadConfigFileAndPrepareSave(START_ARGUMENTS, String.Join(";", args));

            return SaveConfigFile(cfg);
        }

        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool SaveHistory(IEnumerable<string> args)
        {
            string cfg = ReadConfigFileAndPrepareSave(HISTORY, String.Join(";", args));

            return SaveConfigFile(cfg);
        }

        /// <summary>
        /// Save file list
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool SaveFileList(IEnumerable<string> args)
        {
            string cfg = ReadConfigFileAndPrepareSave(FILE_LIST, String.Join(";", args));

            return SaveConfigFile(cfg);
        }

        /// <summary>
        /// Save user setting
        /// </summary>
        /// <param name="device"></param>
        /// <param name="baudrate"></param>
        /// <param name="showTime"></param>
        /// <param name="showTimeGap"></param>
        /// <param name="showSentData"></param>
        /// <param name="showAscii"></param>
        /// <returns></returns>
        internal static bool SaveSetting(string device, int baudrate, bool showTime, bool showTimeGap, bool showSentData, bool showAscii)
        {
            string? cfg = ReadConfiguration();
            cfg = PrepareSave(cfg, SETTING_PORT, device);
            cfg = PrepareSave(cfg, SETTING_BAUDRATE, baudrate.ToString());
            cfg = PrepareSave(cfg, SETTING_SHOWTIME, showTime ? "1" : "0");
            cfg = PrepareSave(cfg, SETTING_SHOWTIMEGAP, showTimeGap ? "1" : "0");
            cfg = PrepareSave(cfg, SETTING_SHOWSENTDATA, showSentData ? "1" : "0");
            cfg = PrepareSave(cfg, SETTING_SHOWASCII, showAscii ? "1" : "0");

            return SaveConfigFile(cfg);
        }

        /// <summary>
        /// Save configuration into file
        /// </summary>
        /// <param name="cfg"></param>
        /// <returns></returns>
        private static bool SaveConfigFile(string configuration)
        {
            try
            {
                using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(configuration);
                    sw.Flush();
                    sw.Close();
                }

                fs.Close();
            }
            catch (System.IO.IOException ex)
            {
                Console.WriteLine($"Error (IOException) accessing config file. {ex}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing config file. {ex}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Prepare configuration file
        /// </summary>
        /// <param name="itemName"></param>
        /// <param name="itemValue"></param>
        /// <returns></returns>
        private static string ReadConfigFileAndPrepareSave(string itemName, string itemValue)
        {
            return PrepareSave(ReadConfiguration(), itemName, itemValue);
        }

        /// <summary>
        /// Replace old configuration record per item
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="itemName"></param>
        /// <param name="itemValue"></param>
        /// <returns></returns>
        private static string PrepareSave(string? configuration, string itemName, string itemValue)
        {
            var record = $"{itemName}={itemValue}";
            if (string.IsNullOrEmpty(configuration))
                return record;

            Regex rg = new Regex($"{itemName}=.*");
            if (rg.IsMatch(configuration))
                configuration = rg.Replace(configuration, record);
            else
                configuration += "\n" + record;
            return configuration;
        }

        /// <summary>
        /// Load saved start configuration
        /// </summary>
        /// <returns></returns>
        public static string[]? LoadStarters()
        {
            string? cfg = ReadConfiguration();

            if (!string.IsNullOrEmpty(cfg))
            {
                Regex rg = new Regex(START_ARGUMENTS_REGEX);

                MatchCollection mc = rg.Matches(cfg);
                if (mc.Count > 0)
                {
                    string cfgLine = mc[0].Groups[2].Value;

                    return cfgLine.Split(';');
                }
            }

            return null;
        }

        /// <summary>
        /// Load saved start configuration
        /// </summary>
        /// <returns></returns>
        public static string[]? LoadHistory()
        {
            string? cfg = ReadConfiguration();

            if (!string.IsNullOrEmpty(cfg))
            {
                Regex rg = new Regex(HISTORY_REGEX);

                Match mc = rg.Match(cfg);
                if (mc.Success)
                {
                    string cfgLine = mc.Groups[2].Value;
                    if (string.IsNullOrEmpty(cfgLine))
                        return null;

                    return cfgLine.Split(';');
                }
            }

            return null;
        }

        /// <summary>
        /// Load saved start configuration
        /// </summary>
        /// <returns></returns>
        public static string[]? LoadFileList()
        {
            string? cfg = ReadConfiguration();

            if (!string.IsNullOrEmpty(cfg))
            {
                Regex rg = new Regex(FILE_LIST_REGEX);

                Match mc = rg.Match(cfg);
                if (mc.Success)
                {
                    string cfgLine = mc.Groups[2].Value;

                    if (string.IsNullOrEmpty(cfgLine))
                        return null;

                    return cfgLine.Split(';');
                }
            }

            return null;
        }

        internal static string? LoadSetting(string parameterName)
        {
            string? cfg = ReadConfiguration();

            if (string.IsNullOrEmpty(cfg))
                return null;

            Regex rg = new Regex($"{parameterName}=(.*)");
            Match mc = rg.Match(cfg);
            if (mc.Success)
                return mc.Groups[1].Value;
            return null;
        }

        private static string? ReadConfiguration()
        {
            string cfg = "";

            if (File.Exists(filePath))
            {
                try
                {
                    using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    if (fs.Length > 0)
                    {
                        using TextReader sr = new StreamReader(fs, Encoding.UTF8);
                        cfg = sr.ReadToEnd();
                    }
                }
                catch (FileNotFoundException ex)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while open config file. {ex}");
                    return null;
                }
            }

            return cfg;
        }
    }
}
