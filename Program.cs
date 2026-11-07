using Microsoft.Win32;

namespace CargoPlus;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
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
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, args) => CrashGuard.ShowThreadException(args.Exception);

            Autostart.Register();
            var store = new LocalStore();
            try
            {
                store.Init();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть локальную базу: " + ex.Message, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

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
        var form = new LoginForm(_store);
        form.FormClosed += (_, _) =>
        {
            if (form.EnteredUser is { } user)
                ShowMain(user);
            else
                ExitThread();
        };
        form.Show();
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
            MessageBox.Show("Не удалось открыть программу.\n" + ex.Message, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
            return;
        }

        form.FormClosed += (_, _) =>
        {
            if (form.LogoutRequested)
                ShowLogin();
            else
                ExitThread();
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
