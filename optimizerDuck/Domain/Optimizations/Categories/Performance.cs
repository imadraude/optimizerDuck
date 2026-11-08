using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(PerformanceOptimizerPage))]
public class Performance : LocalizedObject, IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(Performance)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "648EC19A-FDA5-4607-8A7C-148B8B05FB4C",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram
    )]
    public class DisableBackgroundApps : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                context,
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                    "GlobalUserDisabled",
                    1
                ),
                new RegistryItem(
                    @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search",
                    "BackgroundAppGlobalToggle",
                    0
                )
            );
            context.Logger.LogInformation("Disabled background apps");
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "CD436A05-51F1-46E9-B4DE-5262EE7F812A",
        Risk = OptimizationRisk.Risky,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Ram
    )]
    public class ConsolidateServiceHosts : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            if (context.Snapshot.Memory.TotalKB <= 0)
            {
                context.Logger.LogInformation(
                    "Invalid RAM value: {RamTotalKB}. Skipping...",
                    context.Snapshot.Memory.TotalKB
                );
                return Task.FromResult(
                    ApplyResult.False(
                        Loc.Instance[$"{ErrorPrefix}.InvalidRAM", context.Snapshot.Memory.TotalKB]
                    )
                );
            }

            RegistryService.Write(
                context,
                new RegistryItem(
                    @"HKLM\SYSTEM\CurrentControlSet\Control",
                    "SvcHostSplitThresholdInKB",
                    context.Snapshot.Memory.TotalKB,
                    RegistryValueKind.DWord
                )
            );
            context.Logger.LogInformation(
                "Consolidated service hosts with threshold: {ThresholdKB} KB",
                context.Snapshot.Memory.TotalKB
            );
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }



    [Optimization(
        Id = "613FE85C-770D-441C-B97A-147B89B99028",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class KeyboardLatencyOptimization : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                context,
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardDelay", "0"),
                new RegistryItem(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardSpeed", "31")
            );

            context.Logger.LogInformation("Optimized keyboard repeat settings");
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "B7BB32F8-C756-47A4-83F2-F6E7EC7D45B8",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Latency | OptimizationTags.System
    )]
    public class DisableAccessibilityKeyboardHotkeys : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                context,
                new RegistryItem(
                    @"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys",
                    "Flags",
                    "26"
                ),
                new RegistryItem(
                    @"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response",
                    "Flags",
                    "2"
                ),
                new RegistryItem(
                    @"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys",
                    "Flags",
                    "34"
                )
            );

            context.Logger.LogInformation("Disabled accessibility keyboard hotkeys");
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "D5A2F7C4-1E8B-4C93-B6D0-8F41AE92C7E3",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Network | OptimizationTags.Performance
    )]
    public class LiftWebDavFileSizeLimit : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RegistryService.Write(
                context,
                new RegistryItem(
                    @"HKLM\SYSTEM\CurrentControlSet\Services\WebClient\Parameters",
                    "FileSizeLimitInBytes",
                    unchecked((int)0xFFFFFFFF),
                    RegistryValueKind.DWord
                )
            );

            context.Logger.LogInformation("Lifted the WebDAV file size limit to 4 GB");
            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }
}
