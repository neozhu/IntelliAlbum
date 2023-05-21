﻿using CleanArchitecture.Blazor.Application.Common.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class ImageMagickProcessor : IImageProcessor
{
    private const string imageMagickExe = "convert";

    private const string graphicsMagickExe = "gm";

    // SkiaSharp doesn't handle .heic files... yet
    private static readonly string[] s_imageExtensions = { ".jpg", ".jpeg", ".png", ".heic", ".tif", ".tiff", ".webp" };
    private static bool imAvailable;
    private readonly bool s_useGraphicsMagick = false; // GM doesn't support HEIC yet.
    private static readonly ILogger Logging = Log.ForContext(typeof(SkiaSharpProcessor));
    private string verString = "(not found)";

    public ImageMagickProcessor()
    {
        CheckToolStatus();
    }

    public string IMVersion
    {
        get
        {
            if (imAvailable)
                return verString;

            return "N/A";
        }
    }

    public static ICollection<string> SupportedFileExtensions
    {
        get
        {
            if (imAvailable)
                return s_imageExtensions;

            return new string[0];
        }
    }

    /// <summary>
    ///     Convert the files to thumbnails by shelling out to either ImageMagick
    ///     or the faster GraphicsMagick.
    /// </summary>
    /// <param name="source">Source.</param>
    /// <param name="sizes">Sizes.</param>
    public async Task<IImageProcessResult> CreateThumbs(FileInfo source, IDictionary<FileInfo, IThumbConfig> destFiles)
    {
        // This processor doesn't support hash creation
        IImageProcessResult result = new ImageProcessResult { ThumbsGenerated = false, ImageHash = string.Empty };

        // Some useful unsharp and quality settings, plus by defining the max size of the JPEG, it 
        // makes imagemagic more efficient with its memory allocation, so significantly faster. 
        string args;
        var exeToUse = s_useGraphicsMagick ? graphicsMagickExe : imageMagickExe;
        var maxHeight = destFiles.Max(x => x.Value.height);
        var maxWidth = destFiles.Max(x => x.Value.width);

        if (s_useGraphicsMagick)
            args = string.Format(" convert -size {0}x{1} \"{2}\" -quality 90  -unsharp 0.5x0.5+1.25+0.0 ", maxHeight,
                maxWidth, source.FullName);
        else
            args = string.Format(" -define jpeg:size={0}x{1} \"{2}\" -quality 90 -unsharp 0.5x0.5+1.25+0.0 ", maxHeight,
                maxWidth, source.FullName);

        FileInfo? altSource = null;

        var argsList = new List<string>();

        // First pre-check whether the thumbs exist - don't want to create them if they don't.
        foreach (var pair in destFiles.OrderByDescending(x => x.Value.width))
        {
            var dest = pair.Key;
            var config = pair.Value;

            // File didn't exist, so add it to the command-line. 
            if (s_useGraphicsMagick)
                argsList.Add(string.Format("-thumbnail {0}x{1} -auto-orient -write \"{2}\" ", config.height,
                    config.width, dest.FullName));
            else
                argsList.Add(string.Format("-thumbnail {0}x{1} -auto-orient -write \"{2}\" ", config.height,
                    config.width, dest.FullName));
        }

        if (argsList.Any())
        {
            var lastArg = argsList.Last();
            lastArg = lastArg.Replace(" -write ", " ");
            argsList[argsList.Count() - 1] = lastArg;

            args += string.Join(" ", argsList);

            if (altSource != null)
            {
                source = altSource;
                Logging.Information("File {0} exists - using it as source for smaller thumbs.", altSource.Name);
            }

            Logging.Information("Converting file {0}", source);

            var process = new Process();

            process.StartInfo.FileName = exeToUse;
            process.StartInfo.Arguments = args;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.OutputDataReceived += Process_OutputDataReceived;
            process.ErrorDataReceived += Process_OutputDataReceived;

            try
            {
                Logging.Information("  Executing: {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

                var success = process.Start();

                if (success)
                {
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        result.ThumbsGenerated = true;
                        Logging.Information("Execution complete.");
                    }
                    else
                    {
                        throw new Exception("Failed");
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Error("Conversion failed. Unable to start process: {0}", ex.Message);
                Logging.Error($"Failed commandline was: {exeToUse} {args}");
            }
        }
        else
        {
            Logging.Information("Thumbs already exist in all resolutions. Skipping...");
        }

        return result;
    }

    public Task TransformDownloadImage(string input, Stream output, IExportSettings config)
    {
        throw new NotImplementedException();
    }

    public Task GetCroppedFile(FileInfo source, int x, int y, int width, int height, FileInfo destFile)
    {
        throw new NotImplementedException();
    }

    public Task CropImage(FileInfo source, int x, int y, int width, int height, Stream stream)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Check which command-line processor stuff.
    /// </summary>
    private void CheckToolStatus()
    {
        var improcess = new ProcessStarter();
        imAvailable = improcess.StartProcess(imageMagickExe, "--version");

        if (imAvailable)
        {
            var version = improcess.OutputText?.Split('\n').FirstOrDefault() ?? string.Empty;

            if (!string.IsNullOrEmpty(version))
            {
                verString = $"v{version}";
                Logging.Information($"ImageMagick found: {verString}");
            }
            else
            {
                Logging.Information("No ImageMagick Version returned.");
                imAvailable = false;
            }
        }
        else
        {
            Logging.Error("ImageMagick not found.");
        }
    }

    private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
 
        if (!string.IsNullOrEmpty(e.Data))
            Console.WriteLine(e.Data);
    }
}
