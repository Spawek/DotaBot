using System;
using System.Collections.Generic;
using System.Text;

namespace DotaBot
{
    static class Logger
    {
        public static void Log(string msg)
        {
            Console.WriteLine(msg);
            System.Diagnostics.Trace.TraceInformation(msg);
        }
    }
}
