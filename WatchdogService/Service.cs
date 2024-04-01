using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Watchdog;

namespace WatchdogService
{
    public partial class Service : ServiceBase
    {
        string exePath = Config.Instance.Read("path");
        bool active;

        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Task.Run(() =>
            {
                Process runningDaemon = args.Length > 0 ? Process.GetProcessById(int.Parse(args[0])) : StartDaemon();
                while (active)
                {
                    runningDaemon.WaitForExit();
                    runningDaemon.Close();
                    runningDaemon = StartDaemon();
                }
            });
        }

        Process StartDaemon()
        {
            using (Process executor = new Process())
            {
                executor.StartInfo.FileName = Path.Combine(exePath, "SSDaemon.exe");
                executor.Start();
                return executor;
            }
        }

        protected override void OnStop()
        {
            active = false;
        }
    }
}
