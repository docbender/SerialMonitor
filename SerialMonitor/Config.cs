//---------------------------------------------------------------------------
//
// Name:        Config.cs
// Author:      Vita Tucek
// Created:     11.3.2015
// License:     MIT
// Description: Save / load program configuration
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace SerialMonitor
{
    class Config
    {
        const string CONFIG_FILE = "serialmonitor.cfg";
        const string START_ARGUMENTS = "StartArgs=";
        const string START_ARGUMENTS_REGEX = "(" + START_ARGUMENTS + ")([^\n]*)";
        const string HISTORY = "CommandHistory=";
        const string FILE_LIST = "FileList=";        
        const string HISTORY_REGEX = "(" + HISTORY + ")([^\n]*)";
        const string FILE_LIST_REGEX = "(" + FILE_LIST + ")([^\n]*)";        

        static readonly string filePath = Path.Combine(Directory.GetCurrentDirectory(),CONFIG_FILE);

        /// <summary>
        /// Save started parameters
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool SaveStarters(string[] args)
        {
            string cfgRecord = START_ARGUMENTS + String.Join(";", args);

            string cfg = ReadConfigFileAndPrepareSave(START_ARGUMENTS_REGEX, cfgRecord);

            if (string.IsNullOrEmpty(cfg))
                cfg = cfgRecord;

            return SaveConfigFile(cfg);
        }

        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool SaveHistory(IEnumerable<string> args)
        {
            string cfgRecord = HISTORY + String.Join(";", args);

            string cfg = ReadConfigFileAndPrepareSave(HISTORY, cfgRecord);

            if (string.IsNullOrEmpty(cfg))
                cfg = cfgRecord;

            return SaveConfigFile(cfg);
        }

        /// <summary>
        /// Save file list
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static bool SaveFileList(IEnumerable<string> args)
        {
            string cfgRecord = FILE_LIST + String.Join(";", args);

            string cfg = ReadConfigFileAndPrepareSave(FILE_LIST, cfgRecord);

            if (string.IsNullOrEmpty(cfg))
                cfg = cfgRecord;

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
        /// <param name="newConfigRecord"></param>
        /// <returns></returns>
        private static string ReadConfigFileAndPrepareSave(string itemName, string newConfigRecord)
        {
            string cfg = "";

            if (File.Exists(filePath))
            {
                try
                {
                    using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    if (fs.Length > 0)
                    {
                        using (TextReader sr = new StreamReader(fs, Encoding.UTF8))
                        {
                            cfg = sr.ReadToEnd();
                        }

                        Regex rg = new Regex(itemName);
                        if (rg.IsMatch(cfg))
                            cfg = rg.Replace(cfg, newConfigRecord);
                        else
                            cfg += "\n" + newConfigRecord;
                    }
                }
                catch (FileNotFoundException ex)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while open config file. {ex}");
                }
            }
            return cfg;
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
        public static string[]? LoadFileList()
        {
            string? cfg = ReadConfiguration();

            if (!string.IsNullOrEmpty(cfg))
            {
                Regex rg = new Regex(FILE_LIST_REGEX);

                MatchCollection mc = rg.Matches(cfg);
                if (mc.Count > 0)
                {
                    string cfgLine = mc[0].Groups[2].Value;

                    return cfgLine.Split(';');
                }
            }

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
