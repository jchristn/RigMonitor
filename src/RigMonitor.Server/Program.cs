namespace RigMonitor.Server
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program
    {
        private const string _StartupBanner =
@"      _                             _ _             
 _ __(_) __ _ _ __ ___   ___  _ __ (_) |_ ___  _ __ 
| '__| |/ _` | '_ ` _ \ / _ \| '_ \| | __/ _ \| '__|
| |  | | (_| | | | | | | (_) | | | | | || (_) | |   
|_|  |_|\__, |_| |_| |_|\___/|_| |_|_|\__\___/|_|   
        |___/                                       ";

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args)
        {
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                int shutdownRequested = 0;
                List<IDisposable> signalRegistrations = new List<IDisposable>();
                RigMonitorServer? server = null;

                void RequestShutdown()
                {
                    if (Interlocked.Exchange(ref shutdownRequested, 1) != 0)
                    {
                        return;
                    }

                    if (!cancellationTokenSource.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }

                ConsoleCancelEventHandler cancelKeyPressHandler = (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    RequestShutdown();
                };

                EventHandler processExitHandler = (sender, eventArgs) =>
                {
                    RequestShutdown();
                };

                Action<AssemblyLoadContext> unloadingHandler = context =>
                {
                    RequestShutdown();
                };

                Console.CancelKeyPress += cancelKeyPressHandler;
                AppDomain.CurrentDomain.ProcessExit += processExitHandler;
                AssemblyLoadContext.Default.Unloading += unloadingHandler;

                if (!OperatingSystem.IsWindows())
                {
                    signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, (context) =>
                    {
                        context.Cancel = true;
                        RequestShutdown();
                    }));
                    signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGINT, (context) =>
                    {
                        context.Cancel = true;
                        RequestShutdown();
                    }));
                    signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGQUIT, (context) =>
                    {
                        context.Cancel = true;
                        RequestShutdown();
                    }));
                    signalRegistrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGHUP, (context) =>
                    {
                        context.Cancel = true;
                        RequestShutdown();
                    }));
                }

                string settingsFile = ResolveSettingsFile(args);
                Console.Write(Environment.NewLine + _StartupBanner + Environment.NewLine + Environment.NewLine);
                server = await RigMonitorServer.CreateAsync(settingsFile, cancellationTokenSource.Token).ConfigureAwait(false);

                try
                {
                    await server.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                    await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    Console.CancelKeyPress -= cancelKeyPressHandler;
                    AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
                    AssemblyLoadContext.Default.Unloading -= unloadingHandler;

                    foreach (IDisposable registration in signalRegistrations)
                    {
                        registration.Dispose();
                    }

                    if (server != null)
                    {
                        server.Stop();
                    }
                }
            }
        }

        private static string ResolveSettingsFile(string[] args)
        {
            string? environmentPath = Environment.GetEnvironmentVariable("RIGMONITOR_SETTINGS_FILE");
            if (!String.IsNullOrWhiteSpace(environmentPath))
            {
                return environmentPath;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (String.Equals(args[i], "--settings", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return Constants.DefaultSettingsFilename;
        }
    }
}
