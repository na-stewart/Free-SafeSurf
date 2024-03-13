using System.Diagnostics;
using System.Reflection;

namespace Watchdog
{
    internal static class Program
    {
        //Needs to include process listening and override.
        static void Main()
        {
            using (var mutex = new Mutex(false, "cbe_watchdog"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    while (true)
                    {
                        Process process = new Process();
                        process.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Executor.exe");
                        process.StartInfo.Arguments = $"\"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Enforcer Daemon.exe")}\"";
                        process.Start();
                        Thread.Sleep(3000);
                    }
                }
            }
        }
    }
}