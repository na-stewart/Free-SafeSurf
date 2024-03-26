using System.Diagnostics;
using System.Reflection;

namespace Watchdog
{
    internal static class Program
    {
        static string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "SSWatchdog"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    Process enforcer = Process.GetProcessById(int.Parse(args[0]));
                    while (true)
                    {
                        enforcer.WaitForExit();
                        enforcer.Close();
                        enforcer = Process.GetProcessById(StartDaemon());
                    }
                }
            }
        }
        
        static int StartDaemon()
        {  
            using (Process executor = new Process())
            {           
                executor.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(exePath, "SSDaemon.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }
    }
}