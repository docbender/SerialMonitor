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
      const string START_ARGUMENTS_REGEX="(" + START_ARGUMENTS + ")([^\n]*)";

#if __MonoCS__
      static string filePath = Directory.GetCurrentDirectory() + "/" + CONFIG_FILE;
#else
      static string filePath = Directory.GetCurrentDirectory() + "\\" + CONFIG_FILE;
#endif

      /// <summary>
      /// Save started parameters
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>
      public static bool Save(string[] args)
      {
         string cfgRecord = START_ARGUMENTS + String.Join(";", args);

         string cfg = "";

         if(File.Exists(filePath))
         {
            try
            {
               using(FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
               {
                  if(fs.Length > 0)
                  {
                     using(TextReader sr = new StreamReader(fs, Encoding.UTF8))
                     {
                        cfg = sr.ReadToEnd();
                     }

                     Regex rg = new Regex(START_ARGUMENTS_REGEX);
                     if(rg.IsMatch(cfg))
                     {
                        cfg = rg.Replace(cfg, cfgRecord);
                     }
                  }
               }
            }
            catch(FileNotFoundException ex)
            {
            }
            catch(Exception ex)
            {
               Console.WriteLine("Error while open config file. " + ex.ToString());
            }
         }

         if(cfg.Length == 0)
            cfg = cfgRecord;

         try
         {
            using(FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
               using(StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
               {
                  sw.Write(cfg);
                  sw.Flush();
                  sw.Close();
               }

               fs.Close();
            }
         }
         catch(System.IO.IOException ex)
         {
            Console.WriteLine("Error (IOException) accessing config file. " + ex.ToString());
            return false;
         }
         catch(Exception ex)
         {
            Console.WriteLine("Error accessing config file. " + ex.ToString());
            return false;
         }


         return true;
      }

      /// <summary>
      /// Load saved start configuration
      /// </summary>
      /// <returns></returns>
      public static string[] Load()
      {
         string[] configArgs = null;

         if(File.Exists(filePath))
         {
            try
            {
               using(FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
               {
                  if(fs.Length > 0)
                  {
                     string cfg;

                     using(TextReader sr = new StreamReader(fs, Encoding.UTF8))
                     {
                        cfg = sr.ReadToEnd();
                     }

                     Regex rg = new Regex(START_ARGUMENTS_REGEX);

                     MatchCollection mc = rg.Matches(cfg);
                     if(mc.Count > 0)
                     {
                        string cfgLine = mc[0].Groups[2].Value;

                        return cfgLine.Split(';');
                     }
                  }
               }
            }
            catch(FileNotFoundException ex)
            {
               return null;
            }
            catch(Exception ex)
            {
               Console.WriteLine("Error while open config file. " + ex.ToString());
               return null;
            }
         }

         return configArgs;
      }
   }
}
