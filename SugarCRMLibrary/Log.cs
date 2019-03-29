using System;
using System.Collections.Generic;
using System.Text;

namespace SugarCRMLibrary
{
    static public class Log
    {
        static public void Debug(string str) { Output($"[DEBUG]: {str}"); }
        static public void Info(string str) { Output($"[INFO ]: {str}"); }
        static public void Warning(string str) { Output($"[WARN ]: {str}"); }
        static public void Error(string str) { Output($"[ERROR]: {str}"); }
        static private void Output(string str)
        {
            Console.WriteLine(str);
            //string strDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //System.Diagnostics.Debug.WriteLine($"{strDateTime}: {str}");
        }
    }
}
