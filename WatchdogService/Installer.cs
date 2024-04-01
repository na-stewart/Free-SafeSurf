using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WatchdogService
{
    public static class Installer
    {
        private static readonly string exePath = Assembly.GetExecutingAssembly().Location;
        public static void Install()
        {
            ManagedInstallerClass.InstallHelper(new string[] { exePath });
        }

        public static void Uninstall()
        {
            ManagedInstallerClass.InstallHelper(new string[] { "/u", exePath });
        }
    }
}
