using System.Reflection;

namespace MafiaDE.Trainer;

internal static class Program
{
    private const string ShowEventName = @"Local\MafiaDE.ShowWindow";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test"))
            return SelfTests.Run(args);

        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\MafiaDE.Trainer", out bool first);
        if (!first)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ShowEventName);
                foreach (var process in System.Diagnostics.Process.GetProcessesByName(
                    Path.GetFileNameWithoutExtension(Environment.ProcessPath)))
                {
                    using (process)
                        if (process.Id != Environment.ProcessId)
                            Native.AllowSetForegroundWindow(process.Id);
                }
                signal.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                MessageBox.Show("Eski sürüm zaten açık. Sistem tepsisinden eski MafiaDE'yi kapatıp yeni sürümü aç.",
                    "MafiaDE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("MafiaDE farklı yetki düzeyinde açık. Eski pencereyi kapatıp aynı yetkiyle yeniden aç.",
                    "MafiaDE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Mafia DE • Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        using var window = new TrainerWindow(args.Contains("--hidden"));
        _ = window.Handle;
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) =>
        {
            try
            {
                if (!window.IsDisposed && window.IsHandleCreated)
                    window.BeginInvoke(() => window.ShowFromShortcut());
            }
            catch (InvalidOperationException) { /* The window may be shutting down. */ }
        }, null, Timeout.Infinite, false);
        try { Application.Run(window); }
        finally { listener.Unregister(null); }
        return 0;
    }

    public static Stream Asset(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MafiaDE.Trainer.Assets.{name}")
        ?? throw new InvalidOperationException($"Gömülü dosya bulunamadı: {name}");
}
