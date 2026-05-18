namespace RigMonitor.Telemetry.Platform.Shared
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

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
    }
}
