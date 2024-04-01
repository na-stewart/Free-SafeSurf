using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WatchdogService
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args != null && args.Length == 1 && args[0].Length > 1
               && (args[0][0] == '-' || args[0][0] == '/'))
            {
                switch (args[0].Substring(1).ToLower())
                {
                    default:
                        break;
                    case "install":
                    case "i":
                        Installer.Install();
                        break;
                    case "uninstall":
                    case "u":
                        Installer.Uninstall();
                        break;
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new Service()
                };
                ServiceBase.Run(ServicesToRun);
            }
          
        }
    }
}
