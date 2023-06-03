using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;

public enum JobStatus
{
    Idle,
    Running,
    Paused,
    Disabled,
    Error
}

public class ServiceStatus
{
    public string StatusText { get; set; } = "Initialising";
    public JobStatus Status { get; set; } = JobStatus.Idle;
    public int CPULevel { get; set; }
}


public class CPULevelSettings
{
    public bool EnableAltCPULevel { get; set; } = false;
    public int CPULevel { get; set; } = 25;
    public int CPULevelAlt { get; set; } = 75;
    public TimeSpan? AltTimeStart { get; set; } = new TimeSpan(23, 0, 0);
    public TimeSpan? AltTimeEnd { get; set; } = new TimeSpan(4, 0, 0);

    /// <summary>
    ///     Determines which CPU level to use based on the current time
    /// </summary>
    public int CurrentCPULimit
    {
        get
        {
            var useAlternateLevel = true;

            if (EnableAltCPULevel && AltTimeStart.HasValue && AltTimeEnd.HasValue)
            {
                var now = DateTime.UtcNow.TimeOfDay;

                if (AltTimeStart < AltTimeEnd)
                    useAlternateLevel = AltTimeStart < now && now < AltTimeEnd;
                else
                    useAlternateLevel = AltTimeStart < now || now < AltTimeEnd;
            }

            return useAlternateLevel ? CPULevelAlt : CPULevel;
        }
    }

    public override string ToString()
    {
        var result = $"CPULevel={25}%";

        if (EnableAltCPULevel) result += $", AltLevel={CPULevelAlt}% [{AltTimeStart} - {AltTimeEnd}]";

        return result;
    }
}
public interface IWorkService
{
    void AddJob(IProcessJob job);
    Task<ServiceStatus> GetWorkStatus();
    Task Pause(bool paused);
    event Action<ServiceStatus> OnStatusChanged;

    Task<CPULevelSettings> GetCPUSchedule();
    Task SetCPUSchedule(CPULevelSettings settings);
}
