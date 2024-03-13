
using System.Diagnostics;

namespace Executor
{
    internal class Program
    {

        static void Main(string[] args)
        {
            Process.Start(args[0]);
            Environment.Exit(0);
        }
    }
}