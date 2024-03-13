using System.Diagnostics;

namespace Executor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Process process = new Process();
            process.StartInfo.FileName = args[0];
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            Console.WriteLine(process.Id);
            Environment.Exit(0);
        }
    }
}