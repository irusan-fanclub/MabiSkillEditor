using System;
using System.Windows.Threading;
using MabiSkillEditor.Core.Services;

namespace MabiSkillEditor;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Log.Section($"App 啟動 v{ver?.Major}.{ver?.Minor}.{ver?.Build} pid={Environment.ProcessId} appdir={ConfigService.AppDir}");

        DispatcherUnhandledException                       += OnUiException;
        AppDomain.CurrentDomain.UnhandledException         += OnDomainException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnTaskException;

        base.OnStartup(e);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Log.Info($"App 結束 exitCode={e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private static void OnUiException(object s, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("DispatcherUnhandledException", e.Exception);
    }

    private static void OnDomainException(object s, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) Log.Error("AppDomain.UnhandledException", ex);
        else Log.Error($"AppDomain.UnhandledException: {e.ExceptionObject}");
    }

    private static void OnTaskException(object? s, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
