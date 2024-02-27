using Microsoft.Win32.TaskScheduler;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using Task = System.Threading.Tasks.Task;
using System.Timers;
using Timer = System.Timers.Timer;


namespace svchost
{
    internal static class Program
    {
        static bool enforcementTaskRunning = false;
        static Config config = Config.Instance;
        static List<FileStream> filePadlocks = new List<FileStream>();


        [STAThread]
        static void Main()
        {
            using (var mutex = new Mutex(false, "CleanBrowsingEnforcer SingletonApp"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    InitializeEnforcement();
                    SetShutdownTimer();
                }
            }
        }

        static void RegisterStartupTask()
        {
            using (var taskService = new TaskService())
            {
                var task = taskService.GetTask("SvcHost Manager");
                if (task == null)
                {
                    var taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = "Ensures critical Windows SvcHost processes are running.";
                    taskDefinition.RegistrationInfo.Author = "Microsoft Cooperation";
                    taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                    taskDefinition.Triggers.Add(new LogonTrigger());
                    taskDefinition.Triggers.Add(new TimeTrigger()
                    {
                        StartBoundary = DateTime.Now,
                        Repetition = new RepetitionPattern(TimeSpan.FromMinutes(1), TimeSpan.Zero)
                    });
                    taskDefinition.Actions.Add(new ExecAction(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "svchost.exe")));
                    taskService.RootFolder.RegisterTaskDefinition("SvcHost Manager", taskDefinition);
                }
                else
                {
                    task.Enabled = true;
                    if (task.State != TaskState.Running)
                        task.Run();
                }            
            }
        }

        static void SetShutdownTimer()
        {
            Timer timer = new Timer();
            timer.Interval = (int)(int.Parse(config.Read("Days Locked")) * 24L * 60 * 60 * 1000); // I think my math is fucked.
            timer.Elapsed += (object source, ElapsedEventArgs e) => InitializeEnforcement(); 
            timer.Enabled = true;
        }

        static void InitializeEnforcement()
        {
            var dateLocked = config.Read("Date Locked");
            DateTime.TryParse(dateLocked, out DateTime parsedDateLocked);          
            if (DateTime.Now >= parsedDateLocked.AddDays(int.Parse(config.Read("Days Locked"))))
            {
                enforcementTaskRunning = false;
                ResetDNS();
                foreach (var filePadlock in filePadlocks)
                    filePadlock.Close();  
            }
            else
            {
                foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
                    filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
                Task.Run(() =>
                {
                    while (enforcementTaskRunning)
                    {
                        SetCleanBrowsingDNS();
                        RegisterStartupTask();
                        Thread.Sleep(3000);
                    }
                });
            }        
        }

        static void SetCleanBrowsingDNS()
        {
            try
            {
                string[] Dns = { "185.228.168.10", "185.228.169.11" };
                var CurrentInterface = GetActiveEthernetOrWifiNetworkInterface();
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
            var CurrentInterface = GetActiveEthernetOrWifiNetworkInterface();
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
                            objdns["DNSServerSearchOrder"] = null;
                            objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                        }
                    }
                }
            }
        }

        static NetworkInterface GetActiveEthernetOrWifiNetworkInterface()
        {
            var Nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                a => a.OperationalStatus == OperationalStatus.Up &&
                (a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                a.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily.ToString() == "InterNetwork"));
            return Nic;
        }
    }
}