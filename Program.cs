using Microsoft.Win32;

namespace CargoPlus;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (DeskInstall.HandOff(args))
            return;

        var mutex = new Mutex(false, @"Local\CargoPlus.Desk");
        var owned = false;
        try
        {
            try
            {
                owned = mutex.WaitOne(TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            if (!owned)
                return;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            FocusMark.Install();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, args) =>
            {
                TelemetryLog.Add(args.Exception, "UI");
                CrashGuard.ShowThreadException(args.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                    TelemetryLog.Add(exception, "App");
            };

            Autostart.Register();
            var store = new LocalStore();
            try
            {
                store.Init();
            }
            catch (Exception ex)
            {
                MessageBox.Show(UiText.LocalDbFailed + ex.Message, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var settings = store.GetSettings();
                if (string.IsNullOrWhiteSpace(settings.LogoPath))
                {
                    var dir = Path.GetDirectoryName(Environment.ProcessPath);
                    var bundled = dir is null ? "" : Path.Combine(dir, "logo.png");
                    if (File.Exists(bundled))
                    {
                        settings.LogoPath = BrandLogo.Import(bundled);
                        store.SaveSettings(settings);
                    }
                }

                BrandLogo.Publish(settings.LogoPath);
            }
            catch (Exception)
            {
            }

            Application.ApplicationExit += (_, _) => AppTray.Dispose();
            using var telemetry = new TelemetryService(store);
            Application.Run(new AppHost(store));
        }
        finally
        {
            if (owned)
                mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}

internal sealed class AppHost : ApplicationContext
{
    private readonly LocalStore _store;

    public AppHost(LocalStore store)
    {
        _store = store;
        ShowLogin();
    }

    private void ShowLogin()
    {
        AppLanguage.Use(_store.GetSettings());
        var form = new LoginForm(_store);
        form.FormClosed += (_, _) =>
        {
            var user = form.EnteredUser;
            Later(() =>
            {
                if (user is not null)
                    ShowMain(user);
                else
                    ExitThread();
            });
        };
        form.Show();
    }

    private static void Later(Action next)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            next();
        };
        timer.Start();
    }

    private void ShowMain(AppUser user)
    {
        MainForm form;
        try
        {
            form = new MainForm(_store, user);
        }
        catch (Exception ex)
        {
            try
            {
                var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CargoPlus", "startup-error.txt");
                File.WriteAllText(log, ex.ToString());
            }
            catch (IOException)
            {
            }

            MessageBox.Show(UiText.OpenFailed + ex.Message, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
            return;
        }

        form.FormClosed += (_, _) =>
        {
            var again = form.LogoutRequested;
            Later(() =>
            {
                if (again)
                    ShowLogin();
                else
                    ExitThread();
            });
        };
        form.Show();
    }
}

internal static class CrashGuard
{
    private static int _showing;

    public static void ShowThreadException(Exception exception)
    {
        if (Interlocked.Exchange(ref _showing, 1) == 1)
            return;

        try
        {
            MessageBox.Show(exception.Message, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Interlocked.Exchange(ref _showing, 0);
        }
    }
}

internal static class Autostart
{
    public static void Register()
    {
        var path = Environment.ProcessPath;
        if (path is null || !path.EndsWith("CargoPlus.exe", StringComparison.OrdinalIgnoreCase))
            return;

        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        key?.SetValue("CargoPlus", $"\"{path}\"");
    }
}
