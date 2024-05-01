using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;

namespace Executor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Process process = new Process();
            process.StartInfo.FileName = args[0];
            if (args.Length > 1)
                process.StartInfo.Arguments = args[1];
            process.Start();
            Console.WriteLine(process.Id);
            Environment.Exit(0);
        }
    }
}