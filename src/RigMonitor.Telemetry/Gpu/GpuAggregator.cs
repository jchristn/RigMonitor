namespace RigMonitor.Telemetry.Gpu
{
    using System.Collections.Generic;
    using System.Linq;
    using RigMonitor.Core.Enums;
    using RigMonitor.Core.Models;

    /// <summary>
    /// Builds the vendor-neutral gpu section from the vendor sections.
    /// </summary>
    public static class GpuAggregator
    {
        /// <summary>
        /// Combine vendor sections. Device objects are shared with the vendor sections; ordinals are assigned here.
        /// </summary>
        /// <param name="nvidia">NVIDIA section.</param>
        /// <param name="amd">AMD section.</param>
        /// <param name="apple">Apple section.</param>
        /// <param name="qualcomm">Qualcomm section.</param>
        /// <returns>Aggregate, or null when no vendor reported a device.</returns>
        public static GpuTelemetry? Build(NvidiaGpuTelemetry? nvidia, AmdGpuTelemetry? amd, AppleGpuTelemetry? apple, QualcommGpuTelemetry? qualcomm)
        {
            List<GpuDeviceTelemetry> devices = new List<GpuDeviceTelemetry>();
            if (nvidia != null) devices.AddRange(nvidia.Devices.OrderBy(device => device.DeviceIndex));
            if (amd != null) devices.AddRange(amd.Devices.OrderBy(device => device.DeviceIndex));
            if (apple != null) devices.AddRange(apple.Devices.OrderBy(device => device.DeviceIndex));
            if (qualcomm != null) devices.AddRange(qualcomm.Devices.OrderBy(device => device.DeviceIndex));

            if (devices.Count < 1)
            {
                return null;
            }

            for (int i = 0; i < devices.Count; i++)
            {
                devices[i].Ordinal = i;
            }

            List<GpuVendorEnum> vendors = devices.Select(device => device.Vendor).Distinct().ToList();
            return new GpuTelemetry
            {
                Vendor = vendors.Count == 1 ? vendors[0] : GpuVendorEnum.Mixed,
                Vendors = vendors,
                Devices = devices
            };
        }
    }
}
