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
using System.ServiceProcess;
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
        readonly ServiceController defenderService = new("WdNisSvc");
        readonly Timer expirationTimer = new(1800000);
        readonly List<FileStream> filePadlocks = [];
        readonly Config config = Config.Instance;
        readonly string watchdogPath;
        readonly string daemonPath;
        Process watchdog;
        bool isExpired;
        bool isActive;

        public Main(string[] args)
        {
            InitializeComponent();
            watchdogPath = Path.Combine(windowsPath, "svchost.exe");
            daemonPath = Path.Combine(exePath, "SSDaemon.exe");
            if (config.Read("days-enforced").Equals("0"))
            {
                ApplyHostsFilter();
                ApplyCleanBrowsingDnsFilter();
            }
            else if (!config.HasExpired())
            {
                isActive = true;
                UpdateDefenderExclusions(false); // Prevents closure via Windows Defender.
                InitializeWatchdog(args); // Watchdog prevents closure of enforcer by immediately reopening it.       
                ShutdownBlockReasonCreate(Handle, "Enforcer is active, you may sign out anyway."); // Prevents closure via logout.
                ApplyHostsFilter();
                InitializeEnforcer(); // Applies SafeSurf settings repeatedly to prevent circumvention.
            }
            Environment.Exit(0);
        }

        void InitializeWatchdog(string[] args)
        {
            if (args.Length > 0)
                watchdog = Process.GetProcessById(int.Parse(args[0]));
            else
            {
                foreach (var file in Directory.GetFiles(exePath, "*svchost*")) // Watchdog copied to prevent closure via console.
                {
                    try
                    {
                        File.Copy(file, Path.Combine(windowsPath, Path.GetFileName(file)));
                    }
                    catch (IOException) { }
                }    
                watchdog = Process.GetProcessById(StartWatchdog());
            }
            Task.Run(() =>
            {
                while (isActive)
                {
                    watchdog.WaitForExit();
                    watchdog.Close();
                    if (isActive)
                        watchdog = Process.GetProcessById(StartWatchdog());
                }
            });
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

        void ApplyHostsFilter()
        {
            var hosts = Path.Combine(windowsPath, "System32\\drivers\\etc\\hosts");
            try
            {
                if (config.Read("hosts-filter").Equals("off"))
                {
                    if (!isActive)
                        File.WriteAllText(hosts, string.Empty);
                }
                else
                {
                    File.WriteAllText(hosts, File.ReadAllText(Path.Combine(exePath, $"{config.Read("hosts-filter")}.hosts")));
                    if (isActive)
                        filePadlocks.Add(new FileStream(hosts, FileMode.Open, FileAccess.Read, FileShare.Read)); // Prevents deletion of critical file.
                }
            }
            catch (IOException) { }
        }

        void InitializeEnforcer()
        {
            ExpirationCheck();
            while (isActive)
            {
                if (isExpired)
                {
                    isActive = false;
                    expirationTimer.Stop();               
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    config.SetExpired();
                    using var taskService = new TaskService();
                    var taskFolder = GetTaskFolder(taskService);
                    taskFolder.DeleteTask("SvcStartup", false);
                    taskFolder.DeleteTask("SvcHeartbeat", false);
                    UpdateDefenderExclusions(true);
                    watchdog.Kill();              
                }
                else
                {
                    ApplyFileLocks(); // Prevents closure via permissions override and restart.
                    RegisterTask("SvcStartup", new LogonTrigger()); // SafeSurf started on login.
                    RegisterTask("SvcHeartbeat", new TimeTrigger() { StartBoundary = DateTime.Now, Repetition = new RepetitionPattern(TimeSpan.FromMinutes(1), TimeSpan.Zero) });
                    ApplyCleanBrowsingDnsFilter();
                    Thread.Sleep(1500);
                    UpdateDefenderExclusions(false);
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
            catch (SocketException) { }
            catch (FormatException) { }
            if (networkDateTime != null)
            {
                DateTime.TryParse(config.Read("date-enforced"), out DateTime dateEnforced);
                isExpired = networkDateTime >= dateEnforced.AddDays(int.Parse(config.Read("days-enforced")));
            }
            if (!expirationTimer.Enabled && !isExpired)
            {
                expirationTimer.AutoReset = true;
                expirationTimer.Elapsed += (_, _) => ExpirationCheck();
                expirationTimer.Start();
            }
        }

        void ApplyCleanBrowsingDnsFilter()
        {
            try
            {
                string[]? dns;
                if (!isActive && config.Read("cleanbrowsing-dns-filter").Equals("off"))
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
                    foreach (var objMO in new ManagementClass("Win32_NetworkAdapterConfiguration").GetInstances().Cast<ManagementObject>())
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

        void ApplyFileLocks()
        {
            foreach (var file in Directory.GetFiles(windowsPath, "*svchost*"))
                SetFilePermissions(file);
            foreach (var path in new string[] { exePath, RuntimeEnvironment.GetRuntimeDirectory() })
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))        
                    SetFilePermissions(file);  
        }

        void SetFilePermissions(string path)
        {
            var file = new FileInfo(path);
            var fileSecurity = file.GetAccessControl();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            fileSecurity.RemoveAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.FullControl, AccessControlType.Deny));
            fileSecurity.SetAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            file.SetAccessControl(fileSecurity);
            if (!filePadlocks.Exists(fileStream => fileStream.Name.Equals(path)))
                filePadlocks.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)); // Prevents deletion of critical file.
        }

        void RegisterTask(string name, Trigger taskTrigger)
        {
            using var taskService = new TaskService();
            var taskFolder = GetTaskFolder(taskService);
            taskFolder.DeleteTask(name, false);
            var taskDefinition = taskService.NewTask();
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.RegistrationInfo.Author = "Microsoft Corporation"; // Disguised as a Windows task to prevent deletion.
            taskDefinition.RegistrationInfo.Description = "Ensures all critical Windows service processes are running.";
            taskDefinition.Principal.UserId = "NT AUTHORITY\\SYSTEM";
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
            taskDefinition.Triggers.Add(taskTrigger);
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

        void UpdateDefenderExclusions(bool remove)
        {
            if (defenderService.Status == ServiceControllerStatus.Running || remove)
            {
                Process.Start(new ProcessStartInfo("powershell")
                {
                    CreateNoWindow = true,
                    Verb = "runas",
                    Arguments = $" -Command {(remove ? "Remove" : "Add")}-MpPreference -ExclusionPath '{exePath}', '{watchdogPath}', '{watchdogPath.Replace(".exe", ".dll")}'"
                });
            }     
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011 || aMessage.Msg == 0x0016)
                return;
            base.WndProc(ref aMessage);
        }
    }
}
