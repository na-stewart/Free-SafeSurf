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
        readonly static string? exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly static ValueTuple<int, int> initialCursorPos = Console.GetCursorPosition();
        readonly static Config config = Config.Instance;
        readonly static Option[] options = [
            new Option("Hosts Filter", ["off", "adult", "gambling", "family"]),
            new Option("CleanBrowsing DNS Filter", ["off", "adult", "family"]),
            new Option("Days Enforced", ["0", "1", "7", "14", "30", "60", "180", "365"]),
            new Option("Execute", Execute),
            new Option("Help", () => Process.Start(new ProcessStartInfo("https://github.com/na-stewart/SafeSurf/blob/master/README.md") { UseShellExecute = true }))
        ];
        static string notification = string.Empty;
        static int navIndex = 0;

        static void Main()
        {
            Console.Title = "SafeSurf";
            Process.Start(new ProcessStartInfo("powershell") 
            {
                CreateNoWindow = true,
                Verb = "runas",
                Arguments = $" -Command Add-MpPreference -ExclusionPath '{exePath}'"
            }); // Prevents closure via Windows Defender.
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
   | (___   ____| |_ ___  | (___  _   _ _ __| |_ 
    \___ \ / __ |  _/ _ \  \___ \| | | | '__|  _|
    ____) | (_| | ||  __/  ____) | |_| | |  | |  
   |_____/ \____|_| \___| |_____/ \____|_|  |_|                                 
            "
            );
            Console.WriteLine("Blocks harmful content and prohibits circumvention.");
            Console.WriteLine("Navigate via arrow and enter keys.\n");
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
                foreach (var option in options)
                    if (!option.IsExecutable())
                        config.Write(option.Name, option.ToString());
                if (!options[2].ToString().Equals("0"))
                {
                    if (options[0].ToString().Equals("off") && options[1].ToString().Equals("off"))
                    {
                        notification = "Atleast one filter must be configured for enforcer activation.";
                        return;
                    }
                    config.Write("date-enforced", DateTime.Now.ToString());
                } 
                notification = "SafeSurf settings applied!";
                Process.Start(new ProcessStartInfo(Path.Combine(exePath, "SSExecutor.exe"))
                {
                    CreateNoWindow = true,
                    Arguments = $"\"{Path.Combine(exePath, "SSDaemon.exe")}\""
                }); // Instances started in a separate path from an active enforcer are ignored and won't raise an error.
            }
            catch (IOException) 
            {
                DateTime.TryParse(config.Read("date-enforced"), out DateTime dateEnforced);
                notification = $"SafeSurf enforcer is active! No changes may be made until {dateEnforced.AddDays(int.Parse(config.Read("days-enforced"))).ToShortDateString()}.";
            }
        }
    } 
}