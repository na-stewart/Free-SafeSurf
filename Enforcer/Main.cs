using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Task = System.Threading.Tasks.Task;

/*
MIT License

Copyright (c) 2024 Nicholas Aidan Stewart

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace Enforcer
{
    public partial class Main : Form
    {
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);
        readonly ManagementEventWatcher powershellWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = 'powershell.exe'"));
        readonly string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly Config config = Config.Instance;
        readonly List<FileStream> filePadlocks = [];
        bool isEnforcerActive = true;
        Process watchdog;

        public Main(string[] args)
        {
            InitializeComponent();
            AddDefenderExclusion();
            if (config.Read("days-enforced").Equals("0"))
            {
                isEnforcerActive = false;
                SetCleanBrowsingDNS();
                SetHosts();
            }
            else
            {
                ShutdownBlockReasonCreate(Handle, "Enforcer is active.");
                InitializePowershellKiller();
                InitializeWatchdog(args);
                SetHosts();
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
            catch (ArgumentOutOfRangeException) { }
            return networkDateTime;
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
                    isEnforcerActive = false;
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    using (var taskService = new TaskService()) 
                        taskService.RootFolder.DeleteTask("SafeSurf");
                    powershellWatcher.Stop();
                    watchdog.Kill();
                    continue;
                }
                else
                {  
                    SetCleanBrowsingDNS();      
                    RegisterStartupTask();       
                }
                Thread.Sleep(4000);
                AddDefenderExclusion();
            }
        }

        bool IsExpired()
        {
            DateTime.TryParse(config.Read("date-enforced"), out DateTime parsedDateEnforced);
            var networkTime = GetNetworkTime();
            var expirationDate = parsedDateEnforced.AddSeconds(int.Parse(config.Read("days-enforced")));
            return networkTime == null ? false : networkTime >= expirationDate;
        }

        int StartWatchdog()
        {
            using (Process executor = new Process())
            {
                executor.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(exePath, "svchost.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.CreateNoWindow = true;
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }

        void InitializeWatchdog(string[] args)
        {
            watchdog = Process.GetProcessById(args.Length > 0 ? int.Parse(args[0]) : StartWatchdog());
            Task.Run(() =>
            {
                while (isEnforcerActive)
                {
                    watchdog.WaitForExit();
                    watchdog.Close();
                    if (!isEnforcerActive) 
                        continue;
                    watchdog = Process.GetProcessById(StartWatchdog());
                }
            });
        }

        void InitializePowershellKiller()
        {
            powershellWatcher.EventArrived += (object sender, EventArrivedEventArgs e) =>
            {
                try
                {
                    Process.GetProcessById(Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value)).Kill();
                }
                catch (ArgumentException) { }
            };
            powershellWatcher.Start();
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
                if (!isEnforcerActive && config.Read("cleanbrowsing-dns-filter").Equals("off"))
                    dns = null;
                else if (config.Read("cleanbrowsing-dns-filter").Equals("family"))
                    dns = ["185.228.168.168", "185.228.169.168"];
                else if (config.Read("cleanbrowsing-dns-filter").Equals("adult"))
                    dns = ["185.228.168.10", "185.228.169.11"];
                else
                    return;
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
            var filterHosts = Path.Combine(exePath, $"{config.Read("hosts-filter")}.hosts");
            var hosts = "C:\\WINDOWS\\System32\\drivers\\etc\\hosts";
            try
            {
                if (config.Read("hosts-filter") == "off")
                {
                    if (!isEnforcerActive)
                        File.WriteAllText("C:\\WINDOWS\\System32\\drivers\\etc\\hosts", string.Empty);
                }
                else
                {
                    File.WriteAllText("C:\\WINDOWS\\System32\\drivers\\etc\\hosts", File.ReadAllText(filterHosts));
                    filePadlocks.Add(new FileStream(hosts, FileMode.Open, FileAccess.Read, FileShare.Read));
                }
            }
            catch (IOException) { }
        }

        void AddDefenderExclusion()
        {
            var powershell = new ProcessStartInfo("powershell")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas",
                Arguments = $" -Command Add-MpPreference -ExclusionPath '{exePath}'"
            };
            Process.Start(powershell);
        }

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011)
                return;
            base.WndProc(ref aMessage);
        }
    }
}
