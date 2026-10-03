namespace RigMonitor.Telemetry.Platform.Shared
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using RigMonitor.Core.Observability;

    /// <summary>
    /// Executes child processes for telemetry collection.
    /// </summary>
    internal static class ProcessCommandRunner
    {
        internal static async Task<string?> RunAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken)
        {
            if (String.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            string executableName = Path.GetFileName(fileName);
            using (IntegrationCall call = IntegrationCall.Start(RigMonitorTelemetryNames.ServiceProcess, executableName, null, ActivityKind.Internal))
            {
                call.SetTag("process.executable.name", executableName);

                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };

                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        process.Start();

                        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                        string standardOutput = await standardOutputTask.ConfigureAwait(false);
                        string standardError = await standardErrorTask.ConfigureAwait(false);

                        call.SetTag("process.exit.code", process.ExitCode);
                        if (process.ExitCode != 0)
                        {
                            call.Fail(RigMonitorTelemetryNames.OutcomeError, "Process exited with code " + process.ExitCode + ".");
                        }
                        else
                        {
                            call.Succeed();
                        }

                        if (process.ExitCode != 0 && String.IsNullOrWhiteSpace(standardOutput))
                        {
                            if (String.IsNullOrWhiteSpace(standardError))
                            {
                                return null;
                            }

                            return standardError.Trim();
                        }

                        return standardOutput.Trim();
                    }
                }
                catch (Exception exception)
                {
                    call.Fail(exception, cancellationToken);
                    throw;
                }
            }
        }
    }
}
