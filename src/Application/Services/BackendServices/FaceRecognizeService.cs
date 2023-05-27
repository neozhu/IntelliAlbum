using CleanArchitecture.Blazor.Application.Common.Configurations;
using CleanArchitecture.Blazor.Application.Common.Utils;
using CleanArchitecture.Blazor.Application.Services.BackendServices;
using FluentEmail.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;
using Stopwatch = CleanArchitecture.Blazor.Application.Common.Utils.Stopwatch;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class FaceRecognizeService : IProcessJobFactory, IRescanProvider
{
    private const string _requestRoot = "/images";
    private string _thumbnailRootFolder;
    private string _picturesRoot { get; set; }
    private static readonly int s_maxThreads = GetMaxThreads();
    private readonly ThumbSize UseThumbSize = ThumbSize.Big;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ImageSharpProcessor _imageSharpProcessor;
    private readonly ServiceSettings _serviceSettings;
    private readonly FaceAIService _faceAIService;
    private readonly ILogger<FaceRecognizeService> _logger;
    private readonly IStatusService _statusService;
    private readonly WorkService _workService;

    public FaceRecognizeService(IServiceScopeFactory scopeFactory,
        ImageSharpProcessor imageSharpProcessor,
        ServiceSettings serviceSettings,
        FaceAIService faceAIService,
        ILogger<FaceRecognizeService> logger,
        IStatusService statusService,
        WorkService workService)
    {
        _scopeFactory = scopeFactory;
        _imageSharpProcessor = imageSharpProcessor;
        _serviceSettings = serviceSettings;
        _faceAIService = faceAIService;
        _thumbnailRootFolder = _serviceSettings.ThumbPath;
        _picturesRoot = _serviceSettings.SourceDirectory;
        _logger = logger;
        _statusService = statusService;
        _workService = workService;
        Synology = false;
        EnableFaceRecognition = _serviceSettings.EnableFaceRecognition;
        _workService.AddJobSource(this);
    }
    public bool Synology { get; set; }

    public bool EnableFaceRecognition { get; set; } = true;

    public JobPriorities Priority => JobPriorities.FaceRecognition;

    public async Task<ICollection<IProcessJob>> GetPendingJobs(int maxJobs)
    {
        if (!EnableFaceRecognition)
            return new FaceRecognizeProcess[0];

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var images = await db.Images.Where(x => x.FaceRecognizeLastUpdated == null && x.RecognizeFaceStatus == 0 && x.HasPerson == true &&
                               x.FaceDetectLastUpdated!=null && x.DetectFaceStatus==2 &&
                               x.FaceDetections != null)
            .OrderByDescending(x => x.FileLastModDate)
            .Take(maxJobs)
            .Select(x => x.Id)
            .ToListAsync();

        var jobs = images.Select(x => new FaceRecognizeProcess { ImageId = x, Service = this })
            .ToArray();
        //To avoid duplicate execution,modify the ProcessStatus,0=pending,1=processing,2=done,3=error
        await db.Images.Where(x => images.Contains(x.Id)).ExecuteUpdateAsync(x => x.SetProperty(y => y.RecognizeFaceStatus, y => 1));
        return jobs;
    }

    public async Task MarkAllForScan()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        // TODO: Abstract this once EFCore Bulkextensions work in efcore 6
        var updated = await db.Images.ExecuteUpdateAsync(x => x.SetProperty(y => y.FaceRecognizeLastUpdated, v => null)
                                                             .SetProperty(y => y.RecognizeFaceStatus, y => 0));

        _statusService.UpdateStatus($"All {updated} images flagged for face recognize re-scan.");
    }

    public async Task MarkFolderForScan(int folderId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var updated = await db.Images.Where(x => x.FolderId == folderId)
                                     .ExecuteUpdateAsync(x => x.SetProperty(y => y.FaceRecognizeLastUpdated, v => null)
                                                              .SetProperty(y => y.RecognizeFaceStatus, y => 0));

        if (updated != 0)
            _statusService.UpdateStatus($"{updated} images in folder flagged for face recognize re-scan.");
    }

    public async Task MarkImagesForScan(ICollection<int> imageIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var imageIdList = string.Join(",", imageIds);
        //var sql = $"Update imagemetadata Set ThumbLastUpdated = null where imageid in ({imageIdList})";
        // TODO: Abstract this once EFCore Bulkextensions work in efcore 6
        await db.Images.Where(x => imageIds.Contains(x.Id))
                       .ExecuteUpdateAsync(x => x.SetProperty(y => y.FaceRecognizeLastUpdated, v => null)
                                                 .SetProperty(y => y.RecognizeFaceStatus, y => 0));
        var msgText = imageIds.Count == 1 ? "Image" : $"{imageIds.Count} images";
        _statusService.UpdateStatus($"{msgText} flagged for face recognize re-scan.");
        _workService.FlagNewJobs(this);
    }

    /// <summary>
    ///     TODO - move this somewhere better
    /// </summary>
    /// <returns></returns>
    public static int GetMaxThreads()
    {
        //if (Debugger.IsAttached)
        //    return 1;

        return Math.Max(Environment.ProcessorCount / 2, 2);
    }



    /// <summary>
    ///     Given a particular image, calculates the path and filename of the associated
    ///     thumbnail for that image and size.
    ///     TODO: Use the Thumbnail Last gen date here to avoid passing back images with no thumbs?
    /// </summary>
    /// <param name="imageFile"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public string GetThumbPath(FileInfo imageFile, ThumbSize size)
    {
        string thumbPath;

        if (Synology)
        {
            // Syno thumbs go in a subdir of the location of the image
            var thumbFileName = $"SYNOPHOTO_THUMB_{GetSizePostFix(size).ToUpper()}.jpg";
            thumbPath = Path.Combine(imageFile.DirectoryName, "@eaDir", imageFile.Name, thumbFileName);
        }
        else
        {
            var extension = imageFile.Extension;

            // Keep the extension if it's JPG, but otherwise change it to JPG (for HEIC etc).
            if (!extension.Equals(".JPG", StringComparison.OrdinalIgnoreCase))
                extension = ".JPG";

            var baseName = Path.GetFileNameWithoutExtension(imageFile.Name);
            var relativePath = imageFile.DirectoryName.MakePathRelativeTo(_picturesRoot);
            var thumbFileName = $"{baseName}_{GetSizePostFix(size)}{extension}";
            thumbPath = Path.Combine(_thumbnailRootFolder, relativePath, thumbFileName);
        }

        return thumbPath;
    }

    private string GetSizePostFix(ThumbSize size)
    {
        return size switch
        {
            ThumbSize.ExtraLarge => "xl",
            ThumbSize.Large => "l",
            ThumbSize.Big => "b",
            ThumbSize.Medium => "m",
            ThumbSize.Small => "s",
            _ => "PREVIEW"
        };
    }



    /// <summary>
    ///     Queries the database to find any images that haven't had a thumbnail
    ///     generated, and queues them up to process the thumb generation.
    /// </summary>
    private async Task ProcessFaceRecognizeScan()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        _logger.LogDebug("Starting face recognize scan...");

        var complete = false;

        while (!complete)
        {
            _logger.LogDebug("Querying DB for pending face recognize...");

            var watch = new Stopwatch("GetFaceRecognizeQueue");

            // TODO: Change this to a consumer/producer thread model
            var imagesToScan = db.Images.Where(x => x.FaceRecognizeLastUpdated == null && x.RecognizeFaceStatus == 0 &&
                                                x.FaceDetections != null
                                )
                .OrderByDescending(x => x.FileLastModDate)
                .Take(100)
                .Include(x => x.MetaData)
                .Include(x => x.Hash)
                .Include(x => x.Folder)
                .ToArray();

            watch.Stop();

            complete = !imagesToScan.Any();

            if (!complete)
            {
                _logger.LogDebug(
                    $"Found {imagesToScan.Count()} images requiring face recognize scan. First image is {imagesToScan[0].FullPath}.");

                watch = new Stopwatch("FaceRecognizeBatch", 100000);
                _logger.LogDebug($"Executing face recognize scan in parallel with {s_maxThreads} threads.");

                try
                {
                    await imagesToScan.ExecuteInParallel(async img => await FaceRecognizeScan(img),
                        s_maxThreads);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception during parallelised face recognize scan");
                }

                // Write the timestamps for the newly-generated thumbs.
                _logger.LogDebug("Writing face recognize scan timestamp updates to DB.");

                var updateWatch = new Stopwatch("BulkUpdateImageObject");
                db.Images.UpdateRange(imagesToScan.ToList());
                await db.SaveChangesAsync(CancellationToken.None);
                updateWatch.Stop();

                watch.Stop();

                if (imagesToScan.Length > 1)
                    _statusService.UpdateStatus(
                        $"Completed face recognize scan batch ({imagesToScan.Length} images in {watch.HumanElapsedTime}).");

                Action<string> logFunc = s => _logger.LogInformation(s);
                Stopwatch.WriteTotals(logFunc);
            }
            else
            {
                _logger.LogDebug("No images found to scan.");
            }
        }
    }

    /// <summary>
    ///     object recognize scan for an image.
    /// </summary>
    /// <param name="sourceImage"></param>
    /// <returns></returns>
    public async Task FaceRecognizeScan(Image sourceImage)
    {
        await FaceRecognize(sourceImage);
    }

    /// <summary>
    ///    object recognize scan for an image.
    /// </summary>
    /// <param name="imageId"></param>
    /// <returns></returns>
    public async Task FaceRecognizeScan(int imageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();
        var image = await db.Images.Where(x => x.Id == imageId).Include(x => x.Folder).FirstAsync();
        // Mark the image as done, so that if anything goes wrong it won't go into an infinite loop spiral
        image.FaceRecognizeLastUpdated = DateTime.UtcNow;
        if (image.MetaData is not null)
        {
            image.MetaData.FaceRecognizeLastUpdated = image.FaceRecognizeLastUpdated;
        }
        await FaceRecognizeScan(image);
        db.Images.Update(image);
        await db.SaveChangesAsync(CancellationToken.None);
    }
    public async Task FaceRecognize(FaceDetection face)
    {
        var thumbface = new FileInfo(face.ThumbUrl);
        if (thumbface.Exists)
        {
            var result = await _faceAIService.RecognizeFace(thumbface);
            if (result.Result.Any())
            {
                var subject = result.Result.First().Similarities.FirstOrDefault()?.Subject;
                var similarity = result.Result.First().Similarities.FirstOrDefault()?.SimilarityScore ?? 0;
                if (similarity >= 0.8)
                {
                    face.Name = subject;
                }
                face.Similarity = similarity;
                _logger.LogDebug($"face recognize {subject}:{similarity}");
            }
            else
            {
                _logger.LogDebug($"face recognize fail:{face.Name}");
            }
        }
    }
    /// <summary>
    ///     Process the file on disk to create a set of thumbnails.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="forceRegeneration"></param>
    /// <returns></returns>
    public async Task FaceRecognize(Image image)
    {
        List<FaceDetection>? imageDetections = null;
        var imagePath = new FileInfo(image.FullPath);
        try
        {
            if (imagePath.Exists)
            {
                if (image.FaceDetections?.Any() ?? false)
                {
                    await image.FaceDetections.ExecuteInParallel(async (x) => await FaceRecognize(x), s_maxThreads);
                    var nametag = image.FaceDetections.Where(x=>!string.IsNullOrEmpty(x.Name)).Select(x => x.Name).Distinct().Select(x=>new Tag { Keyword=x }).ToArray();
                    if(image.ImageTags is null)
                    {
                        image.ImageTags = new List<Tag>();
                    }
                    foreach(var tag in nametag)
                    {
                        if (!image.ImageTags.Any(x => x.Keyword == tag.Keyword))
                        {
                            image.ImageTags.Add(tag);
                        }
                    }
                    
                    image.RecognizeFaceStatus = 2;
                }
            }
            else
            {
                _logger.LogWarning("Skipping face recognize scan for missing file...");
                image.ProcessThumbStatus = 3;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception face recognize scan for {0}", imagePath);
        }
    }

    public class FaceRecognizeProcess : IProcessJob
    {
        public int ImageId { get; set; }
        public FaceRecognizeService Service { get; set; }
        public bool CanProcess => true;
        public string Name => "Face Recognition";
        public string Description => $"Face recognize for image id:{ImageId}";
        public JobPriorities Priority => JobPriorities.Thumbnails;

        public async Task Process()
        {
            await Service.FaceRecognizeScan(ImageId);
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
