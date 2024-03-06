using Avalonia;
using Avalonia.Styling;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.Converters;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI
{
    /// <summary>
    /// Global application settings, stored in a JSON file in the user's roaming user directory.
    /// </summary>
    [DataContract]
    public sealed class Settings : ViewModelBase
    {
        [IgnoreDataMember]
        public static Settings Default { get; } = new();

        [IgnoreDataMember]
        private static string FileName => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            MapDecompilerGuiConstants.ConfigDirectoryName,
            MapDecompilerGuiConstants.ConfigFileName);

        // Must match setting in App.axaml
        private ThemeVariant _theme = ThemeVariant.Default;

        [DataMember]
        [JsonConverter(typeof(StringToThemeVariantConverter))]
        public ThemeVariant Theme
        {
            get => _theme;
            set
            {
                this.RaiseAndSetIfChanged(ref _theme, value);
                Application.Current!.RequestedThemeVariant = value;
            }
        }

        private string _outputDirectory = Environment.CurrentDirectory;

        [DataMember]
        public string OutputDirectory
        {
            get => _outputDirectory;
            set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
        }

        private string _lastConvertDirectory = Environment.CurrentDirectory;

        [DataMember]
        public string LastConvertDirectory
        {
            get => _lastConvertDirectory;
            set => this.RaiseAndSetIfChanged(ref _lastConvertDirectory, value);
        }

        private bool _generateWadFile = true;

        [DataMember]
        public bool GenerateWadFile
        {
            get => _generateWadFile;
            set => this.RaiseAndSetIfChanged(ref _generateWadFile, value);
        }

        private bool _applyNullToGeneratedFaces = false;

        [DataMember]
        public bool ApplyNullToGeneratedFaces
        {
            get => _applyNullToGeneratedFaces;
            set => this.RaiseAndSetIfChanged(ref _applyNullToGeneratedFaces, value);
        }

        private bool _alwaysGenerateOriginBrushes = false;

        [DataMember]
        public bool AlwaysGenerateOriginBrushes
        {
            get => _alwaysGenerateOriginBrushes;
            set => this.RaiseAndSetIfChanged(ref _alwaysGenerateOriginBrushes, value);
        }

        private string _decompilerStrategy = DecompilerStrategies.Strategies[0].Name;

        [DataMember]
        public string DecompilerStrategy
        {
            get => _decompilerStrategy;
            set => this.RaiseAndSetIfChanged(ref _decompilerStrategy, value);
        }

        private bool _mergeBrushes = true;

        [DataMember]
        public bool MergeBrushes
        {
            get => _mergeBrushes;
            set => this.RaiseAndSetIfChanged(ref _mergeBrushes, value);
        }

        private bool _skipSolidSkyLeafs = false;

        [DataMember]
        public bool SkipSolidSkyLeafs
        {
            get => _skipSolidSkyLeafs;
            set => this.RaiseAndSetIfChanged(ref _skipSolidSkyLeafs, value);
        }

        private bool _includeLiquids = true;

        [DataMember]
        public bool IncludeLiquids
        {
            get => _includeLiquids;
            set => this.RaiseAndSetIfChanged(ref _includeLiquids, value);
        }

        private BrushOptimization _brushOptimization = BrushOptimization.BestTextureMatch;

        [DataMember]
        [JsonConverter(typeof(StringEnumConverter))]
        public BrushOptimization BrushOptimization
        {
            get => _brushOptimization;
            set => this.RaiseAndSetIfChanged(ref _brushOptimization, value);
        }

        private List<string> _triggerEntityWildcards = new();

        [DataMember]
        public List<string> TriggerEntityWildcards
        {
            get => _triggerEntityWildcards;
            set => this.RaiseAndSetIfChanged(ref _triggerEntityWildcards, value);
        }

        public async Task Load()
        {
            var fileName = FileName;

            if (File.Exists(fileName))
            {
                try
                {
                    var json = File.ReadAllText(fileName);

                    // File is empty; we'll write to it later.
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    JsonConvert.PopulateObject(json, this);
                }
                catch (Exception e)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Error reading configuration file",
                        "An error occurred while reading the configuration file\n\n" +
                        "Error: " + e.ToString(), ButtonEnum.Ok);

                    await box.ShowAsync();
                }
            }
        }

        public async Task Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);

                using var stream = File.Open(FileName, FileMode.Create);

                using StreamWriter streamWriter = new(stream, leaveOpen: true);
                using JsonTextWriter jsonWriter = new(streamWriter);

                JsonSerializer serializer = new()
                {
                    Formatting = Formatting.Indented,
                };

                serializer.Serialize(jsonWriter, this);
            }
            catch (Exception e)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Error writing configuration file",
                    "An error occurred while writing the configuration file\n" +
                    "Make sure the file is not read-only and that the program has permission to access it\n\n" +
                    "Error:" + e.ToString(), ButtonEnum.Ok);

                await box.ShowAsync();
            }
        }
    }
}
