using System.Diagnostics;
using System.Reflection;

namespace Watchdog
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "CBEWatchdog"))
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
                executor.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEExecutor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEDaemon.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }
    }
}