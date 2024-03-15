using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Enforcer
{
    internal static class Program
    {
        [STAThread]
        static void Main(String[] args)
        {
            Application.Run(new Main(args));
        }
    }
}