using CleanArchitecture.Blazor.Application.Common.Utils;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Color = SixLabors.ImageSharp.Color;
using Image = SixLabors.ImageSharp.Image;
using PointF = SixLabors.ImageSharp.PointF;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using Size = SixLabors.ImageSharp.Size;
using SixLabors.ImageSharp;
using Serilog;
using ILogger = Serilog.ILogger;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Drawing.Processing;
using DocumentFormat.OpenXml.Drawing.Charts;
using System.IO;
using SkiaSharp;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class ImageSharpProcessor : IImageProcessor, IHashProvider
{
    private static readonly ILogger Logging = Log.ForContext(typeof(SkiaSharpProcessor));
    private static FontCollection? fontCollection;
    private static readonly string[] s_imageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".tga", ".gif", ".bmp" };

    public ImageSharpProcessor()
    {
        // lets switch out the default encoder for jpeg to one
        // that saves at 90 quality
        Configuration.Default.ImageFormatsManager.SetEncoder(JpegFormat.Instance, new JpegEncoder
        {
            Quality = 90
        });
    }

    /// <summary>
    ///     Get the perceptual hash
    /// </summary>
    /// <param name="path"></param>
    /// <returns>A hex string representing the hash</returns>
    public string GetPerceptualHash(string path)
    {
        // PerceptualHash, DifferenceHash, AverageHash
        var hashAlgorithm = new PerceptualHash();

        using var stream = File.OpenRead(path);

        var imageHash = hashAlgorithm.Hash(stream);

        var binaryString = Convert.ToString((long)imageHash, 16);

        return binaryString;
    }

    public static ICollection<string> SupportedFileExtensions => s_imageExtensions;

    /// <summary>
    ///     Resize using SixLabors ImageSharp, which can do 100 images in about 59s (2020 MacBook Air i5)
    /// </summary>
    /// <param name="source"></param>
    /// <param name="destFiles"></param>
    public async Task<IImageProcessResult> CreateThumbs(FileInfo source, IDictionary<FileInfo, IThumbConfig> destFiles)
    {
        IImageProcessResult result = new ImageProcessResult();
        var load = new Stopwatch("ImageSharpLoad");

        var largest = destFiles.Values
                               .OrderByDescending(x => x.width)
                               .First();

        DecoderOptions options = new() { TargetSize = new(width: largest.width, height: largest.height) };

        // Image.Load(string path) is a shortcut for our default type. 
        // Other pixel formats use Image.Load<TPixel>(string path))
        using var image = await Image.LoadAsync<Rgba32>(options, source.FullName);

        load.Stop();

        // We've got the image in memory. Create the hash. 
        result.ImageHash = GetHash(image);

        var orient = new Stopwatch("ImageSharpOrient");

        image.Mutate(x => x.AutoOrient());

        orient.Stop();

        var thumbs = new Stopwatch("ImageSharpThumbs");

        foreach (var pair in destFiles)
        {
            var dest = pair.Key;
            var config = pair.Value;
            var mode = ResizeMode.Max;

            var size = new Size { Height = config.height, Width = config.width };

            Logging.Information("Generating thumbnail for {0}: {1}x{2}", source.Name, size.Width, size.Height);

            if (config.cropToRatio)
                // For the smallest thumbs, we crop to fix the aspect exactly.
                mode = ResizeMode.Crop;

            var opts = new ResizeOptions
            {
                Mode = mode,
                Size = size,
                Sampler = KnownResamplers.Lanczos8
            };

            // Note, we don't clone and resize from the original image, because that's expensive.
            // So we always resize the previous image, which will be faster for each iteration
            // because each previous image is progressively smaller. 
            image.Mutate(x => x.Resize(opts));
            await image.SaveAsync(dest.FullName);

            result.ThumbsGenerated = true;
        }

        thumbs.Stop();

        return result;
    }

    public async Task GetCroppedFile(FileInfo source, int x, int y, int width, int height, FileInfo destFile)
    {
        var watch = new Stopwatch("ImageSharpCrop");

        // Image.Load(string path) is a shortcut for our default type. 
        // Other pixel formats use Image.Load<TPixel>(string path))
        using var image = await Image.LoadAsync<Rgba32>(source.FullName);

        var rect = new Rectangle(x, y, width, height);
        image.Mutate(x => x.AutoOrient());
        image.Mutate(x => x.Crop(rect));
        await image.SaveAsync(destFile.FullName);

        watch.Stop();
    }
    public async Task GetCropFaceFile(FileInfo source, int xMin, int yMin, int xMax, int yMax, FileInfo destFile)
    {
        var watch = new Stopwatch("ImageCropFace");

        // Image.Load(string path) is a shortcut for our default type. 
        // Other pixel formats use Image.Load<TPixel>(string path))
        // Enlarge by 10 pixels
        int enlargement = 10;
        xMin -= enlargement;
        yMin -= enlargement;
        xMax += enlargement;
        yMax += enlargement;

        using var image = await Image.LoadAsync<Rgba32>(source.FullName);
        // Crop rectangle boundary check
        xMin = Math.Max(xMin, 0);
        yMin = Math.Max(yMin, 0);
        xMax = Math.Min(xMax, image.Width);
        yMax = Math.Min(yMax, image.Height);

        int width = xMax - xMin;
        int height = yMax - yMin;
        
        var rect = new Rectangle(xMin, yMin, width, height);
        image.Mutate(x => x.Crop(rect));
        await image.SaveAsync(destFile.FullName);

        watch.Stop();
    }
    public async Task CropImage(FileInfo source, int x, int y, int width, int height, Stream stream)
    {
        var watch = new Stopwatch("ImageSharpCrop");

        // Image.Load(string path) is a shortcut for our default type. 
        // Other pixel formats use Image.Load<TPixel>(string path))
        using var image = await Image.LoadAsync<Rgba32>(source.FullName);

        var rect = new Rectangle(x, y, width, height);
        image.Mutate(x => x.AutoOrient());
        image.Mutate(x => x.Crop(rect));

        await image.SaveAsJpegAsync(stream);

        watch.Stop();
    }
    public async Task CropFaceImage(FileInfo source, int xMin, int yMin, int xMax, int yMax, Stream stream)
    {
        var watch = new Stopwatch("ImageSharpCropFace");
        // Enlarge by 10 pixels
        int enlargement = 10;
        xMin -= enlargement;
        yMin -= enlargement;
        xMax += enlargement;
        yMax += enlargement;

        // Image.Load(string path) is a shortcut for our default type. 
        // Other pixel formats use Image.Load<TPixel>(string path))
        using var image = await Image.LoadAsync<Rgba32>(source.FullName);
        // Crop rectangle boundary check
        xMin = Math.Max(xMin, 0);
        yMin = Math.Max(yMin, 0);
        xMax = Math.Min(xMax, image.Width);
        yMax = Math.Min(yMax, image.Height);
        int width = xMax - xMin;
        int height = yMax - yMin;

        var rect = new Rectangle(xMin, yMin, width, height);
        image.Mutate(x => x.Crop(rect));
        await image.SaveAsJpegAsync(stream);
        watch.Stop();
    }
    /// <summary>
    ///     Transforms an image to add a watermark.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <param name="waterMarkText"></param>
    public async Task TransformDownloadImage(string input, Stream output, IExportSettings config)
    {
        Logging.Information($" Running image transform for Watermark: {config.WatermarkText}");

        DecoderOptions options = new() { TargetSize = new(width: config.MaxImageSize, height: config.MaxImageSize) };

        using var img = await Image.LoadAsync(options, input);

        if (config.Size != ExportSize.FullRes)
        {
            var maxSize = config.MaxImageSize;

            var opts = new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size { Height = maxSize, Width = maxSize },
                Sampler = KnownResamplers.Lanczos3
            };

            // Rotate and resize.
            img.Mutate(x => x.AutoOrient().Resize(opts));
        }
        else
        {
            // Just rotate.
            img.Mutate(x => x.AutoOrient());
        }

        if (!string.IsNullOrEmpty(config.WatermarkText) && fontCollection != null)
        {
            // Apply the watermark if one's been specified.
            var fontFamily = fontCollection.Get("Arial");
            var font = fontFamily.CreateFont(10);

            img.Mutate(context => ApplyWaterMark(context, font, config.WatermarkText, Color.White));
        }

        await img.SaveAsync(output, img.Metadata.DecodedImageFormat);
    }

    /// <summary>
    ///     Initialises and installs the font for the watermarking.
    ///     TODO: In future we'll make the font configurable.
    /// </summary>
    /// <param name="folder"></param>
    public void SetFontPath(string folder)
    {
        try
        {
            fontCollection = new FontCollection();

            var fontPath = Path.Combine(folder, "arial.ttf");

            fontCollection.Add(fontPath);

            Logging.Information("Watermark font installed: {0}", fontPath);
        }
        catch (Exception ex)
        {
            Logging.Error($"Exception installing watermark font: {ex.Message}");
        }
    }

    /// <summary>
    ///     Create an SHA1 hash from the image data (pixels only) to allow us to find
    ///     duplicate images. Note that this ignores EXIF metadata, so the hash will
    ///     find duplicate images even if the metadata is different.
    /// </summary>
    /// <param name="source"></param>
    /// <returns>String hash of the image data</returns>
    public static string GetHash(Image<Rgba32> image)
    {
        var result = string.Empty;

        try
        {
            var hashWatch = new Stopwatch("CalcImageHash");
            var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

            image.ProcessPixelRows(pixelAccessor =>
            {
                for (var y = 0; y < pixelAccessor.Height; y++)
                {
                    var pixelRowSpan = pixelAccessor.GetRowSpan(y);

                    var rgbaBytes = MemoryMarshal.AsBytes(pixelRowSpan).ToArray();
                    hash.AppendData(rgbaBytes);
                }
            });

            result = hash.GetHashAndReset().ToHex(true);
            hashWatch.Stop();
            Logging.Information($"Hashed image ({result}) in {hashWatch.HumanElapsedTime}");
        }
        catch (Exception ex)
        {
            Logging.Error($"Exception while calculating hash: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    ///     Draw rectangles onto a file
    /// </summary>
    /// <param name="path"></param>
    /// <param name="rects"></param>
    /// <param name="output"></param>
    public static void DrawRects(string path, List<Rectangle> rects, string output)
    {
        using var image = Image.Load<Rgba32>(path);

        foreach (var rect in rects)
        {
            var pen = new Pen(Color.HotPink, 7);

            image.Mutate(x => x.AutoOrient());
            image.Mutate(x => x.Draw(pen, rect));
        }

        image.Save(output);
    }


    /// <summary>
    ///     Given a SixLabours ImageSharp image context, applies a watermark text overlay
    ///     to the bottom right corner in the given font and colour.
    /// </summary>
    /// <param name="processingContext"></param>
    /// <param name="font"></param>
    /// <param name="text"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    private static IImageProcessingContext ApplyWaterMark(IImageProcessingContext processingContext,
        Font font, string text, Color color)
    {
        var imgSize = processingContext.GetCurrentSize();

        // measure the text size
        var size = TextMeasurer.Measure(text, new TextOptions(font));

        var ratio = 4; // Landscape, we make the text 25% of the width

        if (imgSize.Width >= imgSize.Height)
            // Landscape - make it 1/6 of the width
            ratio = 6;

        float quarter = imgSize.Width / ratio;

        // We want the text width to be 25% of the width of the image
        var scalingFactor = quarter / size.Width;

        // create a new font
        var scaledFont = new Font(font, scalingFactor * font.Size);

        // 5% padding from the edge
        var fivePercent = quarter / 20;

        // 5% from the bottom right.
        var position = new PointF(imgSize.Width - fivePercent, imgSize.Height - fivePercent);

        var textOptions = new TextOptions(scaledFont)
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        return processingContext.DrawText(textOptions, text, color);
    }
}
