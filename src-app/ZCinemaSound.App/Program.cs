using ZCinemaSound.Core;

namespace ZCinemaSound.App;

internal static class Program
{
    // keep these alive for the process lifetime
    private static readonly Mutex InstanceMutex = new(false, @"Local\ZCinema_App");
    private static readonly EventWaitHandle ShowEvent = new(false, EventResetMode.AutoReset, @"Local\ZCinema_Show");

    [STAThread]
    private static void Main()
    {
        var args = Environment.GetCommandLineArgs();
        var cmd = args.Length > 1 ? args[1].TrimStart('-', '/').ToLowerInvariant() : "";
        var quiet = args.Any(a => a.TrimStart('-', '/').Equals("quiet", StringComparison.OrdinalIgnoreCase));

        // headless setup verbs (run elevated by the UI / installer)
        if (cmd is "install" or "uninstall")
        {
            var msg = cmd == "install" ? Setup.Install() : Setup.Uninstall();
            if (quiet)
            {
                try { Console.WriteLine(msg); } catch { }
                Environment.ExitCode = msg.StartsWith("Equalizer APO config folder not found", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
            else
            {
                MessageBox.Show(msg, "ZCinema Sound", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }

        if (!InstanceMutex.WaitOne(0))
        {
            try { ShowEvent.Set(); } catch { } // ask the running instance to show itself
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
