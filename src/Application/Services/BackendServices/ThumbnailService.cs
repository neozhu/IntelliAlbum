using CleanArchitecture.Blazor.Application.Common.Configurations;
using CleanArchitecture.Blazor.Application.Common.Utils;
using CleanArchitecture.Blazor.Application.Services.BackendServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;
using Stopwatch = CleanArchitecture.Blazor.Application.Common.Utils.Stopwatch;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class ThumbnailService : IProcessJobFactory, IRescanProvider
{
    private const string _requestRoot = "/images";
    private static string _thumbnailRootFolder;
    private static readonly int s_maxThreads = GetMaxThreads();


    /// <summary>
    ///     This is the set of thumb resolutions that Syno PhotoStation and moments expects
    /// </summary>
    private static readonly IThumbConfig[] thumbConfigs =
    {
        new ThumbConfig { width = 2000, height = 2000, size = ThumbSize.ExtraLarge, useAsSource = true, batchGenerate = false },
        new ThumbConfig { width = 800, height = 800, size = ThumbSize.Large, useAsSource = true },
        new ThumbConfig { width = 640, height = 640, size = ThumbSize.Big, batchGenerate = false },
        new ThumbConfig { width = 320, height = 320, size = ThumbSize.Medium },
        new ThumbConfig { width = 160, height = 120, size = ThumbSize.Preview, cropToRatio = true, batchGenerate = false },
        new ThumbConfig { width = 120, height = 120, size = ThumbSize.Small, cropToRatio = true }
    };


    private readonly ImageProcessService _imageProcessingService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceSettings _serviceSettings;
    private readonly ILogger<ThumbnailService> _logger;
    private readonly IStatusService _statusService;
    private readonly WorkService _workService;

    public ThumbnailService(IServiceScopeFactory scopeFactory,
        ServiceSettings  serviceSettings,
        ILogger<ThumbnailService> logger,
        IStatusService statusService,
        ImageProcessService imageService,
        WorkService workService)
    {
       
        _scopeFactory = scopeFactory;
        _serviceSettings = serviceSettings;
        _logger = logger;
        _statusService = statusService;
        _imageProcessingService = imageService;
        _workService = workService;
        _thumbnailRootFolder = _serviceSettings.ThumbPath;
        PicturesRoot = _serviceSettings.SourceDirectory;
        Synology = false;
        EnableThumbnailGeneration = _serviceSettings.GenerateThumbnails;
        setThumbnailRoot(_thumbnailRootFolder);
       _workService.AddJobSource(this);
    }

    public  string PicturesRoot { get; set; }
    public  bool UseGraphicsMagick { get; set; }
    public  bool Synology { get; set; }
    public  string RequestRoot => _requestRoot;
    public  bool EnableThumbnailGeneration { get; set; } = true;

    public JobPriorities Priority => JobPriorities.Thumbnails;

    public async Task<ICollection<IProcessJob>> GetPendingJobs(int maxJobs)
    {
        if (!EnableThumbnailGeneration)
            return new ThumbProcess[0];

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var images = await db.Images.Where(x => x.ThumbLastUpdated == null && x.MetaData!=null && x.ProcessThumbStatus==0)
            .OrderByDescending(x => x.FileLastModDate)
            .Take(maxJobs)
            .Select(x => x.Id)
            .ToListAsync();

        var jobs = images.Select(x => new ThumbProcess { ImageId = x, Service = this })
            .ToArray();
        //To avoid duplicate execution,modify the ProcessStatus,0=pending,1=processing,2=done,3=error
        await db.Images.Where(x => images.Contains(x.Id)).ExecuteUpdateAsync(x => x.SetProperty(y => y.ProcessThumbStatus, y => 1));
        return jobs;
    }

    public async Task MarkAllForScan()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        // TODO: Abstract this once EFCore Bulkextensions work in efcore 6
        var updated = await db.Images.ExecuteUpdateAsync(x=>x.SetProperty(y=>y.ThumbLastUpdated,v=>null)
                                                             .SetProperty(y => y.ProcessThumbStatus, y => 0));

        _statusService.UpdateStatus($"All {updated} images flagged for thumbnail re-generation.");
    }

    public async Task MarkFolderForScan(int folderId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var updated = await db.Images.Where(x=>x.FolderId==folderId)
                                     .ExecuteUpdateAsync( x=>x.SetProperty(y=>y.ThumbLastUpdated,v=>null)
                                                              .SetProperty(y => y.ProcessThumbStatus, y => 0));

        if (updated != 0)
            _statusService.UpdateStatus($"{updated} images in folder flagged for thumbnail re-generation.");
    }

    public async Task MarkImagesForScan(ICollection<int> imageIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var imageIdList = string.Join(",", imageIds);
        var sql = $"Update imagemetadata Set ThumbLastUpdated = null where imageid in ({imageIdList})";
        // TODO: Abstract this once EFCore Bulkextensions work in efcore 6
        await db.Images.Where(x=>imageIds.Contains(x.Id))
                       .ExecuteUpdateAsync(x => x.SetProperty(y => y.ThumbLastUpdated, v => null)
                                                 .SetProperty(y => y.ProcessThumbStatus, y => 0));
        var msgText = imageIds.Count == 1 ? "Image" : $"{imageIds.Count} images";
        _statusService.UpdateStatus($"{msgText} flagged for thumbnail re-generation.");
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
    ///     Set the http thumbnail request root - this will be wwwroot or equivalent
    ///     and will be determined by the webserver we're being called from.
    /// </summary>
    /// <param name="rootFolder"></param>
    private void setThumbnailRoot(string rootFolder)
    {
        // Get the full absolute path.
        _thumbnailRootFolder = Path.GetFullPath(rootFolder);

        if (!Synology)
        {
            if (!Directory.Exists(_thumbnailRootFolder))
            {
                Directory.CreateDirectory(_thumbnailRootFolder);
                _logger.LogDebug("Created folder for thumbnails storage at {0}", _thumbnailRootFolder);
            }
            else
            {
                _logger.LogDebug("Initialised thumbnails storage at {0}", _thumbnailRootFolder);
            }
        }
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
            var relativePath = imageFile.DirectoryName.MakePathRelativeTo(PicturesRoot);
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
    ///     Gets the list of thumbnails sizes/specs to generate
    /// </summary>
    /// <param name="source"></param>
    /// <param name="ignoreExisting">Force the creation even if there's an existing file with the correct timestamp</param>
    /// <param name="altSource">If an existing thumbnail can be used as a source image, returns it</param>
    /// <returns></returns>
    private Dictionary<FileInfo, IThumbConfig> GetThumbConfigs(FileInfo source, bool forceRegeneration,
        out FileInfo altSource)
    {
        altSource = null;

        var thumbFileAndConfig = new Dictionary<FileInfo, IThumbConfig>();

        // First pre-check whether the thumbs exist
        foreach (var thumbConfig in thumbConfigs.Where(x => x.batchGenerate))
        {
            var destFile = new FileInfo(GetThumbPath(source, thumbConfig.size));

            if (!destFile.Directory.Exists)
            {
                _logger.LogDebug("Creating directory: {0}", destFile.Directory.FullName);
                var newDir = Directory.CreateDirectory(destFile.Directory.FullName);
            }

            var needToGenerate = true;

            if (destFile.Exists)
                // We have a thumbnail on disk. See if it's suitable,
                // or if it needs to be regenerated.
                if (!forceRegeneration)
                    // First, check if the source is older than the thumbnail
                    if (source.LastWriteTimeUtc < destFile.LastWriteTimeUtc)
                    {
                        // The source is older, so we might be able to use it. Check the res:
                        int actualHeight, actualWidth;
                        MetaDataService.GetImageSize(destFile.FullName, out actualWidth, out actualHeight);

                        // Note that the size may be smaller - thumbconfigs are 'max' size, not actual.
                        if (actualHeight <= thumbConfig.height && actualWidth <= thumbConfig.width)
                        {
                            // Size matches - so no need to generate.
                            needToGenerate = false;

                            // If the creation time of both files is the same, we're done.
                            _logger.LogDebug("File {0} already exists with matching creation time.", destFile);

                            // Since a smaller version that's suitable as a source exists, use it. This is a
                            // performance enhancement - it means that if we're scaling a 7MB image, but a 1MB
                            // thumbnail already exists, use that as the source instead, as it'll be faster
                            // to process.
                            if (altSource == null && thumbConfig.useAsSource)
                                altSource = destFile;
                        }
                    }

            if (needToGenerate) thumbFileAndConfig.Add(destFile, thumbConfig);
        }

        return thumbFileAndConfig;
    }

    /// <summary>
    ///     Go through all of the thumbnails and delete any thumbs that
    ///     don't apply to a legit iamage.
    /// </summary>
    /// <param name="thumbCleanupFreq"></param>
    public void CleanUpThumbnails(TimeSpan thumbCleanupFreq)
    {
        var root = new DirectoryInfo(PicturesRoot);
        var thumbRoot = new DirectoryInfo(_thumbnailRootFolder);

        CleanUpThumbDir(root, thumbRoot);
    }

    private void CleanUpThumbDir(DirectoryInfo picsFolder, DirectoryInfo thumbsFolder)
    {
        // Check the images here.
        var thumbsToKeep = thumbConfigs.Where(x => x.batchGenerate);
        var picsSubDirs = picsFolder.SafeGetSubDirectories().Select(x => x.Name);
        var thumbSubDirs = thumbsFolder.SafeGetSubDirectories().Select(x => x.Name);

        var foldersToDelete = thumbSubDirs.Except(picsSubDirs);
        var foldersToCheck = thumbSubDirs.Intersect(picsSubDirs);

        foreach (var deleteDir in foldersToDelete) _logger.LogInformation($"Deleting folder {deleteDir} [Dry run]");

        foreach (var folderToCheck in foldersToCheck.Select(x => new DirectoryInfo(x)))
        {
            var allFiles = folderToCheck.GetFiles("*.*");
            var allThumbFiles =
                allFiles.SelectMany(file => thumbsToKeep.Select(thumb => GetThumbPath(file, thumb.size)));

            //var filesToDelete = allFiles;

            // Build hashmap of all base filenames without postfix or extension. Then enumerate
            // thumb files, and any that aren't found, delete
        }
    }

    /// <summary>
    ///     Queries the database to find any images that haven't had a thumbnail
    ///     generated, and queues them up to process the thumb generation.
    /// </summary>
    private async Task ProcessThumbnailScan()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        _logger.LogDebug("Starting thumbnail scan...");

        var complete = false;

        while (!complete)
        {
            _logger.LogDebug("Querying DB for pending thumbs...");

            var watch = new Stopwatch("GetThumbnailQueue");

            // TODO: Change this to a consumer/producer thread model
            var imagesToScan = db.Images.Where(x => x.ThumbLastUpdated == null)
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
                    $"Found {imagesToScan.Count()} images requiring thumb gen. First image is {imagesToScan[0].FullPath}.");

                watch = new Stopwatch("ThumbnailBatch", 100000);

                // We always ignore existing thumbs when generating
                // them based onthe ThumbLastUpdated date.
                const bool forceRegeneration = false;

                _logger .LogDebug($"Executing CreatThumbs in parallel with {s_maxThreads} threads.");

                try
                {
                    await imagesToScan.ExecuteInParallel(async img => await CreateThumbs(img, forceRegeneration),
                        s_maxThreads);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,$"Exception during parallelised thumbnail generation");
                }

                // Write the timestamps for the newly-generated thumbs.
                _logger.LogDebug("Writing thumbnail generation timestamp updates to DB.");

                var updateWatch = new Stopwatch("BulkUpdateThumGenDate");
                db.Images.UpdateRange(imagesToScan.ToList());
                await db.SaveChangesAsync(CancellationToken.None);
                updateWatch.Stop();

                watch.Stop();

                if (imagesToScan.Length > 1)
                    _statusService.UpdateStatus(
                        $"Completed thumbnail generation batch ({imagesToScan.Length} images in {watch.HumanElapsedTime}).");

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
    ///     Generates thumbnails for an image.
    /// </summary>
    /// <param name="sourceImage"></param>
    /// <param name="forceRegeneration"></param>
    /// <returns></returns>
    public async Task<IImageProcessResult> CreateThumbs(Image sourceImage, bool forceRegeneration)
    {
        // Mark the image as done, so that if anything goes wrong it won't go into an infinite loop spiral
        sourceImage.ThumbLastUpdated = DateTime.UtcNow;
        var result = await ConvertFile(sourceImage, forceRegeneration);
        //_imageCache.Evict(sourceImage.Id);
        return result;
    }

    /// <summary>
    ///     Generates thumbnails for an image.
    /// </summary>
    /// <param name="sourceImage"></param>
    /// <param name="forceRegeneration"></param>
    /// <returns></returns>
    public async Task<IImageProcessResult> CreateThumb(int imageId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();
        var image =await db.Images.Where(x=>x.Id==imageId).Include(x=>x.Folder).FirstAsync();
        // Mark the image as done, so that if anything goes wrong it won't go into an infinite loop spiral
        image.ThumbLastUpdated = DateTime.UtcNow;
        var result = await ConvertFile(image, false);
        if (result.ThumbsGenerated)
        {
            await db.Images.Where(x => x.Id == imageId)
                            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ThumbImages, y => result.ThumbImages)
                                                    .SetProperty(y => y.ThumbLastUpdated, y => DateTime.UtcNow)
                                                    .SetProperty(y => y.ProcessThumbStatus,y => 2));
        }
        else
        {
            await db.Images.Where(x => x.Id == imageId)
                            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ThumbLastUpdated, y => DateTime.UtcNow)
                                                      .SetProperty(y => y.ProcessThumbStatus, y => 3));
        }
        
        return result;
    }

    /// <summary>
    ///     Saves an MD5 Image hash against an image.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="processResult"></param>
    /// <returns></returns>
    public Task AddHashToImage(Image image, IImageProcessResult processResult)
    {
        try
        {
            image.Hash = new Hash() { MD5ImageHash = processResult.ImageHash };
            var fullHex = processResult.ImageHash.PadLeft(16, '0');
            var chunks = fullHex.Chunk(4).Select(x => new string(x)).ToArray();
            if (chunks.Length == 4)
            {
                image.Hash.PerceptualHex1 = chunks[0];
                image.Hash.PerceptualHex2 = chunks[1];
                image.Hash.PerceptualHex3 = chunks[2];
                image.Hash.PerceptualHex4 = chunks[3];
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception during perceptual hash calc: {ex}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Clears the cache of face thumbs from the disk
    /// </summary>
    public Task ClearFaceThumbs()
    {
        var dir = new DirectoryInfo(Path.Combine(_thumbnailRootFolder, "_FaceThumbs"));

        dir.GetFiles().ToList()
            .ForEach(x => x.SafeDelete());

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Given an image ID and a face object, returns the path of a generated
    ///     thumbnail for that croppped face.
    /// </summary>
    /// <param name="imageId"></param>
    /// <param name="face"></param>
    /// <returns></returns>
    public async Task<FileInfo> GenerateFaceThumb(Image image)
    {
        FileInfo destFile = null;
        var watch = new Stopwatch("GenerateFaceThumb");

        try
        {
            var faceDir = Path.Combine(_thumbnailRootFolder, "_FaceThumbs");
            var file = new FileInfo(image.FullPath);
            var thumbPath = new FileInfo(GetThumbPath(file, ThumbSize.Large));

            if (thumbPath.Exists && image.MetaData!=null && image.ImageObjects!=null && image.ImageObjects.Any(x => x.Type == ObjectTypes.Face))
            {
                var index = 1;
                foreach (var faceobject in image.ImageObjects.Where(x => x.Type == ObjectTypes.Face))
                {
                    index++;
                    destFile = new FileInfo($"{faceDir}/face_{image.Id}_{index}.jpg");

                    if (!Directory.Exists(faceDir))
                    {
                        _logger.LogInformation($"Created folder for face thumbnails: {faceDir}");
                        Directory.CreateDirectory(faceDir);
                    }

                    if (!destFile.Exists)
                    {
                        _logger.LogInformation($"Generating face thumb for {image.Id}-{index} from file {thumbPath}...");

                        MetaDataService.GetImageSize(image.MetaData, out var thumbWidth, out var thumbHeight);

                        _logger.LogTrace($"Loaded {thumbPath.FullName} - {thumbWidth} x {thumbHeight}");

                        var (x, y, width, height) = ScaleDownRect(image.MetaData.Width, image.MetaData.Height,
                            thumbWidth, thumbHeight,
                            faceobject.RectX, faceobject.RectY, faceobject.RectWidth, faceobject.RectHeight);

                        _logger.LogTrace($"Cropping face at {x}, {y}, w:{width}, h:{height}");

                        // TODO: Update person LastUpdated here?

                        await _imageProcessingService.GetCroppedFile(thumbPath, x, y, width, height, destFile);

                        destFile.Refresh();

                        if (!destFile.Exists)
                            destFile = null;
                    }
                }
            }
            else
            {
                _logger.LogWarning($"Unable to generate face thumb from {thumbPath} - file does not exist.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,$"Exception generating face thumb for image ID {image.Id}");
        }

        watch.Stop();

        return destFile;
    }

    /// <summary>
    ///     Scales the detected face/object rectangles based on the full-sized image,
    ///     since the object detection was done on a smaller thumbnail.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="imgObjects">Collection of objects to scale</param>
    /// <param name="thumbSize"></param>
    public static (int x, int y, int width, int height) ScaleDownRect(int imageWidth, int imageHeight, int sourceWidth,
        int sourceHeight, int x, int y, int width, int height)
    {
        if (sourceHeight == 0 || sourceWidth == 0)
            return (x, y, width, height);

        float longestBmpSide = sourceWidth > sourceHeight ? sourceWidth : sourceHeight;
        float longestImgSide = imageWidth > imageHeight ? imageWidth : imageHeight;

        var ratio = longestBmpSide / longestImgSide;

        var outX = (int)(x * ratio);
        var outY = (int)(y * ratio);
        var outWidth = (int)(width * ratio);
        var outHeight = (int)(height * ratio);

        var percentExpand = 0.3;
        var expandX = outWidth * percentExpand;
        var expandY = outHeight * percentExpand;

        outX = (int)Math.Max(outX - expandX, 0);
        outY = (int)Math.Max(outY - expandY, 0);

        outWidth = (int)(outWidth + expandX * 2);
        outHeight = (int)(outHeight + expandY * 2);

        if (outX + outWidth > sourceWidth)
            outWidth = sourceWidth - outX;

        if (outY + outHeight > sourceHeight)
            outHeight = outHeight - outY;

        return (outX, outY, outWidth, outHeight);
    }

    /// <summary>
    ///     Process the file on disk to create a set of thumbnails.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="forceRegeneration"></param>
    /// <returns></returns>
    public async Task<IImageProcessResult> ConvertFile(Image image, bool forceRegeneration, ThumbSize size = ThumbSize.Unknown)
    {
        var imagePath = new FileInfo(image.FullPath);
        IImageProcessResult result = null;
        try
        {
            if (imagePath.Exists)
            {
                Dictionary<FileInfo, IThumbConfig> destFiles;
                FileInfo altSource = null;

                if (size == ThumbSize.Unknown)
                {
                    // No explicit size passed, so we'll generate any that are flagged as batch-generate.
                    destFiles = GetThumbConfigs(imagePath, forceRegeneration, out altSource);
                }
                else
                {
                    var destFile = new FileInfo(GetThumbPath(imagePath, size));
                    var config = thumbConfigs.Where(x => x.size == size).FirstOrDefault();
                    destFiles = new Dictionary<FileInfo, IThumbConfig> { { destFile, config } };
                }

                if (altSource != null)
                {
                    _logger.LogDebug("File {0} exists - using it as source for smaller thumbs.", altSource.Name);
                    imagePath = altSource;
                }

                // See if there's any conversions to do
                if (destFiles.Any())
                {
                    // First, pre-create the folders for any thumbs we'll be creating
                    destFiles.Select(x => x.Key.DirectoryName)
                        .Distinct().ToList()
                        .ForEach(dir => Directory.CreateDirectory(dir));

                    _logger.LogDebug("Generating thumbnails for {0}", imagePath);

                    var watch = new Stopwatch("ConvertNative", 60000);
                    try
                    {
                        result = await _imageProcessingService.CreateThumbs(imagePath, destFiles);
                        result.ThumbImages = destFiles.Select(x => new ThumbImage() { Name = x.Key.Name, Url = x.Key.FullName, ThumbSize = x.Value.size.ToString()}).ToList();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,"Thumbnail conversion failed for {0}", imagePath);
                    }
                    finally
                    {
                        watch.Stop();
                        _logger.LogDebug(
                            $"{destFiles.Count()} thumbs created for {imagePath} in {watch.HumanElapsedTime}");
                    }

                    if (result!=null && result.ThumbsGenerated)
                    {
                        // Generate the perceptual hash from the large thumbnail.
                        var largeThumbPath = GetThumbPath(imagePath, ThumbSize.Large);
                        var fileName = (new FileInfo(largeThumbPath)).Name;
                        result.ThumbImages.Add(new ThumbImage() { Name = fileName, Url = largeThumbPath, ThumbSize = "l", Types = ObjectTypes.Face });
                        if (File.Exists(largeThumbPath))
                        {
                            result.PerceptualHash = _imageProcessingService.GetPerceptualHash(largeThumbPath);

                            // Store the hash with the image.
                            await AddHashToImage(image, result);
                        }

                       

                    }
                }
                else
                {
                    _logger.LogDebug("Thumbs already exist in all resolutions. Skipping...");
                    result = new ImageProcessResult { ThumbsGenerated = false };
                }
            }
            else
            {
                _logger.LogWarning("Skipping thumb generation for missing file...");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Exception converting thumbnails for {0}", imagePath);
        }

        return result;
    }

    public class ThumbProcess : IProcessJob
    {
        public int ImageId { get; set; }
        public ThumbnailService Service { get; set; }
        public bool CanProcess => true;
        public string Name => "Thumbnail Generation";
        public string Description => $"Thumbnail gen for ID:{ImageId}";
        public JobPriorities Priority => JobPriorities.Thumbnails;

        public async Task Process()
        {
            await Service.CreateThumb(ImageId);
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
