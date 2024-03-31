using System.Diagnostics;
using System.Reflection;

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

namespace Watchdog
{
    internal static class Program
    {
        static string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "SSWatchdog"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    Process enforcer = Process.GetProcessById(int.Parse(args[0]));
                    while (true)
                    {  
                        try
                        {
                            using (File.Open(Path.Combine(exePath, "SafeSurf.exe"), FileMode.Open, FileAccess.Write, FileShare.None))
                            {
                                break;
                            }
                        }
                        catch (IOException ex)
                        {
                            enforcer.WaitForExit();
                            enforcer.Close();
                            enforcer = Process.GetProcessById(StartDaemon());
                        }
                    }
                }
            }
        }
        
        static int StartDaemon()
        {  
            using (Process executor = new Process())
            {           
                executor.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
                executor.StartInfo.Arguments = $"\"{Path.Combine(exePath, "SSDaemon.exe")}\" {Process.GetCurrentProcess().Id}";
                executor.StartInfo.RedirectStandardOutput = true;
                executor.Start();
                return int.Parse(executor.StandardOutput.ReadLine());
            }
        }
    }
}