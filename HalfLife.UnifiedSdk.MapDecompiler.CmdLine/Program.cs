using HalfLife.UnifiedSdk.MapDecompiler.Jobs;
using System.Collections.Immutable;
using System.CommandLine;

namespace HalfLife.UnifiedSdk.MapDecompiler.CmdLine
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            var decompilerStrategyOption = new Option<DecompilerStrategy>("--strategy",
                parseArgument: result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        return DecompilerStrategies.TreeDecompilerStrategy;
                    }

                    var decompilerStrategy = DecompilerStrategies.Strategies
                        .SingleOrDefault(s => s.Name.Equals(result.Tokens.Single().Value, StringComparison.InvariantCultureIgnoreCase));

                    if (decompilerStrategy is not null)
                    {
                        return decompilerStrategy;
                    }
                    else
                    {
                        result.ErrorMessage = "Unknown decompiler strategy.";
                        return DecompilerStrategies.TreeDecompilerStrategy;
                    }
                },
                isDefault: true,
                description: "Which decompiler algorithm to use");

            decompilerStrategyOption.FromAmong(DecompilerStrategies.Strategies.Select(s => s.Name).ToArray());

            var filesArgument = new Argument<IEnumerable<FileInfo>>("files", description: "List of files to decompile");

            var destinationOption = new Option<DirectoryInfo?>(
                "--destination",
                getDefaultValue: () => null,
                description: "Directory to save decompiled maps to. Leave empty to use current working directory");

            var generateWadFileOption = new Option<bool>(
                "--generate-wad-file",
                getDefaultValue: () => true,
                description: "Whether to generate a WAD file if the map contains embedded textures");

            var applyNullToGeneratedFacesOption = new Option<bool>(
                "--apply-null",
                getDefaultValue: () => false,
                description: "Whether to apply NULL to generated faces");

            var alwaysGenerateOriginBrushesOption = new Option<bool>(
                "--generate-origin-brushes",
                getDefaultValue: () => false,
                description: "Whether to always generate origin brushes for brush entities");

            var mergeBrushesOption = new Option<bool>("--merge-brushes",
                getDefaultValue: () => true,
                description: "Whether to merge brushes");

            var includeLiquidsOption = new Option<bool>("--include-liquids",
                getDefaultValue: () => true,
                description: "Whether to include brushes with liquid content types");

            var brushOptimizationOption = new Option<BrushOptimization>("--brush-optimization",
                getDefaultValue: () => BrushOptimization.BestTextureMatch,
                description: "What to optimize brushes for");

            var triggerEntityClassNameWildcardsOption = new Option<List<string>>(
                "--trigger-wildcard",
                description: "List of wildcards matching trigger entities to apply AAATRIGGER to");

            var rootCommand = new RootCommand("Half-Life Unified SDK Map Decompiler")
            {
                decompilerStrategyOption,
                destinationOption,
                generateWadFileOption,
                applyNullToGeneratedFacesOption,
                alwaysGenerateOriginBrushesOption,
                mergeBrushesOption,
                includeLiquidsOption,
                brushOptimizationOption,
                triggerEntityClassNameWildcardsOption,
                filesArgument
            };

            rootCommand.SetHandler((context) =>
            {
                var decompilerStrategy = context.ParseResult.GetValueForOption(decompilerStrategyOption)
                    ?? DecompilerStrategies.TreeDecompilerStrategy;
                var destination = context.ParseResult.GetValueForOption(destinationOption);
                var generateWadFile = context.ParseResult.GetValueForOption(generateWadFileOption);
                var applyNullToGeneratedFaces = context.ParseResult.GetValueForOption(applyNullToGeneratedFacesOption);
                var alwaysGenerateOriginBrushes = context.ParseResult.GetValueForOption(alwaysGenerateOriginBrushesOption);
                var mergeBrushes = context.ParseResult.GetValueForOption(mergeBrushesOption);
                var includeLiquids = context.ParseResult.GetValueForOption(includeLiquidsOption);
                var brushOptimization = context.ParseResult.GetValueForOption(brushOptimizationOption);
                var triggerEntityClassNameWildcards = context.ParseResult.GetValueForOption(triggerEntityClassNameWildcardsOption);
                var files = context.ParseResult.GetValueForArgument(filesArgument);

                if (!files.Any())
                {
                    Console.WriteLine("Nothing to decompile");
                    return;
                }

                var groupedFiles = files.GroupBy(f => f.FullName);

                foreach (var group in groupedFiles)
                {
                    if (group.Count() > 1)
                    {
                        Console.WriteLine($"Warning: file name \"{group.Key}\" specified more than once, ignoring duplicates");
                    }
                }

                var uniqueFiles = groupedFiles.Select(g => g.Key);

                destination ??= new DirectoryInfo(Directory.GetCurrentDirectory());

                MapDecompilerFrontEnd decompiler = new();
                DecompilerOptions decompilerOptions = new()
                {
                    ApplyNullToGeneratedFaces = applyNullToGeneratedFaces,
                    AlwaysGenerateOriginBrushes = alwaysGenerateOriginBrushes,
                    MergeBrushes = mergeBrushes,
                    IncludeLiquids = includeLiquids,
                    BrushOptimization = brushOptimization,
                    TriggerEntityWildcards = triggerEntityClassNameWildcards?.ToImmutableList() ?? ImmutableList<string>.Empty,
                };

                var destinationDirectory = destination.FullName;

                var jobs = uniqueFiles
                    .Select(f =>
                    {
                        var job = new MapDecompilerJob(f, destinationDirectory);

                        job.MessageReceived += (job, message) => job.Output += message;

                        return job;
                    })
                    .ToList();

                Parallel.ForEach(jobs, job =>
                {
                    decompiler.Decompile(job, decompilerStrategy, decompilerOptions, generateWadFile, CancellationToken.None);

                    // Write completed log to console.
                    // Because we're decompiling multiple maps at the same time the log output would be mixed otherwise.
                    Console.WriteLine(job.Output);
                });
            });

            return await rootCommand.InvokeAsync(args);
        }
    }
}