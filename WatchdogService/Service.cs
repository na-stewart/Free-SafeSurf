using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Watchdog;

namespace WatchdogService
{
    public partial class Service : ServiceBase
    {
        Config config = Config.Instance;
        private Task monitoringTask;
        private CancellationTokenSource cancellationTokenSource;

        public Service()
        {
            InitializeComponent();
            cancellationTokenSource = new CancellationTokenSource();
        }

        protected override void OnStart(string[] args)
        {
            monitoringTask = Task.Run(() => MonitorDaemon(args), cancellationTokenSource.Token);
        }

        private void MonitorDaemon(string[] args)
        {
            Process runningDaemon = args.Length > 0 ? Process.GetProcessById(int.Parse(args[0])) : StartDaemon();
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                runningDaemon.WaitForExit();
                runningDaemon = StartDaemon();
            }
        }

        Process StartDaemon()
        {
            using (Process executor = new Process())
            {
                executor.StartInfo.FileName = Path.Combine(config.Read("path"), "SSDaemon.exe");
                executor.Start();
                return executor;
            }
        }

        protected override void OnStop()
        {
            // Signal the monitoring task to stop
            cancellationTokenSource.Cancel();
            try
            {
                // Give the task a bit of time to complete gracefully
                monitoringTask.Wait(5000);
            }
            catch (AggregateException ae)
            {
                // Handle any exceptions that were thrown by the task
                // This is a placeholder for exception handling logic
            }

            base.OnStop();
        }
    }
}
