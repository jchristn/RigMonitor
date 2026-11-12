namespace RigMonitor.Telemetry.Gpu.Amd.Native
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Seam over the ROCm AMD SMI native library.
    /// </summary>
    public interface IAmdSmiNative : IDisposable
    {
        /// <summary>
        /// Path of the loaded library, when loaded.
        /// </summary>
        string? LibraryPath { get; }

        /// <summary>
        /// Library version string, when loaded.
        /// </summary>
        string? LibraryVersion { get; }

        /// <summary>
        /// Load and initialize the library.
        /// </summary>
        /// <param name="error">Reason when initialization fails.</param>
        /// <returns>True when the library is initialized.</returns>
        bool TryInitialize(out string? error);

        /// <summary>
        /// Read every GPU.
        /// </summary>
        /// <returns>Per-device samples.</returns>
        IReadOnlyList<AmdSmiDeviceSample> Sample();
    }
}
