using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

public abstract class GpuRegistryOptimization : BaseOptimization
{
    protected abstract GpuVendor Vendor { get; }

    protected abstract IReadOnlyList<RegistryItem> CreateItems(string registryPath);

    public override Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    )
    {
        foreach (var gpu in context.Snapshot.Gpus.Where(g => g.Vendor == Vendor))
        {
            // The WMI VideoControllerN ordinal is unrelated to the display class registry key.
            // Resolve the PnP device's actual driver registration and fail closed.
            if (!Gpu.TryResolveDriverClassPath(gpu.PnpDeviceId, out var path))
            {
                context.Logger.LogWarning(
                    "No verified driver key for GPU {GpuName} ({PnpDeviceId}); skipping tweak",
                    gpu.Name,
                    gpu.PnpDeviceId
                );
                continue;
            }

            RegistryService.Write(context, CreateItems(path).ToArray());
            context.Logger.LogInformation(
                "Applied {Optimization} for GPU {DeviceId}",
                GetType().Name,
                gpu.PnpDeviceId
            );
        }

        return Task.FromResult(context.Changes.ToApplyResult());
    }
}

[OptimizationCategory(typeof(GpuOptimizerPage))]
public class Gpu : LocalizedObject, IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(Gpu)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Gpu;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    private const string DisplayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>
    /// Parse a PnP driver's registered display-class key without relying on WMI enumeration order.
    /// </summary>
    public static bool TryParseDriverClassRegistration(string? registration, out string classSubkey)
    {
        classSubkey = string.Empty;
        if (string.IsNullOrWhiteSpace(registration))
            return false;

        var prefix = DisplayClassGuid + @"\";
        if (!registration.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var index = registration[prefix.Length..];
        if (index.Length != 4 || !index.All(char.IsAsciiDigit))
            return false;

        classSubkey = @"SYSTEM\CurrentControlSet\Control\Class\" + registration;
        return true;
    }

    /// <summary>
    /// Resolve a display adapter's real registry key from Enum\PnPDeviceId\Driver.
    /// Missing, malformed, or inaccessible mappings are skipped, never guessed.
    /// </summary>
    internal static bool TryResolveDriverClassPath(string? pnpDeviceId, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(pnpDeviceId))
            return false;

        try
        {
            using var device = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Enum\" + pnpDeviceId
            );
            var registration = device?.GetValue("Driver") as string;
            if (!TryParseDriverClassRegistration(registration, out var classSubkey))
                return false;

            using var driver = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(classSubkey);
            if (driver is null)
                return false;

            path = @"HKLM\" + classSubkey;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.IO.IOException)
        {
            return false;
        }
    }

    [Optimization(
        Id = "4433CE93-B632-4D67-AE71-0241C31099C0",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Power,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableUlps : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "EnableULPS", 0)];
        }
    }

    [Optimization(
        Id = "A3836587-C461-4BEB-9C4C-88493B36C138",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisablePowerGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new RegistryItem(path, "DisablePowerGating", 1),
                new RegistryItem(path, "PP_GPUPowerDownEnabled", 0),
                new RegistryItem(path, "DisableDynamicPstate", 1),
            ];
        }
    }

    [Optimization(
        Id = "DB379BB5-FC8C-4AD3-BE32-11BE09E76FBA",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Performance,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableVideoClockGating : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new RegistryItem(path, "DisableVCEPowerGating", 1),
                new RegistryItem(path, "DisableVceClockGating", 1),
                new RegistryItem(path, "EnableUvdClockGating", 0),
                new RegistryItem(path, "EnableVceSwClockGating", 0),
            ];
        }
    }

    [Optimization(
        Id = "8A4B4C4D-000E-42DB-8BD7-293067F8FAE7",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Amd | OptimizationTags.Latency,
        Condition = typeof(AmdGpuCondition)
    )]
    public class AmdDisableAspm : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Amd;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return
            [
                new RegistryItem(path, "EnableAspmL0s", 0),
                new RegistryItem(path, "EnableAspmL1", 0),
            ];
        }
    }

    [Optimization(
        Id = "FA58802F-B98C-4E98-AA67-067C5A394296",
        Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Power,
        Condition = typeof(NvidiaGpuCondition)
    )]
    public class NvidiaDisableDynamicPstate : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Nvidia;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "DisableDynamicPstate", 1)];
        }
    }

    [Optimization(
        Id = "9E7702BE-9E03-4C96-A316-C4E435CB993E",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Nvidia | OptimizationTags.Performance,
        Condition = typeof(NvidiaGpuCondition)
    )]
    public class NvidiaDisableAsyncPstates : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Nvidia;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "DisableASyncPstates", 1)];
        }
    }

    [Optimization(
        Id = "DF8739FF-E322-4958-B369-DF584BB9B0C1",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Intel | OptimizationTags.Latency,
        Condition = typeof(IntelGpuCondition)
    )]
    public class IntelDisableAsyncFlips : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "Display1_DisableAsyncFlips", 1)];
        }
    }

    [Optimization(
        Id = "EBFB0DED-5D1A-4CD1-B5C0-4F7018DD6054",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Intel | OptimizationTags.Display,
        Condition = typeof(IntelGpuCondition)
    )]
    public class IntelDisableAdaptiveVsync : GpuRegistryOptimization
    {
        protected override GpuVendor Vendor => GpuVendor.Intel;

        protected override IReadOnlyList<RegistryItem> CreateItems(string path)
        {
            return [new RegistryItem(path, "AdaptiveVsyncEnable", 0)];
        }
    }
}
