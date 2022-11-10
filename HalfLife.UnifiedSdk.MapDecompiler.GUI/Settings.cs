using Avalonia;
using Avalonia.Themes.Fluent;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReactiveUI;
using System;
using System.IO;
using System.Runtime.Serialization;

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
            "Half-Life Unified SDK",
            "MapDecompilerSettings.json");

        // Must match setting in App.axaml
        private FluentThemeMode _theme = FluentThemeMode.Light;

        [DataMember]
        public FluentThemeMode Theme
        {
            get => _theme;
            set
            {
                this.RaiseAndSetIfChanged(ref _theme, value);

                if (Application.Current!.Styles[0] is FluentTheme theme)
                {
                    theme.Mode = _theme;
                }
            }
        }

        private string _outputDirectory = Environment.CurrentDirectory;

        [DataMember]
        public string OutputDirectory
        {
            get => _outputDirectory;
            set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
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

        public void Load()
        {
            var fileName = FileName;

            if (File.Exists(fileName))
            {
                var json = File.ReadAllText(fileName);
                JsonConvert.PopulateObject(json, this);
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            using var stream = File.Open(FileName, FileMode.Create);
            Save(stream);
        }

        public void Save(Stream stream)
        {
            using StreamWriter streamWriter = new(stream, leaveOpen: true);
            using JsonTextWriter jsonWriter = new(streamWriter);

            JsonSerializer serializer = new()
            {
                Formatting = Formatting.Indented,
            };

            serializer.Serialize(jsonWriter, this);
        }
    }
}
