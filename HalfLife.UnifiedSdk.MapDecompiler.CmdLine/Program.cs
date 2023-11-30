using HalfLife.UnifiedSdk.MapDecompiler.Jobs;
using System.CommandLine;

namespace HalfLife.UnifiedSdk.MapDecompiler.CmdLine
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            var filesArgument = new Argument<IEnumerable<FileInfo>>("files",
                description: "List of files to decompile");

            var destinationOption = new Option<DirectoryInfo?>("--destination",
                getDefaultValue: () => null,
                description: "Directory to save decompiled maps to. Leave empty to use current working directory");

            var generateWadFileOption = new Option<bool>("--generate-wad-file",
                getDefaultValue: () => true,
                description: "Whether to generate a WAD file if the map contains embedded textures");

            var applyNullToGeneratedFacesOption = new Option<bool>("--apply-null",
                getDefaultValue: () => false,
                description: "Whether to apply NULL to generated faces");

            var alwaysGenerateOriginBrushesOption = new Option<bool>("--generate-origin-brushes",
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

            var triggerEntityClassNameWildcardsOption = new Option<List<string>>("--trigger-wildcard",
                description: "List of wildcards matching trigger entities to apply AAATRIGGER to");

            DecompilerOptionsBinder decompilerOptionsBinder = new(applyNullToGeneratedFacesOption,
                alwaysGenerateOriginBrushesOption,
                mergeBrushesOption,
                includeLiquidsOption,
                brushOptimizationOption,
                triggerEntityClassNameWildcardsOption);

            var treeStrategyVerb = new Command(DecompilerStrategies.TreeDecompilerStrategy.Name,
                "Decompiles maps by walking the BSP tree")
            {
                destinationOption,
                filesArgument,
                generateWadFileOption,
                applyNullToGeneratedFacesOption,
                alwaysGenerateOriginBrushesOption,
                mergeBrushesOption,
                includeLiquidsOption,
                brushOptimizationOption,
                triggerEntityClassNameWildcardsOption
            };

            treeStrategyVerb.SetHandler((decompilerOptions, destination, files, generateWadFile) =>
            {
                DecompileMaps(DecompilerStrategies.TreeDecompilerStrategy, 
                    decompilerOptions, destination, files, generateWadFile);
            }, decompilerOptionsBinder, destinationOption, filesArgument, generateWadFileOption);

            var faceToBrushStrategyVerb = new Command(DecompilerStrategies.FaceToBrushDecompilerStrategy.Name,
                "Decompiles maps by converting each visible face to a brush")
            {
                destinationOption,
                filesArgument,
                generateWadFileOption
            };

            faceToBrushStrategyVerb.SetHandler((decompilerOptions, destination, files, generateWadFile) =>
            {
                DecompileMaps(DecompilerStrategies.FaceToBrushDecompilerStrategy,
                    decompilerOptions, destination, files, generateWadFile);
            }, decompilerOptionsBinder, destinationOption, filesArgument, generateWadFileOption);

            var rootCommand = new RootCommand("Half-Life Unified SDK Map Decompiler")
            {
                treeStrategyVerb,
                faceToBrushStrategyVerb
            };

            return await rootCommand.InvokeAsync(args);
        }

        private static void DecompileMaps(DecompilerStrategy decompilerStrategy, DecompilerOptions decompilerOptions,
            DirectoryInfo? destination, IEnumerable<FileInfo> files, bool generateWadFile)
        {
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
        }
    }
}