using System.Diagnostics;
using System.Reflection;

namespace UI
{
    internal class Program
    {
        static bool navigating = true;
        static string navMessage = "";
        static int navIndex = 0;
        static Option[] options = {
            new Option("DNS Filter", ["off", "adult", "family", "secure"]),
            new Option("Days Locked", ["0", "1", "7", "14", "30", "60", "365"]),
            new Option("Activate", Activate),
        };
        static ValueTuple<int, int> initialPos = Console.GetCursorPosition();
        static Config config = Config.Instance;


        static void Main(string[] args)
        {
            Console.Title = "CleanBrowsing Enforcer";
            while (navigating)
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
            Console.SetCursorPosition(initialPos.Item1, initialPos.Item2);
            for (int i = 0; i < Console.WindowHeight; i++)
                Console.WriteLine(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(initialPos.Item1, initialPos.Item2);
            Console.WriteLine(@"
              ____ _                  ____                        _             
             / ___| | ___  __ _ _ __ | __ ) _ __ _____      _____(_)_ __   __ _ 
            | |   | |/ _ \/ _` | '_ \|  _ \| '__/ _ \ \ /\ / / __| | '_ \ / _` |
            | |___| |  __/ (_| | | | | |_) | | | (_) \ V  V /\__ \ | | | | (_| |
             \____|_|\___|\__,_|_| |_|____/|_|  \___/ \_/\_/ |___/_|_| |_|\__, |
            | ____|_ __  / _| ___  _ __ ___ ___ _ __                      |___/ 
            |  _| | '_ \| |_ / _ \| '__/ __/ _ \ '__|                           
            | |___| | | |  _| (_) | | | (_|  __/ |                              
            |_____|_| |_|_|  \___/|_|  \___\___|_|                              
            
            "
            );
            Console.WriteLine("Blocks explicit content and prohibits circumvention.");
            Console.WriteLine("https://github.com/na-stewart/CleanBrowsing-Enforcer\n");
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

        static void Activate()
        {
            try
            {
                foreach (Option option in options)
                    if (!option.isExecutable())
                        config.Write(option.Name, option.ToString());
                if (options[0].ToString().Equals("off"))
                    navMessage = "CleanBrowsing disabled.";
                else
                {
                    config.Write("Date Locked", DateTime.Now.ToString());
                    navMessage = "CleanBrowsing activated!";
                    Process process = new Process();
                    process.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEExecutor.exe");
                    process.StartInfo.Arguments = $"\"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CBEDaemon.exe")}\"";
                    process.Start();
                    process.WaitForExit();
                    PrintNav();
                }
            }
            catch (IOException ex)
            {
                DateTime.TryParse(config.Read("date-locked"), out DateTime parsedDateLocked);
                navMessage = $"Enforcer is locked! No changes can be made until {parsedDateLocked.AddDays(int.Parse(config.Read("days-locked")))}.";
            }
        }
    }
}