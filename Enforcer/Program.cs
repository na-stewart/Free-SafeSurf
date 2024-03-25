namespace Enforcer
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "SSDaemon"))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                    Application.Run(new Main(args));
            }
        }
    }
}