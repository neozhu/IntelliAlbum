﻿using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class MetaData
{
    public List<string> Keywords { get; set; }
    public int Rating { get; set; }
}

public class Photo
{
    public bool guid_locked { get; set; }
    public MetaData metadata { get; set; }
}

/// <summary>
///     Class to represent, and read, the On1 Sidecar data, which is json
///     serialised in a .on1 file.
/// </summary>
public class On1Sidecar
{
    private static readonly ILogger log = Log.ForContext(typeof(On1Sidecar));
    public Dictionary<string, Photo> photos { get; set; } = new();

    /// <summary>
    ///     Load the on1 sidecar metadata for the image - if it exists.
    /// </summary>
    /// <param name="image"></param>
    /// <returns>Metadata, with keywords etc</returns>
    public static MetaData LoadMetadata(FileInfo sidecarPath)
    {
        MetaData result = null;

        try
        {
            var json = File.ReadAllText(sidecarPath.FullName);

            // Deserialize.
            var sideCar = JsonSerializer.Deserialize<On1Sidecar>(json);

            if (sideCar != null)
            {
                var photo = sideCar.photos.Values.FirstOrDefault();

                if (photo != null)
                    result = photo.metadata;
            }
        }
        catch (Exception ex)
        {
            log.Warning($"Unable to load On1 Sidecar data from {sidecarPath.FullName}: {ex.Message}");
        }

        return result;
    }
}