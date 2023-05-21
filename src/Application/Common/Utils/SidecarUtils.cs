﻿using  CleanArchitecture.Blazor.Application.BackendServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;
using XmpCore;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;

namespace CleanArchitecture.Blazor.Application.Common.Utils;
public static class SidecarUtils
{
    public enum SidecarType
    {
        XMP,
        ON1
    }

    private static readonly string[] s_sidecarExtensions = { ".on1", ".xmp" };
    private static readonly ILogger Logging = Log.ForContext(typeof(SidecarUtils));
    public static ICollection<string> SidecarExtensions => s_sidecarExtensions;

    /// <summary>
    ///     Returns true if this is a known, supported sidecar type.
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static bool IsSidecarFileType(this FileInfo filename)
    {
        if (filename.IsHidden())
            return false;

        return SidecarExtensions.Any(x => x.Equals(filename.Extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Given a sidecar object, parses the files (ON1 or XMP) and pulls out
    ///     the list of keywords in the sidecar file.
    /// </summary>
    /// <param name="sidecar"></param>
    /// <returns></returns>
    public static IList<string> GetKeywords(this ImageSideCar sidecar)
    {
        var sideCarTags = new List<string>();

        // If there's an On1 sidecar, read it
        try
        {
            if (sidecar.Type == SidecarType.ON1)
            {
                var on1MetaData = On1Sidecar.LoadMetadata(sidecar.Filename);

                if (on1MetaData != null && on1MetaData.Keywords != null && on1MetaData.Keywords.Any())
                    sideCarTags = on1MetaData.Keywords
                        .Select(x => x.Trim())
                        .ToList();
            }

            // If there's an XMP sidecar
            if (sidecar.Type == SidecarType.XMP)
            {
                using var stream = File.OpenRead(sidecar.Filename.FullName);
                var xmp = XmpMetaFactory.Parse(stream);

                var xmpKeywords = xmp.Properties.FirstOrDefault(x => x.Path == "pdf:Keywords");

                if (xmpKeywords != null)
                    sideCarTags = xmpKeywords.Value.Split(",")
                        .Select(x => x.Trim())
                        .ToList();
            }
        }
        catch (Exception ex)
        {
            Logging.Error($"Exception processing {sidecar.Type} sidecar: {sidecar.Filename.FullName}: {ex.Message}");
        }

        return sideCarTags;
    }

    /// <summary>
    ///     For an image, see if there's a sidecar on disk, and if so return
    ///     an object representing that sidecar.
    /// </summary>
    /// <param name="img"></param>
    /// <returns></returns>
    public static ImageSideCar GetSideCar(this Image img)
    {
        ImageSideCar result = null;

        var sidecarSearch = Path.ChangeExtension(img.Name, "*");
        var dir = new DirectoryInfo(img.Folder.Path);
        var files = dir.GetFiles(sidecarSearch);

        if (files.Any())
        {
            var on1Sidecar = files.FirstOrDefault(x => x.Extension.Equals(".on1", StringComparison.OrdinalIgnoreCase));

            if (on1Sidecar != null)
            {
                result = new ImageSideCar { Filename = on1Sidecar, Type = SidecarType.ON1 };
            }
            else
            {
                var xmpSidecar =
                    files.FirstOrDefault(x => x.Extension.Equals(".xmp", StringComparison.OrdinalIgnoreCase));
                if (xmpSidecar != null) result = new ImageSideCar { Filename = xmpSidecar, Type = SidecarType.XMP };
            }
        }

        return result;
    }

    public class ImageSideCar
    {
        public SidecarType Type { get; set; }
        public FileInfo Filename { get; set; }
    }
}
