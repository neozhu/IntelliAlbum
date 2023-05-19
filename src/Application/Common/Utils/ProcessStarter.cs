using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;

namespace CleanArchitecture.Blazor.Application.Common.Utils;
public class ProcessStarter
{
    private static readonly ILogger Logging = Log.ForContext(typeof(ProcessStarter));
    /// <summary>
    ///     Public property which will contain the output text of the
    ///     executed process once it's completed.
    /// </summary>
    public string OutputText { get; private set; }

    /// <summary>
    ///     Start and run a command-line process and capture the output
    /// </summary>
    /// <param name="exe"></param>
    /// <param name="args"></param>
    /// <returns>True if execution succeeded</returns>
    public bool StartProcess(string exe, string args, IDictionary<string, string> envVars = null)
    {
        var process = new Process();

        process.StartInfo.FileName = exe;
        process.StartInfo.Arguments = args;
        process.StartInfo.RedirectStandardError = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        if (envVars != null)
            foreach (var kvp in envVars)
                process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;

        try
        {
            var lastOutput = DateTime.UtcNow;

            Logging.Information("  Executing: {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            var success = process.Start();

            if (success)
            {
                OutputText = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                Logging.Verbose("  Process completed exit code {0}", process.ExitCode);

                if (!string.IsNullOrEmpty(OutputText))
                    Logging.Verbose("  Output: {0}", OutputText);

                if (process.ExitCode == 0)
                    return true;
            }
        }
        catch (Exception ex)
        {
            Logging.Error("ERROR: Unable to start process: {0} {1}", exe, ex.Message);
        }
        finally
        {
            process.Dispose();
        }

        return false;
    }
}
