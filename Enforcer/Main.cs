using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Task = System.Threading.Tasks.Task;


namespace Enforcer
{
    public partial class Main : Form
    {

        [DllImport("user32.dll")]
        public extern static bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);
        bool lockRunning = true;
        Config config = Config.Instance;
        List<FileStream> filePadlocks = new List<FileStream>();

        public Main(string[] args)
        {
            Text = "CleanBrowsing Enforcer";
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
                ShutdownBlockReasonCreate(Handle, "Lock is active.");
                InitializeLock();
            }
        }

        DateTime? GetNetworkTime()
        {
            DateTime? networkDateTime = null;
            try
            {
                const string ntpServer = "pool.ntp.org";
                var ntpData = new byte[48];
                ntpData[0] = 0x1B;
                var addresses = Dns.GetHostEntry(ntpServer).AddressList;
                var ipEndPoint = new IPEndPoint(addresses[0], 123);
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(ipEndPoint);
                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
                ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
                ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];
                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                networkDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);
            }
            catch (SocketException) { }
            return networkDateTime;
        }

        bool IsExpired()
        {
            DateTime.TryParse(config.Read("date-locked"), out DateTime parsedDateLocked);
            DateTime expirationDate = parsedDateLocked.AddDays(int.Parse(config.Read("days-locked")));
            DateTime? networkTime = GetNetworkTime();
            return networkTime == null ? false : networkTime >= expirationDate;
        }

        void InitializeLock()
        {
            filePadlocks.Add(new FileStream(config.ConfigFile, FileMode.Open, FileAccess.Read, FileShare.Read));
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
                filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            while (lockRunning)
            {
                if (IsExpired())
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

        int StartWatchdog()
        {
            using (Process executor = new Process())
            {
                executor.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEExecutor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "svchost.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }

        void InitializeWatchdog(string[] args)
        {
            Process watchdog = Process.GetProcessById(args.Length > 0 ? int.Parse(args[0]) : StartWatchdog());
            Task.Run(() =>
            {
                while (lockRunning)
                {
                    watchdog.WaitForExit();
                    watchdog = Process.GetProcessById(StartWatchdog());
                }
            });
        }

        void RegisterStartupTask()
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
                    taskDefinition.Actions.Add(new ExecAction(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEDaemon.exe")));
                    taskService.RootFolder.RegisterTaskDefinition("CleanBrowsing Enforcer", taskDefinition);
                }
                else
                    task.Enabled = true;
            }
        }

        void RemoveStartupTask()
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask("CleanBrowsing Enforcer");
            }
        }

        void SetCleanBrowsingDNS()
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

        void ResetDNS()
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

        NetworkInterface? GetActiveEthernetOrWifiNetworkInterface()
        {
            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                a => a.OperationalStatus == OperationalStatus.Up &&
                (a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                a.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily.ToString() == "InterNetwork"));
        }

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011)
                return;
            base.WndProc(ref aMessage);
        }
    }
}
