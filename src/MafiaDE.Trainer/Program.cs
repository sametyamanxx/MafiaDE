using System.Reflection;

namespace MafiaDE.Trainer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test"))
            return SelfTests.Run(args);

        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\MafiaDE.Trainer", out bool first);
        if (!first)
        {
            MessageBox.Show("Uygulama zaten açık. Pencereyi göstermek için Insert tuşuna bas.",
                "Mafia: Definitive Edition", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Mafia DE • Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new TrainerWindow(args.Contains("--hidden")));
        return 0;
    }

    public static Stream Asset(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream($"MafiaDE.Trainer.Assets.{name}")
        ?? throw new InvalidOperationException($"Gömülü dosya bulunamadı: {name}");
}
