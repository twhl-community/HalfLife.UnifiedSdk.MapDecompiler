using Avalonia.Threading;
using DynamicData;
using HalfLife.UnifiedSdk.MapDecompiler.Decompilation;
using HalfLife.UnifiedSdk.MapDecompiler.Jobs;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly CancellationTokenSource _jobCancellationTokenSource = new();

        private Task _jobTask = Task.CompletedTask;

        private readonly MapDecompilerFrontEnd _decompiler = new();

        private readonly ILogger _programLogger;

        private readonly Stopwatch _programStopwatch = new();

        public ICommand ConvertFilesCommand { get; }

        public Interaction<OpenFileViewModel, string[]?> ShowConvertFilesDialog { get; } = new();

        public ICommand CancelCommand { get; }

        public Interaction<CancelJobsDialogViewModel, bool> ShowCancelJobsDialog { get; } = new();

        public ObservableCollection<MapDecompilerJob> Files { get; } = new();

        private int _logIndex;

        public int LogIndex
        {
            get => _logIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _logIndex, value);

                if (_logIndex == 1 && CurrentJob is null)
                {
                    CurrentJob = Files.FirstOrDefault();
                }
            }
        }

        private string _programOutput = string.Empty;

        public string ProgramOutput
        {
            get => _programOutput;
            set => this.RaiseAndSetIfChanged(ref _programOutput, value);
        }

        private MapDecompilerJob? _currentJob;

        public MapDecompilerJob? CurrentJob
        {
            get => _currentJob;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentJob, value);
                this.RaisePropertyChanged(nameof(CanExecuteDelete));

                if (_currentJob is not null)
                {
                    LogIndex = 1;
                }
            }
        }

        public DecompilerOptionsViewModel DecompilerOptions { get; } = new();

        public bool HasJobItems => !_jobTask.IsCompleted;

        public ICommand DeleteCommand { get; }

        public bool CanExecuteDelete => CurrentJob is not null && CurrentJob.Status != MapDecompilerJobStatus.Converting;

        public MainWindowViewModel()
        {
            _programLogger = new LoggerConfiguration()
                .WriteTo.Sink(new ForwardingSink(message => ProgramOutput += message, "{Message:lj}{NewLine}{Exception}"))
                .MinimumLevel.Information()
                .CreateLogger();

            ConvertFilesCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var store = new OpenFileViewModel
                {
                    Title = "Convert",
                    Filters = new[]
                    {
                        new FileFilter("Half-Life 1 BSP Files (.bsp)", "bsp"),
                        new FileFilter("All Files", "*")
                    },
                    AllowMultiple = true
                };

                var result = await ShowConvertFilesDialog.Handle(store);

                if (result is null)
                {
                    return;
                }

                var outputDirectory = DecompilerOptions.OutputDirectory;

                if (outputDirectory.Length == 0)
                {
                    outputDirectory = Directory.GetCurrentDirectory();
                }

                var jobs = result
                    .Select(bspFileName =>
                    {
                        var job = new MapDecompilerJob(bspFileName, outputDirectory);

                        job.MessageReceived += LogMessage;

                        return job;
                    })
                    .Where(j => j.MapFileName.Length > 0)
                    .ToList();

                Files.AddRange(jobs);

                QueueJobs(jobs);
            });

            CancelCommand = ReactiveCommand.Create(() => CancelJobs());

            DeleteCommand = ReactiveCommand.Create(
                () => Files.Remove(CurrentJob!),
                this.WhenAnyValue(x => x.CanExecuteDelete));
        }

        public async Task<bool> ShouldClose()
        {
            if (!HasJobItems)
            {
                return true;
            }

            return await ShowCancelJobsDialog.Handle(new());
        }

        public async Task OnClosing()
        {
            await CancelJobs();
        }

        public async Task CancelJobs()
        {
            _jobCancellationTokenSource.Cancel();
            await _jobTask;
        }

        private static void LogMessage(MapDecompilerJob job, string message)
        {
            // This gets called from another thread so sync it.
            Dispatcher.UIThread.Post(() =>
            {
                var output = job.Output ?? string.Empty;
                job.Output = output + message;
            });
        }

        private void QueueJobs(List<MapDecompilerJob> jobs)
        {
            // Decompile each map one at a time.
            // Anything that relies on user settings should be created before starting the task to prevent race conditions.
            // Make sure to cache objects in local variables to prevent member variables from being captured.
            var decompilerOptions = DecompilerOptions.ToOptions();

            // If we're starting a single job just activate the job log automatically.
            if (_jobTask.IsCompleted && jobs.Count == 1)
            {
                CurrentJob = jobs[0];
            }

            _jobTask = _jobTask.ContinueWith(_ => ExecuteJobs(jobs, decompilerOptions), _jobCancellationTokenSource.Token);
        }

        private void ExecuteJobs(List<MapDecompilerJob> jobs, DecompilerOptions decompilerOptions)
        {
            Dispatcher.UIThread.Post(() => _programLogger.Information("Starting {Count} new jobs", jobs.Count));
            _programStopwatch.Restart();

            try
            {
                Parallel.ForEach(
                    jobs,
                    new ParallelOptions()
                    {
                        CancellationToken = _jobCancellationTokenSource.Token,
                        // Use no more than half the cores to keep the UI responsive.
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                    },
                    job =>
                {
                    Dispatcher.UIThread.Post(() => job.Status = MapDecompilerJobStatus.Converting);

                    var result = _decompiler.Decompile(job, decompilerOptions, _jobCancellationTokenSource.Token);

                    var timeElapsed = _programStopwatch.Elapsed;

                    Dispatcher.UIThread.Post(() =>
                    {
                        job.Status = result;

                        _programLogger.Information("{From} => {To}: Time elapsed: {Time:dd\\.hh\\:mm\\:ss\\.fff}", job.From, job.To, timeElapsed);
                    });
                });
            }
            catch (OperationCanceledException)
            {
                foreach (var job in jobs)
                {
                    if (job.Status == MapDecompilerJobStatus.Waiting)
                    {
                        job.Status = MapDecompilerJobStatus.Canceled;
                    }
                }
            }

            {
                var timeElapsed = _programStopwatch.Elapsed;

                Dispatcher.UIThread.Post(() => _programLogger.Information("Total time elapsed: {Time:dd\\.hh\\:mm\\:ss\\.fff}", timeElapsed));
                _programStopwatch.Stop();
            }
        }
    }
}
