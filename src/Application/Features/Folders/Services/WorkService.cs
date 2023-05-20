﻿using CleanArchitecture.Blazor.Application.Common.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Services;
public class WorkService : IWorkService
{
#if DEBUG
    private const int jobFetchSleep = 10;
#else
    private const int jobFetchSleep = 30;
#endif

    // This is like an IRQ flag. If it's true, we check the queue 
    // for new entries rather than just ploughing through it. 
    private volatile bool _newJobsFlag;

    private readonly UniqueConcurrentPriorityQueue<IProcessJob, string> _jobQueue = new(x => x.Description,
        y => (int)y.Priority);

    private readonly ConcurrentBag<IProcessJobFactory> _jobSources = new();
    private const int _maxQueueSize = 500;
    private CPULevelSettings _cpuSettings = new();

    public bool Paused { get; set; }

    public Task Pause(bool paused)
    {
        Paused = paused;
        return Task.CompletedTask;
    }

    public Task<ServiceStatus> GetWorkStatus()
    {
        return Task.FromResult(Status);
    }

    private ServiceStatus Status { get; } = new();
    private readonly ServerNotifierService _notifier;
    private readonly ILogger<WorkService> _logger;

    public event Action<ServiceStatus> OnStatusChanged;

    public WorkService(ServerNotifierService notifier,
        ILogger<WorkService> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public Task<CPULevelSettings> GetCPUSchedule()
    {
        return Task.FromResult(_cpuSettings);
    }

    public Task SetCPUSchedule(CPULevelSettings cpuSettings)
    {
        _logger.LogInformation($"Work service updated with new CPU settings: {cpuSettings}");
        _cpuSettings = cpuSettings;

        return Task.CompletedTask;
    }

    public void AddJobSource(IProcessJobFactory source)
    {
        _logger.LogInformation($"Registered job processing source: {source.GetType().Name}");
        _jobSources.Add(source);
    }

    private void SetStatus(string newStatusText, JobStatus newStatus, int newCPULevel)
    {
        if (newStatusText != Status.StatusText || newStatus != Status.Status || newCPULevel != Status.CPULevel)
        {
            Status.StatusText = newStatusText;
            Status.Status = newStatus;
            Status.CPULevel = newCPULevel;

            OnStatusChanged?.Invoke(Status);
            _ = _notifier.NotifyClients(NotificationType.WorkStatusChanged, Status);
        }
    }

    public void StartService()
    {
        _logger.LogInformation("Started Work service thread.");

        var thread = new Thread(ProcessJobs)
        {
            Name = "WorkThread",
            IsBackground = true,
            Priority = ThreadPriority.Lowest
        };
        thread.Start();
    }

    /// <summary>
    ///     The thread loop for the job processing queue. Processes
    ///     jobs in sequence - we never process jobs in parallel as
    ///     it's too complex to avoid data integrity and concurrency
    ///     problems (although we could perhaps allow that when a
    ///     DB like PostGres is in use. For SQLite, definitely not.
    /// </summary>
    private async void ProcessJobs()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            var cpuPercentage = _cpuSettings.CurrentCPULimit;

            if (Paused || cpuPercentage == 0)
            {
                if (Paused)
                    SetStatus("Paused", JobStatus.Paused, cpuPercentage);
                else
                    SetStatus("Disabled", JobStatus.Disabled, cpuPercentage);

                // Nothing to do, so have a kip.
                Thread.Sleep(jobFetchSleep * 1000);
                continue;
            }

            var getNewJobs = _newJobsFlag;
            _newJobsFlag = false;
            var item = _jobQueue.TryDequeue();

            if (item != null)
                ProcessJob(item, cpuPercentage);
            else
                // No job to process, so we want to grab more
                getNewJobs = true;

            // See if there's any higher-priority jobs to process
            if (getNewJobs && !PopulateJobQueue())
                if (_jobQueue.IsEmpty)
                {
                    // Nothing to do, so set the status to idle, and have a kip.
                    SetStatus("Idle", JobStatus.Idle, cpuPercentage);
                    Thread.Sleep(jobFetchSleep * 1000);
                }
        }
    }

    /// <summary>
    ///     Callback to notify the work service to look for new jobs.
    ///     Will be called async, from another thread, and should
    ///     process the PopulateJobs method on another thread and
    ///     return immediately.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="waitSeconds"></param>
    public void FlagNewJobs(IProcessJobFactory source)
    {
        _logger.LogInformation($"Flagging new jobs state for {source.GetType().Name}");

        var watch = new Stopwatch("FlagNewJobs");

        _newJobsFlag = true;

        watch.Stop();
    }

    /// <summary>
    ///     Check with the work providers and see if there's any work to do.
    /// </summary>
    /// <returns></returns>
    private bool PopulateJobQueue()
    {
        _logger.LogTrace("Looking for new jobs...");

        var watch = new Stopwatch("PopulateJobQueue");

        var newJobs = false;

        foreach (var source in _jobSources.OrderBy(x => x.Priority))
            if (PopulateJobsForService(source, _maxQueueSize - _jobSources.Count))
                newJobs = true;

        watch.Stop();

        return newJobs;
    }

    /// <summary>
    ///     For the given service, checks for new jobs that might be
    ///     processed, and adds any that are found into the queue.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="maxCount"></param>
    /// <returns></returns>
    private bool PopulateJobsForService(IProcessJobFactory source, int maxCount)
    {
        var newJobs = 0;

        var watch = new Stopwatch("PopulateJobsForService");

        if (maxCount > 0)
            try
            {
                var jobs = source.GetPendingJobs(maxCount).Result;

                foreach (var job in jobs)
                    if (_jobQueue.TryAdd(job))
                        newJobs++;

                if (newJobs > 0)
                    _logger.LogTrace($"Added {newJobs} jobs to pending queue for {source.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception getting jobs: {ex.Message}");
            }

        watch.Stop();

        return newJobs > 0;
    }

    /// <summary>
    ///     Do the actual work in processing a task in the queue
    /// </summary>
    /// <param name="job"></param>
    /// <param name="cpuPercentage"></param>
    /// <returns></returns>
    private void ProcessJob(IProcessJob job, int cpuPercentage)
    {
        var jobName = job.GetType().Name;

        // If we can't process, we'll discard this job, and pick it 
        // up again in future during the next GetPendingJobs call.
        if (job.CanProcess)
        {
            SetStatus($"{job.Name}", JobStatus.Running, cpuPercentage);

            _logger.LogTrace($"Processing job type: {jobName}");

            var stopwatch = new Stopwatch($"ProcessJob{jobName}");
            try
            {
                job.Process();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception processing {job.Description}: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
            }

            // Now, decide how much we need to sleep, in order to throttle CPU to the desired percentage
            // E.g., if the job took 2.5s to execute, then in order to maintain 25% CPU usage, we need to
            // sleep for 7.5s. Similarly, if the job took 0.5s, and we want to maintain 75% CPU usage,
            // we'd sleep for 0.33s.
            var sleepFactor = 1.0 / (cpuPercentage / 100.0) - 1;

            if (sleepFactor > 0)
            {
                // Never ever sleep for more than 10s. Otherwise a long-running job that takes a minute
                // to complete could end up freezing the worker thread for 3 minutes, which makes no
                // sense whatsoeever. :)
                const int maxWaitTime = 10 * 1000;

                // If the job took less than 100ms, assume it took 100ms - so that the sleep factor * job
                // time never approaches zero. This is to stop very fast/short jobs overwhelming the CPU
                double jobTime = Math.Max(stopwatch.ElapsedTime, 100);

                var waitTime = Math.Min((int)(sleepFactor * jobTime), maxWaitTime);

                _logger.LogTrace($"Job '{jobName}' took {stopwatch.ElapsedTime}ms, so sleeping {waitTime} to give {cpuPercentage}% CPU usage.");
                Thread.Sleep(waitTime);
            }
        }
        else
        {
            _logger.LogInformation($"Discarded job {jobName}");
        }
    }
}
