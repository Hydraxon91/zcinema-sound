namespace ZCinemaSound.App;

internal static class Program
{
    // keep the mutex alive for the process lifetime
    private static readonly Mutex InstanceMutex = new(false, @"Local\ZCinema_App");

    [STAThread]
    private static void Main()
    {
        if (!InstanceMutex.WaitOne(0)) return; // already running
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
