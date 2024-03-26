using System.Diagnostics;
using System.Reflection;

namespace UI
{
    internal class Program
    {
        static string navMessage = "";
        static string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        static int navIndex = 0;
        static Option[] options = {
            new Option("Hosts Filter", ["off", "adult", "gambling", "family"]),
            new Option("CleanBrowsing DNS Filter", ["off", "adult", "family"]),
            new Option("Disable PowerShell", ["yes", "no"]),
            new Option("Days Enforced", ["0", "1", "7", "14", "30", "60", "365"]),
            new Option("Execute", Execute),
            new Option("Help", () => Process.Start(new ProcessStartInfo("https://github.com/na-stewart/SafeSurf/blob/master/README.md") { UseShellExecute = true }))
          
        };
        static ValueTuple<int, int> initialCursorPos = Console.GetCursorPosition();
        static Config config = Config.Instance;


        static void Main(string[] args)
        {
            Console.Title = "Safe Surf";
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
            Console.WriteLine($"\n{navMessage}");
        }

        static void Execute()
        {
            try
            {
                foreach (Option option in options)
                    if (!option.isExecutable())
                        config.Write(option.Name, option.ToString());
                config.Write("date-enforced", DateTime.Now.ToString());       
                Process process = new Process();
                process.StartInfo.FileName = Path.Combine(exePath, "SSExecutor.exe");
                process.StartInfo.Arguments = $"\"{Path.Combine(exePath, "SSDaemon.exe")}\"";
                process.Start();
                process.WaitForExit();
                navMessage = "Safe Surf executed successfully!";
                PrintNav();
            }
            catch (IOException)
            {
                DateTime.TryParse(config.Read("date-enforced"), out DateTime parsedDateEnforced);
                navMessage = $"Safe Surf enforcer is active! No changes can be made until {parsedDateEnforced.AddDays(int.Parse(config.Read("days-enforced")))}.";
            }
        }
    }
}