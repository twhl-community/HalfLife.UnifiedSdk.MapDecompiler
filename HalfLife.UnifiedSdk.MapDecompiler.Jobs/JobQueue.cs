using System.Collections.Concurrent;

namespace HalfLife.UnifiedSdk.MapDecompiler.Jobs
{
    public sealed class JobQueue : IDisposable
    {
        private sealed record WorkerJob(MapDecompilerJob Job, DecompilerStrategy DecompilerStrategy,
            DecompilerOptions DecompilerOptions, bool GenerateWadFile, CancellationToken CancellationToken);

        private bool _disposedValue;

        private CancellationTokenSource _jobCancellationTokenSource = new();
        private readonly CancellationTokenSource _stopCancellationTokenSource = new();

        private readonly List<Thread> _workerThreads = new();

        private readonly BlockingCollection<WorkerJob> _jobs = new();

        private int _activeJobsCount;

        public bool IsEmpty => Interlocked.CompareExchange(ref _activeJobsCount, 0, 0) == 0;

        public event Action<MapDecompilerJob>? OnJobStarting;

        public event Action<MapDecompilerJob, MapDecompilerJobStatus>? OnJobCompleted;

        public event Action<Exception>? OnExceptionCaught;

        public JobQueue()
        {
            // This should give the OS the chance to run the UI thread on its own core to keep things running smoothly.
            int threadCount = Math.Max(1, Environment.ProcessorCount - 1);

            for (int i = 0; i < threadCount; ++i)
            {
                var thread = new Thread(WorkerThread);
                _workerThreads.Add(thread);
                thread.Start();
            }
        }

        public void CancelAllJobs()
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(nameof(JobQueue));
            }

            // Clear out the queue first to avoid race conditions.
            while (_jobs.TryTake(out var job))
            {
                if (job.Job.Status == MapDecompilerJobStatus.Waiting)
                {
                    job.Job.Status = MapDecompilerJobStatus.Canceled;
                }

                Interlocked.Decrement(ref _activeJobsCount);

                OnJobCompleted?.Invoke(job.Job, job.Job.Status);
            }

            _jobCancellationTokenSource.Cancel();
            _jobCancellationTokenSource = new();
        }

        public void Stop()
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(nameof(JobQueue));
            }

            CancelAllJobs();
            _stopCancellationTokenSource.Cancel();

            foreach (var thread in _workerThreads)
            {
                thread.Join();
            }

            _workerThreads.Clear();
        }

        public void AddJobs(List<MapDecompilerJob> jobs, DecompilerStrategy decompilerStrategy,
            DecompilerOptions decompilerOptions, bool generateWadFile)
        {
            if (_disposedValue)
            {
                throw new ObjectDisposedException(nameof(JobQueue));
            }

            if (_stopCancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            Interlocked.Add(ref _activeJobsCount, jobs.Count);

            foreach (var job in jobs)
            {
                _jobs.Add(new(job, decompilerStrategy, decompilerOptions, generateWadFile,
                    _jobCancellationTokenSource.Token));
            }
        }

        private void WorkerThread()
        {
            var decompiler = new MapDecompilerFrontEnd();

            while (!_stopCancellationTokenSource.IsCancellationRequested)
            {
                WorkerJob job;

                try
                {
                    job = _jobs.Take(_stopCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }

                MapDecompilerJobStatus result = MapDecompilerJobStatus.Canceled;

                try
                {
                    OnJobStarting?.Invoke(job.Job);

                    result = decompiler.Decompile(job.Job, job.DecompilerStrategy, job.DecompilerOptions, job.GenerateWadFile,
                    job.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Nothing.
                }
                catch (Exception e)
                {
                    OnExceptionCaught?.Invoke(e);
                }
                finally
                {
                    // Must decrement before invoking the event handler so they can check for active jobs.
                    Interlocked.Decrement(ref _activeJobsCount);
                    OnJobCompleted?.Invoke(job.Job, result);
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _stopCancellationTokenSource.Dispose();
                    _jobCancellationTokenSource.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
