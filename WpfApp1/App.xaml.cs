using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowException("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        }

        private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
            ShowException("AppDomainUnhandledException", ex);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowException("UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private void ShowException(string source, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                if (Current?.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ShowException(source, ex);
                }
                else
                {
                    MessageBox.Show($"[{source}] {ex}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}
