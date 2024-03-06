using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class DecompilerOptionsViewModel : ViewModelBase
    {
        public Settings Settings { get; } = Settings.Default;

        public ObservableCollection<StringWrapper> TriggerEntityWildcards { get; }
            = new ObservableCollection<StringWrapper>(Settings.Default.TriggerEntityWildcards.Select(s => new StringWrapper(s)));

        public ICommand BrowseOutputDirectoryCommand { get; }

        public Interaction<OpenDirectoryViewModel, string?> ShowBrowseDirectoryDialog { get; } = new();

        private int _selectedTriggerEntityWilcardIndex = -1;

        public int SelectedTriggerEntityWildcardIndex
        {
            get => _selectedTriggerEntityWilcardIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedTriggerEntityWilcardIndex, value);
        }

        public ICommand AddTriggerEntityWildcard { get; }

        public ICommand RemoveTriggerEntityWildcard { get; }

        public DecompilerOptionsViewModel()
        {
            TriggerEntityWildcards.CollectionChanged += TriggerEntityWildcards_CollectionChanged;

            BrowseOutputDirectoryCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var directory = await ShowBrowseDirectoryDialog.Handle(new()
                {
                    Directory = Settings.OutputDirectory
                });

                if (directory is null)
                {
                    return;
                }

                Settings.OutputDirectory = directory;
            });

            AddTriggerEntityWildcard = ReactiveCommand.Create(() => TriggerEntityWildcards.Add(new StringWrapper("trigger_*")));
            RemoveTriggerEntityWildcard = ReactiveCommand.Create(() => TriggerEntityWildcards.RemoveAt(SelectedTriggerEntityWildcardIndex),
                this.WhenAnyValue(x => x.SelectedTriggerEntityWildcardIndex).Select(i => i != -1));
        }

        private void TriggerEntityWildcards_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var wrapper = TriggerEntityWildcards[e.NewStartingIndex];
                    Settings.TriggerEntityWildcards.Add(wrapper.Value);
                    int index = e.NewStartingIndex;
                    wrapper.WhenPropertyChanged(x => x.Value, false)
                        .DistinctUntilChanged()
                        .Subscribe(wrapper => Settings.TriggerEntityWildcards[index] = wrapper.Value!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    Settings.TriggerEntityWildcards.RemoveAt(e.OldStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    Settings.TriggerEntityWildcards[e.NewStartingIndex] = TriggerEntityWildcards[e.NewStartingIndex].Value;
                    break;
            }
        }

        public DecompilerOptions ToOptions()
        {
            return new()
            {
                ApplyNullToGeneratedFaces = Settings.ApplyNullToGeneratedFaces,
                AlwaysGenerateOriginBrushes = Settings.AlwaysGenerateOriginBrushes,
                MergeBrushes = Settings.MergeBrushes,
                SkipSolidSkyLeafs = Settings.SkipSolidSkyLeafs,
                IncludeLiquids = Settings.IncludeLiquids,
                BrushOptimization = Settings.BrushOptimization,
                TriggerEntityWildcards = Settings.TriggerEntityWildcards.ToImmutableList()
            };
        }
    }
}
