using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wonderland_Private_Server.Utilities
{
    public static class LogServices
    {
        public static void Log(object message)
        {
            Console.WriteLine(message);
        }
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }
        public static void Log(Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}
