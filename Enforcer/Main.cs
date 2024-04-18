using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Task = System.Threading.Tasks.Task;
using Timer = System.Timers.Timer;

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
        readonly string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        readonly string? exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly Timer expirationTimer = new(86400000);
        readonly List<FileStream> filePadlocks = [];
        readonly Config config = Config.Instance;
        readonly string watchdogPath;
        readonly string daemonPath;
        bool isEnforcerActive;
        Process watchdog;
        bool isExpired;

        public Main(string[] args)
        {
            InitializeComponent();
            watchdogPath = Path.Combine(windowsPath, "svchost.exe");
            daemonPath = Path.Combine(exePath, "SSDaemon.exe");
            if (config.Read("days-enforced").Equals("0"))
            {
                SetHostsFilter();
                SetCleanBrowsingDnsFilter();
            }        
            else 
            {
                isEnforcerActive = true;
                AddDefenderExclusion(exePath); // Prevents closure via Windows Defender.
                InitializeWatchdog(args); // Watchdog prevents closure of enforcer by immediately reopening it.       
                ShutdownBlockReasonCreate(Handle, "Enforcer is active."); // Prevents closure via logout.
                SetHostsFilter();      
                InitializeEnforcer(); // Applies SafeSurf settings repeatedly to prevent circumvention.
            }
            Environment.Exit(0);
        }

        void InitializeWatchdog(string[] args)
        {
            AddDefenderExclusion(watchdogPath.Replace("exe", "dll"));
            if (args.Length > 0)
                watchdog = Process.GetProcessById(int.Parse(args[0]));
            else
            {
                try
                {
                    foreach (string file in Directory.GetFiles(exePath, "*svchost*")) // Moves watchdog to prevent closure via console.
                        File.Move(file, Path.Combine(windowsPath, Path.GetFileName(file)), true);
                }
                catch (IOException) { }
                watchdog = Process.GetProcessById(StartWatchdog());
            }
            Task.Run(() =>
            {
                while (isEnforcerActive)
                {
                    watchdog.WaitForExit();
                    watchdog.Close();
                    if (isEnforcerActive)
                        watchdog = Process.GetProcessById(StartWatchdog());
                }
            });
            foreach (string file in Directory.GetFiles(windowsPath, "*svchost*")) // Prevents deletion of critical files.
                filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        int StartWatchdog()
        {
            using Process executor = new();
            executor.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
            executor.StartInfo.Arguments = $"\"{watchdogPath}\" {Environment.ProcessId} \"{exePath}\"";
            executor.StartInfo.CreateNoWindow = true;
            executor.StartInfo.RedirectStandardOutput = true;
            executor.Start();
            var executorResponse = executor.StandardOutput.ReadLine();
            return executorResponse == null ? throw new NullReferenceException("No pid returned from executor.") : int.Parse(executorResponse);
        }

        void SetHostsFilter()
        {
            var hosts = Path.Combine(windowsPath, "System32\\drivers\\etc\\hosts");
            try
            {
                if (config.Read("hosts-filter").Equals("off"))
                {
                    if (!isEnforcerActive)
                        File.WriteAllText(hosts, string.Empty);
                }
                else
                {
                    File.WriteAllText(hosts, File.ReadAllText(Path.Combine(exePath, $"{config.Read("hosts-filter")}.hosts")));
                    if (isEnforcerActive)
                        filePadlocks.Add(new FileStream(hosts, FileMode.Open, FileAccess.Read, FileShare.Read));
                }
            }
            catch (IOException) { }
        }

        void InitializeEnforcer()
        {
            ExpirationCheck();
            foreach (string path in new string[] { exePath, RuntimeEnvironment.GetRuntimeDirectory() })
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) // Prevents deletion of critical files.
                    filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            while (isEnforcerActive)
            {
                if (isExpired)
                {
                    expirationTimer.Stop();
                    isEnforcerActive = false;
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    using var taskService = new TaskService();
                    GetTaskFolder(taskService).DeleteTask("SvcStartup", false);
                    watchdog.Kill();
                }
                else
                {
                    SetDaemonFilePermissions(); // Prevents closure via permissions override and restart.
                    RegisterTask("SvcStartup"); // Windows task opens SafeSurf on Windows login.
                    SetCleanBrowsingDnsFilter();   
                    Thread.Sleep(4000);
                }
            }
        }

        void ExpirationCheck()
        {
            DateTime? networkDateTime = null;
            try
            {
                using var tcpClient = new TcpClient();
                if (tcpClient.ConnectAsync("time.nist.gov", 13).Wait(500)) // Network time prevents closure via system date override.
                {
                    using var streamReader = new StreamReader(tcpClient.GetStream());
                    networkDateTime = DateTime.ParseExact(streamReader.ReadToEnd().Substring(7, 17), "yy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                }
            }
            catch (AggregateException) { }
            DateTime.TryParse(config.Read("date-enforced"), out DateTime dateEnforced);
            isExpired = networkDateTime != null && networkDateTime >= dateEnforced.AddDays(int.Parse(config.Read("days-enforced")));
            if (!expirationTimer.Enabled && !isExpired)
            {
                expirationTimer.AutoReset = true;
                expirationTimer.Elapsed += (_, _) => ExpirationCheck();
                expirationTimer.Start();
            }
        }

        void SetCleanBrowsingDnsFilter()
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
                if (currentInterface != null)
                {
                    foreach (ManagementObject objMO in new ManagementClass("Win32_NetworkAdapterConfiguration").GetInstances().Cast<ManagementObject>())
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

        void SetDaemonFilePermissions()
        {
            var daemon = new FileInfo(daemonPath);
            var daemonSecurity = daemon.GetAccessControl();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            daemonSecurity.RemoveAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.FullControl, AccessControlType.Deny));
            daemonSecurity.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.FullControl, AccessControlType.Allow));
            daemon.SetAccessControl(daemonSecurity);
        }

        void RegisterTask(string name)
        {
            using var taskService = new TaskService();
            var taskFolder = GetTaskFolder(taskService);
            taskFolder.DeleteTask(name, false);
            var taskDefinition = taskService.NewTask();
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.RegistrationInfo.Author = "Microsoft Corporation"; // Disguised to prevent deletion.
            taskDefinition.RegistrationInfo.Description = "Ensures all critical Windows service processes are running.";
            taskDefinition.Principal.UserId = "NT AUTHORITY\\SYSTEM";
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
            taskDefinition.Triggers.Add(new LogonTrigger());
            taskDefinition.Actions.Add(new ExecAction(daemonPath));
            taskFolder.RegisterTaskDefinition(name, taskDefinition);
        }

        TaskFolder GetTaskFolder(TaskService taskService)
        {
            TaskFolder? taskFolder = null;
            try
            {
                taskFolder = taskService.GetFolder("\\Microsoft\\Windows\\Maintenance");
            }
            catch (NotV1SupportedException) { }
            taskFolder ??= taskService.RootFolder;
            return taskFolder;
        }

        void AddDefenderExclusion(string path)
        {
            using Process powershell = new();
            powershell.StartInfo.FileName = "powershell";
            powershell.StartInfo.UseShellExecute = false;
            powershell.StartInfo.CreateNoWindow = true;
            powershell.StartInfo.Verb = "runas";
            powershell.StartInfo.Arguments = $" -Command Add-MpPreference -ExclusionPath '{path}'";
            powershell.Start();
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011)
                return;
            base.WndProc(ref aMessage);
        }
    }
}