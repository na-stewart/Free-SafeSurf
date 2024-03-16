namespace Watchdog
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "CBEWatchdog"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                    Application.Run(new Main(args));
            }
        }
    }
}