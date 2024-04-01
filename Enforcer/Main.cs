using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32.TaskScheduler;
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
        readonly string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly Config config = Config.Instance;
        readonly List<FileStream> filePadlocks = new List<FileStream> ();
        bool isEnforcerActive = true;
        Process watchdog;

        public Main(string[] args)
        {
            InitializeComponent();
            if (config.Read("motivation").Equals("on"))
                ShowMotivation();    
            if (config.Read("days-enforced").Equals("0"))
            {
                isEnforcerActive = false;
                SetHosts();
                SetCleanBrowsingDNS();
            }
            else
            {
                InitializeWatchdog(args);
                SetHosts();
                AddDefenderExclusion();
                ShutdownBlockReasonCreate(Handle, "Enforcer is active.");     
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
                    watchdog.Kill();
                    continue;
                }
                else
                {  
                    SetCleanBrowsingDNS();      
                    RegisterStartupTask();       
                }
                Thread.Sleep(4000);
            }
        }

        bool IsExpired()
        {
            DateTime.TryParse(config.Read("date-enforced"), out DateTime parsedDateEnforced);
            var networkTime = GetNetworkTime();
            var expirationDate = parsedDateEnforced.AddDays(int.Parse(config.Read("days-enforced")));
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

        void RegisterStartupTask()
        {
            using (var taskService = new TaskService())
            {
                taskService.RootFolder.DeleteTask("SafeSurf", false);
                var taskDefinition = taskService.NewTask();
                taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                taskDefinition.RegistrationInfo.Description = "SafeSurf startup and heartbeat task.";
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
                    dns = new string[] { "185.228.168.168", "185.228.169.168" };
                else if (config.Read("cleanbrowsing-dns-filter").Equals("adult"))
                    dns = new string[] { "185.228.168.10", "185.228.169.11" };
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
                if (config.Read("hosts-filter").Equals("off"))
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

        public void ShowMotivation()
        {
            string[] quotes = new string[] { 
                "You either suffer the pain of discipline or the pain or regret.",
                "The clock is ticking. Are you becoming the person you want to be?",
                "Treat each day as a new life and at once begin to live while you still can.",
                "If you quit now, suddenly years will pass and your regrets will be far behind you.",
                "There are no regrets in life, just lessons. It's time to do the right by yourself.",
                "Don't let this lead to regret that will live with you the rest of your life.",
                "This is nothing to be proud of. Do something worthy of your time.",
                "The only person you are destined to become is who you decide to be.",
                "I’m not telling you it is going to be easy. I’m telling you it’s going to be worth it! Get up and live!",
                "Hardships often prepare ordinary people for extraordinary things. Find strength within and live with no regret.",
                "Be stronger than your strongest excuse, or suffer the consequences.",
                "Success is the sum of small efforts, repeated day in and day out. Continue towards a life of no regret.",
                "It won't be like this forever, take advantage of now and do right by yourself.",
                "Regret born from ill-fated choices will surpasses all other hardships."
            };
            new ToastContentBuilder().AddText("SafeSurf").AddText(quotes[new Random().Next(0, quotes.Count())]).Show();
        }

        protected override void WndProc(ref Message aMessage)
        {
            if (aMessage.Msg == 0x0011)
                return;
            base.WndProc(ref aMessage);
        }
    }
}
