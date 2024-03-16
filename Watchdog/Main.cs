using System.Diagnostics;
using System.Reflection;

namespace Watchdog
{
    public partial class Main : Form
    {
        public Main(string[] args)
        {
            Text = "CBEWatchdog";
            Process enforcer = Process.GetProcessById(int.Parse(args[0]));
            while (true)
            {
                enforcer.WaitForExit();
                enforcer = Process.GetProcessById(StartEnforcer());
            }
        }

        int StartEnforcer()
        {
            Process executor = new Process();
            executor.EnableRaisingEvents = true;
            executor.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEExecutor.exe");
            executor.StartInfo.Arguments = $"\"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEDaemon.exe")}\" {Process.GetCurrentProcess().Id}";
            executor.StartInfo.RedirectStandardOutput = true;
            executor.Start();
            return int.Parse(executor.StandardOutput.ReadLine());
        }
    }
}
