namespace Enforcer
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using (var mutex = new Mutex(false, "cbe_daemon"))
            {
                Application.Run(new Main(args));
            }
        }
    }
}