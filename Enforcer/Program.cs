using System.Diagnostics;

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
                {
                    try
                    {
                        Application.Run(new Main(args));
                    } catch (Exception ex)
                    {
                        LogException(ex); 
                    }
                }              
            }
        }

        static void LogException(Exception ex)
        {
            var eventLogName = "Application";
            var eventLogSource = "SafeSurf";
            if (!EventLog.Exists(eventLogSource))
                EventLog.CreateEventSource(eventLogSource, eventLogName);
            EventLog eventLog = new EventLog(eventLogName);
            eventLog.Source = eventLogSource;
            eventLog.WriteEntry(ex.StackTrace, EventLogEntryType.Error);
        }   
    }
}