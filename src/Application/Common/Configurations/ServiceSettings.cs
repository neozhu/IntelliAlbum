using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Common.Configurations;

public class ServiceSettings
{
    /// <summary>
    ///     App configuration key constraint
    /// </summary>
    public const string Key = nameof(ServiceSettings);
    /// <summary>
    /// Base folder for photographs.
    /// </summary>
    public string SourceDirectory { get; set; } = string.Empty;
    /// <summary>
    /// Thumbnail cache path (ignored if --syno specified)
    /// </summary>
    public string ThumbPath { get; set; } = "./wwwroot/thumbs";
    /// <summary>
    /// Enable indexing
    /// </summary>
    public bool EnableIndexing = true;
    /// <summary>
    /// Generate Thumbnails
    /// </summary>
    public bool GenerateThumbnails { get; set; }
    /// <summary>
    /// Enable object detect
    /// </summary>
    public bool EnableObjectDetect { get; set; }

    public bool EnableFaceDetection { get; set; }
    public bool EnableFaceRecognition { get; set; }
}

