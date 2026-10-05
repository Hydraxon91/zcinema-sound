namespace ZCinemaSound.App;

internal static class Program
{
    // keep these alive for the process lifetime
    private static readonly Mutex InstanceMutex = new(false, @"Local\ZCinema_App");
    private static readonly EventWaitHandle ShowEvent = new(false, EventResetMode.AutoReset, @"Local\ZCinema_Show");

    [STAThread]
    private static void Main()
    {
        if (!InstanceMutex.WaitOne(0))
        {
            try { ShowEvent.Set(); } catch { } // ask the running instance to show itself
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
