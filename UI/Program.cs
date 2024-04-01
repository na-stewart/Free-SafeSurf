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

namespace UI
{
    internal class Program
    {
        static string notification = "";
        static string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        static int navIndex = 0;
        static Option[] options = {
            new Option("Hosts Filter", ["off", "adult", "gambling", "family"]),
            new Option("CleanBrowsing DNS Filter", ["off", "adult", "family"]),
            new Option("Days Enforced", ["0", "1", "7", "14", "30", "60", "180", "365"]),
            new Option("Execute", Execute),
            new Option("Help", () => Process.Start(new ProcessStartInfo("https://github.com/na-stewart/SafeSurf/blob/master/README.md") { UseShellExecute = true }))
          
        };
        static ValueTuple<int, int> initialCursorPos = Console.GetCursorPosition();
        static Config config = Config.Instance;

        static void Main(string[] args)
        {
            Console.Title = "SafeSurf";
            AddDefenderExclusion();
            while (true)
            {
                PrintNav();
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    if (navIndex > 0)
                        navIndex--;
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    if (navIndex < options.Length - 1)
                        navIndex++;
                }
                else
                    options[navIndex].Select(keyInfo);
            }
        }

        static void PrintNav()
        {
            Console.SetCursorPosition(initialCursorPos.Item1, initialCursorPos.Item2);
            for (int i = 0; i < Console.WindowHeight; i++)
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(initialCursorPos.Item1, initialCursorPos.Item2);
            Console.WriteLine(@"
     _____        __        _____             __ 
    / ____|      / _|      / ____|           / _|
   | (___   __ _| |_ ___  | (___  _   _ _ __| |_ 
    \___ \ / _` |  _/ _ \  \___ \| | | | '__|  _|
    ____) | (_| | ||  __/  ____) | |_| | |  | |  
   |_____/ \__,_|_| \___| |_____/ \__,_|_|  |_|                                 
            "
            );
            Console.WriteLine("Blocks harmful content and prohibits circumvention.");
            Console.WriteLine("Naviagte via arrow and enter keys.\n");
            for (int i = 0; i < options.Length; i++)
            {
                if (i == navIndex)
                    Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(options[i].ToString() == null ? options[i].Name : $"{options[i].Name}: ");
                Console.ResetColor();
                Console.WriteLine(options[i].ToString());
            }
            Console.WriteLine($"\n{notification}");
        }

        static void Execute()
        {
            try
            {
                foreach (Option option in options)
                    if (!option.isExecutable())
                        config.Write(option.Name, option.ToString());
                if (options[0].ToString() == "off" && options[1].ToString() == "off")
                    config.Write("days-enforced", "0");
                else
                    config.Write("date-enforced", DateTime.Now.ToString());
                Process process = new Process();
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
                process.StartInfo.Arguments = $"\"{Path.Combine(exePath, "SSDaemon.exe")}\"";
                process.Start();
                notification = "SafeSurf settings applied successfully!";
            }
            catch (IOException)
            {
                DateTime.TryParse(config.Read("date-enforced"), out DateTime parsedDateEnforced);
                notification = $"SafeSurf enforcer is active! No changes can be made until {parsedDateEnforced.AddDays(int.Parse(config.Read("days-enforced")))}.";
            }
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
    }
}