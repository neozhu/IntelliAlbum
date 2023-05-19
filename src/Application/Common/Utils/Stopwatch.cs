﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Common.Utils;
/// <summary>
///     Timer class for tracking performance. Keeps a running stat of the max times
///     for each named operation, and the average times for all instances of a named
///     operation.
/// </summary>
public struct Stopwatch
{
    private struct Totals
    {
        public long count;
        public long totalTime;
        public long maxTime;
        public string name;

        public long AverageTime => (long)((double)totalTime / count);
    }

    private static readonly IDictionary<string, Totals> stats =
        new ConcurrentDictionary<string, Totals>(StringComparer.OrdinalIgnoreCase);

    private void UpdateStats(string statName, long time)
    {
        lock (stats)
        {
            Totals total;
            if (!stats.TryGetValue(statName, out total))
            {
                total = new Totals { count = 1, totalTime = time, maxTime = time, name = statName };
            }
            else
            {
                total.count++;
                total.totalTime += time;
                if (total.maxTime < time)
                    total.maxTime = time;
            }

            stats[timername] = total;
        }
    }

    private int taskThresholdMS = -1;
    private readonly string timername;
    private readonly long start;
    private long end;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="name">Name of the work being timed</param>
    /// <param name="thresholdMS">
    ///     Threshold, in milliseconds, ovr which we should
    ///     log a message stating that the work took an unexpectedly long time to complete.
    /// </param>
    public Stopwatch(string name, int thresholdMS = 1000)
    {
        taskThresholdMS = thresholdMS;

        timername = name;
        end = start = Environment.TickCount64;
    }

    /// <summary>
    ///     Terminate the stopwatch, add the average/max to the collection of timers,
    ///     and log if the job took longer than the expected threshold.
    /// </summary>
    public void Stop()
    {
        end = Environment.TickCount64;

        var time = end - start;

        UpdateStats(timername, time);
    }

    public long ElapsedTime => end - start;
    public string HumanElapsedTime => ((int)(end - start)).ToHumanReadableString();

    public override string ToString()
    {
        return $"{ElapsedTime}ms";
    }

    public static void WriteTotals(Action<string> logfunc)
    {
        try
        {
            if (stats.Any())
            {
                logfunc("Performance Summary:");
                var titleLength = stats.Keys.Max(x => x.Length);

                foreach (var kvp in stats.OrderBy(x => x.Key))
                {
                    var lineItem = kvp.Key + ":";
                    logfunc(
                        $"  {lineItem.PadRight(titleLength + 2, ' ')}   Count: {kvp.Value.count,7}   Avg: {kvp.Value.AverageTime,7}ms   Max: {kvp.Value.maxTime,7}ms");
                }
            }
        }
        catch (Exception)
        {
            logfunc("Unable to write stopwatch totals.");
        }
    }
}
