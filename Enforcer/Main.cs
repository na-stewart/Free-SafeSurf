using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Globalization;
using System.Management;
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
        bool isLockActive = true;
        bool hostsSet = false;
        string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Config config = Config.Instance;
        List<FileStream> filePadlocks = new List<FileStream>();

        public Main(string[] args)
        {
            InitializeComponent();
            if (config.Read("days-enforced").Equals("0"))
            {
                RegisterStartupTask();
                SetCleanBrowsingDNS();
            }
            else
            {
                ShutdownBlockReasonCreate(Handle, "Enforcer is active.");
                InitializeWatchdog(args);
                InitializeLock();
            }
            Environment.Exit(0);
        }

        DateTime? GetNetworkTime()
        {
            DateTime? networkDateTime = null;
            try
            {
                var client = new TcpClient("time.nist.gov", 13);
                using (var streamReader = new StreamReader(client.GetStream()))
                {
                    var response = streamReader.ReadToEnd();
                    var utcDateTimeString = response.Substring(7, 17);
                    networkDateTime = DateTime.ParseExact(utcDateTimeString, "yy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                }
            }
            catch (SocketException) { }
            return networkDateTime;
        }

        bool IsExpired()
        {
            DateTime.TryParse(config.Read("date-enforced"), out DateTime parsedDateEnforced);
            var networkTime = GetNetworkTime();
            var expirationDate = parsedDateEnforced.AddDays(int.Parse(config.Read("days-enforced")));
            return networkTime == null ? false : networkTime >= expirationDate;
        }

        void InitializeLock()
        {
            filePadlocks.Add(new FileStream(config.ConfigFile, FileMode.Open, FileAccess.Read, FileShare.Read));
            foreach (var file in Directory.GetFiles(exePath, "*", SearchOption.AllDirectories))
                filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            while (isLockActive)
            {
                if (IsExpired())
                {              
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    RemoveStartupTask();
                    isLockActive = false;
                }
                else
                {
                    if (config.Read("disable-powershell").Equals("yes"))
                        killPowerShell();               
                    SetCleanBrowsingDNS();
                    TryToSetHosts();
                    RegisterStartupTask();
                }
                Thread.Sleep(4000);
            }
        }
      
        int StartWatchdog()
        {
            using (Process executor = new Process())
            {
                executor.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(exePath, "svchost.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }

        void killPowerShell()
        {
            foreach (Process process in Process.GetProcesses())
            {
                if (!string.IsNullOrEmpty(process.MainWindowTitle) && process.MainWindowTitle.Contains("Windows PowerShell"))
                    process.Kill();
            }
        }

        void InitializeWatchdog(string[] args)
        {
            Process watchdog = Process.GetProcessById(args.Length > 0 ? int.Parse(args[0]) : StartWatchdog());
            Task.Run(() =>
            {
                while (isLockActive)
                {
                    watchdog.WaitForExit();
                    watchdog.Close();
                    watchdog = Process.GetProcessById(StartWatchdog());
                }
            });
        }

        void RegisterStartupTask()
        {
            using (var taskService = new TaskService())
            {
                var task = taskService.GetTask("SafeSurf");
                if (task != null)
                    taskService.RootFolder.DeleteTask("SafeSurf");
                var taskDefinition = taskService.NewTask();
                taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                taskDefinition.RegistrationInfo.Description = "Runs SafeSurf on startup.";
                taskDefinition.RegistrationInfo.Author = "github.com/na-stewart";
                taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                taskDefinition.Triggers.Add(new LogonTrigger());
                taskDefinition.Triggers.Add(new TimeTrigger()
                {
                    StartBoundary = DateTime.Now,
                    Repetition = new RepetitionPattern(TimeSpan.FromMinutes(1), TimeSpan.Zero)
                });
                taskDefinition.Actions.Add(new ExecAction(Path.Combine(exePath, "SSDaemon.exe")));
                taskService.RootFolder.RegisterTaskDefinition("SafeSurf", taskDefinition);
            }
        }

        void RemoveStartupTask()
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask("SafeSurf");
            }
        }

        void SetCleanBrowsingDNS()
        {
            try
            {
                string[]? dns;
                if (config.Read("cleanbrowsing-dns-filter").Equals("off"))
                    dns = null;
                else if (config.Read("cleanbrowsing-dns-filter").Equals("family"))
                    dns = ["185.228.168.168", "185.228.169.168"];
                else
                    dns = ["185.228.168.10", "185.228.169.11"];
                var currentInterface = GetActiveEthernetOrWifiNetworkInterface();
                if (currentInterface == null) return;
                foreach (ManagementObject objMO in new ManagementClass("Win32_NetworkAdapterConfiguration").GetInstances())
                {
                    if ((bool)objMO["IPEnabled"])
                    {
                        if (objMO["Description"].Equals(currentInterface.Description))
                        {
                            var objdns = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                            if (objdns != null)
                            {
                                objdns["DNSServerSearchOrder"] = dns;
                                objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                            }
                        }
                    }
                }
            }
            catch (FileLoadException) { }
        }

        NetworkInterface? GetActiveEthernetOrWifiNetworkInterface()
        {
            return NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                a => a.OperationalStatus == OperationalStatus.Up &&
                (a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                a.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily.ToString().Equals("InterNetwork")));
        }

        void TryToSetHosts()
        {
            try
            {
                if (!hostsSet)
                {
                    if (config.Read("hosts-filter") == "off")
                        File.WriteAllText("C:\\WINDOWS\\System32\\drivers\\etc\\hosts", "");
                    else
                        File.WriteAllText("C:\\WINDOWS\\System32\\drivers\\etc\\hosts", File.ReadAllText(Path.Combine(exePath, $"{config.Read("hosts-filter")}.hosts")));
                    filePadlocks.Add(new FileStream("C:\\WINDOWS\\System32\\drivers\\etc\\hosts", FileMode.Open, FileAccess.Read, FileShare.Read));
                    hostsSet = true;
                }
            }
            catch (IOException) { }
        }

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011)
                return;
            base.WndProc(ref aMessage);
        }
    }
}
