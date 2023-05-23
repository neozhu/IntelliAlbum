using CleanArchitecture.Blazor.Application.Common.Configurations;
using CleanArchitecture.Blazor.Application.Common.Utils;
using CleanArchitecture.Blazor.Application.Services.BackendServices;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;
using Stopwatch = CleanArchitecture.Blazor.Application.Common.Utils.Stopwatch;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class ObjectDetectService : IProcessJobFactory, IRescanProvider
{
    private const string _requestRoot = "/images";
    private string _thumbnailRootFolder;
    private string _picturesRoot { get; set; }
    private static readonly int s_maxThreads = GetMaxThreads();
    private readonly ThumbSize UseThumbSize = ThumbSize.Big;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceSettings _serviceSettings;
    private readonly YoloAIService _yoloAIService;
    private readonly ILogger<ObjectDetectService> _logger;
    private readonly IStatusService _statusService;
    private readonly WorkService _workService;

    public ObjectDetectService(IServiceScopeFactory scopeFactory,
        ServiceSettings  serviceSettings,
        YoloAIService yoloAIService,
        ILogger<ObjectDetectService> logger,
        IStatusService statusService,
        WorkService workService)
    {
        _scopeFactory = scopeFactory;
        _serviceSettings = serviceSettings;
        _yoloAIService = yoloAIService;
        _thumbnailRootFolder = _serviceSettings.ThumbPath;
        _picturesRoot = _serviceSettings.SourceDirectory;
        _logger = logger;
        _statusService = statusService;
        _workService = workService;
        Synology = false;
        EnableObjectDetect = _serviceSettings.EnableObjectDetect;
       _workService.AddJobSource(this);
    }
    public  bool Synology { get; set; }

    public  bool EnableObjectDetect { get; set; } = true;

    public JobPriorities Priority => JobPriorities.ObjectDetection;

    public async Task<ICollection<IProcessJob>> GetPendingJobs(int maxJobs)
    {
        if (!EnableObjectDetect)
            return new ObjectDectectProcess[0];

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var images = await db.Images.Where(x =>x.ObjectDetectLastUpdated ==null && x.DetectObjectStatus == 0 &&
                               x.ThumbLastUpdated != null && x.MetaData!=null )
            .OrderByDescending(x => x.FileLastModDate)
            .Take(maxJobs)
            .Select(x => x.Id)
            .ToListAsync();

        var jobs = images.Select(x => new ObjectDectectProcess { ImageId = x, Service = this })
            .ToArray();
        //To avoid duplicate execution,modify the ProcessStatus,0=pending,1=processing,2=done,3=error
        await db.Images.Where(x => images.Contains(x.Id)).ExecuteUpdateAsync(x => x.SetProperty(y => y.DetectObjectStatus, y => 1));
        return jobs;
    }

    public async Task MarkAllForScan()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        // TODO: Abstract this once EFCore Bulkextensions work in efcore 6
        var updated = await db.Images.ExecuteUpdateAsync(x=>x.SetProperty(y=>y.ObjectDetectLastUpdated, v=>null)
                                                             .SetProperty(y => y.DetectObjectStatus, y => 0));

        _statusService.UpdateStatus($"All {updated} images flagged for object detect re-scan.");
    }

    public async Task MarkFolderForScan(int folderId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var updated = await db.Images.Where(x=>x.FolderId==folderId)
                                     .ExecuteUpdateAsync( x=>x.SetProperty(y=>y.ObjectDetectLastUpdated, v=>null)
                                                              .SetProperty(y => y.DetectObjectStatus, y => 0));

        if (updated != 0)
            _statusService.UpdateStatus($"{updated} images in folder flagged for object detect re-scan.");
    }

    public async Task MarkImagesForScan(ICollection<int> imageIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var imageIdList = string.Join(",", imageIds);
        //var sql = $"Update imagemetadata Set ThumbLastUpdated = null where imageid in ({imageIdList})";
        // TODO: Abstract this once EFCore Bulkextensions work in efcore 6
        await db.Images.Where(x=>imageIds.Contains(x.Id))
                       .ExecuteUpdateAsync(x => x.SetProperty(y => y.ObjectDetectLastUpdated, v => null)
                                                 .SetProperty(y => y.DetectObjectStatus, y => 0));
        var msgText = imageIds.Count == 1 ? "Image" : $"{imageIds.Count} images";
        _statusService.UpdateStatus($"{msgText} flagged for object detect re-scan.");
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
    private async Task ProcessObjectDetectScan()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        _logger.LogDebug("Starting object detect scan...");

        var complete = false;

        while (!complete)
        {
            _logger.LogDebug("Querying DB for pending object detect...");

            var watch = new Stopwatch("GetObjectDetectQueue");

            // TODO: Change this to a consumer/producer thread model
            var imagesToScan = db.Images.Where(x => x.ObjectDetectLastUpdated == null)
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
                    $"Found {imagesToScan.Count()} images requiring object detect scan. First image is {imagesToScan[0].FullPath}.");

                watch = new Stopwatch("ObjectDetectBatch", 100000);

                // We always ignore existing thumbs when generating
                // them based onthe ThumbLastUpdated date.
                const bool forceRegeneration = false;

                _logger.LogDebug($"Executing object detect scan in parallel with {s_maxThreads} threads.");

                try
                {
                    await imagesToScan.ExecuteInParallel(async img => await ObjectDetectScan(img),
                        s_maxThreads);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,$"Exception during parallelised object detect scan");
                }

                // Write the timestamps for the newly-generated thumbs.
                _logger.LogDebug("Writing object detect scan timestamp updates to DB.");

                var updateWatch = new Stopwatch("BulkUpdateImageObject");
                db.Images.UpdateRange(imagesToScan.ToList());
                await db.SaveChangesAsync(CancellationToken.None);
                updateWatch.Stop();

                watch.Stop();

                if (imagesToScan.Length > 1)
                    _statusService.UpdateStatus(
                        $"Completed object detect scan batch ({imagesToScan.Length} images in {watch.HumanElapsedTime}).");

                Action<string> logFunc = s =>  _logger.LogInformation(s);
                Stopwatch.WriteTotals(logFunc);
            }
            else
            {
                _logger.LogDebug("No images found to scan.");
            }
        }
    }

    /// <summary>
    ///     object detect scan for an image.
    /// </summary>
    /// <param name="sourceImage"></param>
    /// <returns></returns>
    public async Task ObjectDetectScan(Image sourceImage)
    {
        await ObjectDetect(sourceImage);
    }

    /// <summary>
    ///    object detect scan for an image.
    /// </summary>
    /// <param name="imageId"></param>
    /// <returns></returns>
    public async Task ObjectDetectScan(int imageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();
        var image =await db.Images.Where(x=>x.Id==imageId).Include(x=>x.Folder).FirstAsync();
        // Mark the image as done, so that if anything goes wrong it won't go into an infinite loop spiral
        image.ObjectDetectLastUpdated = DateTime.UtcNow;
        if (image.MetaData is not null)
        {
            image.MetaData.ObjectDetectLastUpdated = image.ObjectDetectLastUpdated;
        }
        await ObjectDetectScan(image);
        db.Images.Update(image);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    ///     Process the file on disk to create a set of thumbnails.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="forceRegeneration"></param>
    /// <returns></returns>
    public async Task ObjectDetect(Image image, ThumbSize size = ThumbSize.Big)
    {
        List<ImageObject>? imageObjects = null;
        var imagePath = new FileInfo(image.FullPath);
        try
        {
            if (imagePath.Exists)
            {
                var thumbImagePath = new FileInfo(GetThumbPath(imagePath, size));
                if (thumbImagePath.Exists)
                {
                    var watch = new Stopwatch("ObjectDetect", 60000);
                    try
                    {
                        var result = await _yoloAIService.DetectObject(imagePath);
                        image.DetectObjectStatus = 2;
                        
                        if (result.DetectObjects?.Any() ?? false)
                        {
                            if (image.ImageTags is null)
                            {
                                image.ImageTags = new List<Tag>();
                            }
                            if(image.Classification is null)
                            {
                                image.Classification = new List<ImageClassification>();
                            }
                            imageObjects = new List<ImageObject>();
                            foreach(var obj in result.DetectObjects)
                            {
                                image.ImageTags.Add(new Tag() { Keyword = obj.Name });
                                image.Classification.Add(new ImageClassification() {  Label = obj.Name, Score=obj.Confidence });
                                if (obj.Name.Equals("person", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    imageObjects.Add(new ImageObject()
                                    {
                                        Type= ObjectTypes.Person,
                                        Tag=new Tag() { Keyword = obj.Name },
                                        Score = obj.Confidence,
                                        RectX = Convert.ToInt32(obj.Bbox.Xmin),
                                        RectY = Convert.ToInt32(obj.Bbox.Ymin),
                                        RectHeight = Convert.ToInt32(obj.Bbox.Ymax),
                                        RectWidth = Convert.ToInt32(obj.Bbox.Xmax),
                                    });
                                }
                                else
                                {
                                    imageObjects.Add(new ImageObject()
                                    {
                                        Type = ObjectTypes.Object,
                                        Tag = new Tag() { Keyword = obj.Name },
                                        Score = obj.Confidence,
                                        RectX = Convert.ToInt32(obj.Bbox.Xmin),
                                        RectY = Convert.ToInt32(obj.Bbox.Ymin),
                                        RectHeight = Convert.ToInt32(obj.Bbox.Ymax),
                                        RectWidth = Convert.ToInt32(obj.Bbox.Xmax),
                                    });
                                }
                                
                            }
                        }
                        
                        image.ImageObjects = imageObjects;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "object detect failed for {0}", imagePath);
                        image.DetectObjectStatus = 3;
                    }
                    finally
                    {
                        watch.Stop();
                        _logger.LogDebug(
                            $"{thumbImagePath.Name} object detect finished in {watch.HumanElapsedTime}");
                    }
                }
                else
                {
                    _logger.LogDebug("thumb file not exists for {0}", thumbImagePath);
                    image.DetectObjectStatus = 3;
                }

            }
            else
            {
                _logger.LogWarning("Skipping object detect scan for missing file...");
                image.ProcessThumbStatus = 3;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception object detect scan for {0}", imagePath);
        }
    }

    public class ObjectDectectProcess : IProcessJob
    {
        public int ImageId { get; set; }
        public ObjectDetectService Service { get; set; }
        public bool CanProcess => true;
        public string Name => "Object Detection";
        public string Description => $"Object detect for image id:{ImageId}";
        public JobPriorities Priority => JobPriorities.Thumbnails;

        public async Task Process()
        {
            await Service.ObjectDetectScan(ImageId);
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
