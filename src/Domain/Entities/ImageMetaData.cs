using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities;

public class ImageMetaData
{
    public DateTime DateTaken { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSize { get; set; }
    public double AspectRatio { get; set; } = 1;
    public int Rating { get; set; } // 1-5, stars
    public string? Caption { get; set; }
    public string? Copyright { get; set; }
    public string? Credit { get; set; }
    public string? Description { get; set; }
    public string? ISO { get; set; }
    public string? FNum { get; set; }
    public string? Exposure { get; set; }
    public bool FlashFired { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Camera? Camera { get; set; }

    public virtual Lens? Lens { get; set; }

    public string? DominantColor { get; set; }
    public string? AverageColor { get; set; }

    // The date that this metadata was read from the image
    // If this is older than Image.LastUpdated, the image
    // will be re-indexed
    public DateTime? LastUpdated { get; set; }

    // Date the thumbs were last created. If this is null
    // the thumbs will be regenerated
    public DateTime? ThumbLastUpdated { get; set; }

    // Date we last performed face/object/image recognition
    // If this is null, AI will be reprocessed
    public DateTime? ObjectDetectLastUpdated { get; set; }

    public DateTime? FaceDetectLastUpdated { get; set; }
    public DateTime? FaceRecognizeLastUpdated { get; set; }

    public virtual List<DirectoryBase>? EXIFData { get; set; }

    public string? MimeType { get; set; }
    public string? FileType { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public override string ToString()
    {
        return $"{MimeType} {Category} {Caption} {Description} {City} { Copyright} {ISO} {FNum } {Exposure} {Camera?.Model} {Lens?.Model} {DateTaken}";
    }
    public string ToImageInformation()
    {
        return $"{FileType}, {Width} x {Height}, {GetBytesReadable(FileSize)}";
    }

    private string GetBytesReadable(long i)
    {
        // Get absolute value
        long absolute_i = (i < 0 ? -i : i);
        // Determine the suffix and readable value
        string suffix;
        double readable;
        if (absolute_i >= 0x1000000000000000) // Exabyte
        {
            suffix = "EB";
            readable = (i >> 50);
        }
        else if (absolute_i >= 0x4000000000000) // Petabyte
        {
            suffix = "PB";
            readable = (i >> 40);
        }
        else if (absolute_i >= 0x10000000000) // Terabyte
        {
            suffix = "TB";
            readable = (i >> 30);
        }
        else if (absolute_i >= 0x40000000) // Gigabyte
        {
            suffix = "GB";
            readable = (i >> 20);
        }
        else if (absolute_i >= 0x100000) // Megabyte
        {
            suffix = "MB";
            readable = (i >> 10);
        }
        else if (absolute_i >= 0x400) // Kilobyte
        {
            suffix = "KB";
            readable = i;
        }
        else
        {
            return i.ToString("0 B"); // Byte
        }
        // Divide by 1024 to get fractional value
        readable = (readable / 1024);
        // Return formatted number with suffix
        return readable.ToString("0 ") + suffix;
    }
}

    public class Camera
    {
        public string? Model { get; set; }
        public string? Make { get; set; }
        public string? Serial { get; set; }
    }
public class DirectoryBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}
public class Lens
{
    public string? Model { get; set; }
    public string? Make { get; set; }
    public string? Serial { get; set; }
}