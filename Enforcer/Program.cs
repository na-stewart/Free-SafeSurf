namespace Enforcer
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.Run(new Main(args));
        }
    }
}