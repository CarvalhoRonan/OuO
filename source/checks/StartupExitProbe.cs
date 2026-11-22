using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

static class StartupExitProbe {
    static IEnumerable<DependencyObject> Children(DependencyObject value) {
        yield return value;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
            foreach (var child in Children(VisualTreeHelper.GetChild(value, i))) yield return child;
    }

    [STAThread] static int Main(string[] args) {
        string output = args[1], mode = args[2];
        var errors = new List<string>();
        bool dialogSeen = false, closed = false, initialized = false;
        var app = new Application();
        Window main = null;
        app.DispatcherUnhandledException += (s, e) => { errors.Add(e.Exception.ToString()); e.Handled = true; app.Shutdown(2); };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timeout.Tick += (s, e) => { errors.Add("Timed out waiting for startup exit."); timeout.Stop(); app.Shutdown(3); };
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => {
            var dialog = s as Window;
            if (dialog == null || (dialog.Title != "Antes de começar" && dialog.Title != "Before you start") || dialogSeen) return;
            dialogSeen = true;
            dialog.ContentRendered += (sender, rendered) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
                if (mode == "close") dialog.Close();
                else Children((DependencyObject)dialog.Content).OfType<Button>().Single(x => x.IsCancel).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }));
        }));
        int code = 1;
        try {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type = assembly.GetType("Aurora.MainWindow", true);
            main = (Window)Activator.CreateInstance(type, new object[] { false, args.Contains("--tray") });
            main.Closed += (s, e) => {
                closed = true;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                initialized = (bool)type.GetField("startupAccepted", flags).GetValue(main) ||
                    ((IDictionary)type.GetField("activeJobs", flags).GetValue(main)).Count != 0 ||
                    type.GetField("timer", flags).GetValue(main) != null || type.GetField("trayIcon", flags).GetValue(main) != null;
            };
            timeout.Start();
            code = app.Run(main);
        } catch (Exception error) { errors.Add(error.ToString()); }
        finally { timeout.Stop(); }
        bool passed = code == 0 && dialogSeen && closed && !initialized && errors.Count == 0;
        File.WriteAllText(output, (passed ? "PASS" : "FAIL") + " startup refusal mode=" + mode +
            "; dialog=" + dialogSeen + "; closed=" + closed + "; hardware-started=" + initialized +
            "; code=" + code + Environment.NewLine + String.Join(Environment.NewLine, errors));
        return passed ? 0 : 1;
    }
}
