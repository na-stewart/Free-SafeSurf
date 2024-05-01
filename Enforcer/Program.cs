using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;

namespace Enforcer
{
    internal static class Program
    {

        static bool lockRunning = true;
        static Config config = Config.Instance;
        static List<FileStream> filePadlocks = new List<FileStream>();

        public static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "cbe_enforcer"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    if (config.Read("days-locked").Equals("0"))
                    {
                        if (config.Read("dns-filter").Equals("off"))
                        {
                            ResetDNS();
                            RemoveStartupTask();
                        }
                        else
                        {
                            SetCleanBrowsingDNS();
                            RegisterStartupTask();
                        }
                    }
                    else
                    {
                        InitializeWatchdog(args);
                        InitializeLock();
                       
                    }
                       
                }
            }
        }

        static bool isExpired()
        {
            DateTime.TryParse(config.Read("date-locked"), out DateTime parsedDateLocked);
            DateTime expirationDate = parsedDateLocked.AddDays(int.Parse(config.Read("days-locked")));
            return DateTime.Now >= expirationDate;
        }

        static void InitializeLock()
        {         
            filePadlocks.Add(new FileStream(config.ConfigFile, FileMode.Open, FileAccess.Read, FileShare.Read));
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
                filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            while (lockRunning)
            {
                if (isExpired())
                {
                    ResetDNS();
                    RemoveStartupTask();
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    lockRunning = false;
                }
                else
                {
                    SetCleanBrowsingDNS();
                    RegisterStartupTask();
                    Thread.Sleep(3000);
                }
                Thread.Sleep(3000);
            }
        }

        static int StartWatchdog()
        {
            string path = "C:\\Users\\Aidan Stewart\\source\\repos\\CleanBrowsing-Enforcer\\bin";
            using (Process executor = new Process())
            {
                executor.StartInfo.FileName = Path.Combine(path, "Executor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(path, "Watchdog.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }

        static void InitializeWatchdog(string[] args)
        {
            if (!isExpired())
            {
                Process watchdog = Process.GetProcessById(args.Length > 0 ? int.Parse(args[0]) : StartWatchdog());
                watchdog.EnableRaisingEvents = true;
                watchdog.Exited += (sender, e) =>
                {
                    InitializeWatchdog([]);
                };
            }
        }

        static void RegisterStartupTask()
        {
            using (var taskService = new TaskService())
            {

                var task = taskService.GetTask("CleanBrowsing Enforcer");
                if (task == null)
                {
                    var taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = "Runs CleanBrowsing Enforcer on startup.";
                    taskDefinition.RegistrationInfo.Author = "github.com/na-stewart";
                    taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                    taskDefinition.Triggers.Add(new LogonTrigger());
                    taskDefinition.Actions.Add(new ExecAction(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Enforcer.exe")));
                    taskService.RootFolder.RegisterTaskDefinition("CleanBrowsing Enforcer", taskDefinition);
                }
                else
                    task.Enabled = true;
            }
        }

        static void RemoveStartupTask()
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask("CleanBrowsing Enforcer");
            }
        }

        static void SetCleanBrowsingDNS()
        {
            try
            {
                string[] Dns = { "185.228.168.10", "185.228.169.11" };
                var CurrentInterface = GetActiveEthernetOrWifiNetworkInterface();
                while (CurrentInterface == null)
                    CurrentInterface = GetActiveEthernetOrWifiNetworkInterface();
                if (CurrentInterface == null) return;
                var objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
                var objMOC = objMC.GetInstances();
                foreach (ManagementObject objMO in objMOC)
                {
                    if ((bool)objMO["IPEnabled"])
                    {
                        if (objMO["Description"].Equals(CurrentInterface.Description))
                        {
                            var objdns = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                            if (objdns != null)
                            {
                                objdns["DNSServerSearchOrder"] = Dns;
                                objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                            }
                        }
                    }
                }
            }
            catch (FileLoadException) { }
        }

        static void ResetDNS()
        {
            var currentInterface = GetActiveEthernetOrWifiNetworkInterface();
            if (currentInterface == null) return;
            var objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            var objMOC = objMC.GetInstances();
            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    if (objMO["Description"].Equals(currentInterface.Description))
                    {
                        var objdns = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                        if (objdns != null)
                        {
                            objdns["DNSServerSearchOrder"] = null;
                            objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                        }
                    }
                }
            }
        }

        static NetworkInterface? GetActiveEthernetOrWifiNetworkInterface()
        {
            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                a => a.OperationalStatus == OperationalStatus.Up &&
                (a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                a.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily.ToString() == "InterNetwork"));
        }

    }
}