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
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);
        readonly string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly Config config = Config.Instance;
        readonly List<FileStream> filePadlocks = [];
        bool isEnforcerActive = true;

        public Main(string[] args)
        {
            InitializeComponent();
            if (config.Read("days-enforced").Equals("0"))
            {        
                SetCleanBrowsingDNS();
                SetHosts();
                RegisterStartupTask();
            }
            else
            {
                ShutdownBlockReasonCreate(Handle, "Enforcer is active.");
                InitializeLock();
                InitializeWatchdog(args);
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
            catch (FormatException) { }
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
            while (isEnforcerActive)
            {
                if (IsExpired())
                {              
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    using (var taskService = new TaskService())
                    {
                        taskService.RootFolder.DeleteTask("SafeSurf");
                    }
                    isEnforcerActive = false;
                }
                else
                {
                    if (config.Read("disable-powershell").Equals("yes"))
                    {
                        foreach (Process process in Process.GetProcesses())
                        {
                            if (!string.IsNullOrEmpty(process.MainWindowTitle) && process.MainWindowTitle.Contains("PowerShell"))
                                process.Kill();
                        }
                    }
                    SetCleanBrowsingDNS();
                    SetHosts();
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

        void InitializeWatchdog(string[] args)
        {
            Process watchdog = Process.GetProcessById(args.Length > 0 ? int.Parse(args[0]) : StartWatchdog());
            Task.Run(() =>
            {
                while (isEnforcerActive)
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
                taskService.RootFolder.DeleteTask("SafeSurf", false);
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

        void SetHosts()
        {
            try
            {
                var filterHosts = Path.Combine(exePath, $"{config.Read("hosts-filter")}.hosts");
                var hosts = "C:\\WINDOWS\\System32\\drivers\\etc\\hosts";
                if (config.Read("hosts-filter") == "off")
                    File.WriteAllText(hosts, "");
                else
                {
                    using (StreamReader filterHostsReader = new StreamReader(filterHosts))
                    using (StreamReader hostsReader = new StreamReader(hosts))
                    using (StreamWriter hostsWriter = new StreamWriter(hosts, false))
                    {
                        if (!hostsReader.ReadLine().Contains("StevenBlack/hosts"))
                            hostsWriter.WriteLine(filterHostsReader.ReadToEnd());
                    }
                }
                filePadlocks.Add(new FileStream(hosts, FileMode.Open, FileAccess.Read, FileShare.Read));
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
