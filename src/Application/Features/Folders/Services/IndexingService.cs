using CleanArchitecture.Blazor.Application.Common.Configurations;
using CleanArchitecture.Blazor.Application.Common.Interfaces;
using CleanArchitecture.Blazor.Application.Common.Utils;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Services;

public class IndexingService : IProcessJobFactory, IRescanProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MetaDataService _metaDataService;
    private readonly ServiceSettings _settings;
    private readonly ILogger<IndexingService> _logger;
    private readonly WorkService _workService;
    private readonly ImageProcessService _imageProcessService;
    private readonly FolderWatcherService _watcherService;
    private readonly IStatusService _statusService;
    private bool _fullIndexComplete;
    public  string RootFolder { get; set; }
    public  bool EnableIndexing { get; set; } 
    public IndexingService(IServiceScopeFactory scopeFactory,
        MetaDataService metaDataService,
         ServiceSettings settings,
        ILogger<IndexingService> logger,
        WorkService workService,
        ImageProcessService imageService,
        FolderWatcherService watcherService,
        IStatusService statusService)
    {

        _scopeFactory = scopeFactory;
        _metaDataService = metaDataService;
        _settings = settings;
        _logger = logger;
        _workService = workService;
        _imageProcessService = imageService;
        _watcherService = watcherService;
        _statusService = statusService;
        RootFolder = _settings.SourceDirectory;
        EnableIndexing = _settings.EnableIndexing;

        // Slight hack to work around circular dependencies
        _watcherService.LinkIndexingServiceInstance(this);
    }
    public JobPriorities Priority => JobPriorities.Indexing;
    public async Task<ICollection<IProcessJob>> GetPendingJobs(int maxCount)
    {
        if (_fullIndexComplete)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

            // Now, see if there's any folders that have a null scan date.
            var folders = await db.Folders.Where(x => x.FolderScanDate == null)
                .OrderBy(x => x.Path)
                .Take(maxCount)
                .ToArrayAsync();

            var jobs = folders.Select(x => new IndexProcess
            {
                Path = new DirectoryInfo(x.Path),
                Service = this,
                Name = "Indexing"
            })
                .ToArray();

            return jobs;
        }
        // We always perform a full index at startup. This checks the
        // state of the folders/images, and also creates the filewatchers

        _fullIndexComplete = true;
        _logger.LogInformation("Performing full index.");

        return new[]
        {
            new IndexProcess
            {
                Path = new DirectoryInfo(RootFolder),
                Service = this,
                Name = "Full Index",
                IsFullIndex = true
            }
        };
    }
    public async Task MarkFolderForScan(int folderId)
    {
        await MarkFoldersForScan(new List<int> { folderId });
    }
    public async Task MarkAllForScan()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

            var updated = await db.Folders.ExecuteUpdateAsync(p => p.SetProperty(x => x.FolderScanDate, x => null));

            _statusService.UpdateStatus($"All {updated} folders flagged for re-indexing.");

            _workService.FlagNewJobs(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception when marking folder for reindexing");
        }
    }
    /// <summary>
    ///     Flags a set of images for reindexing by marking their containing
    ///     folders for rescan.
    /// </summary>
    /// <param name="images"></param>
    /// <returns></returns>
    public async Task MarkImagesForScan(ICollection<int> images)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

            var folders = await db.Images.Where(x => images.Contains(x.Id))
            .Select(x => x.Folder.Id)
            .ToListAsync();

            await MarkFoldersForScan(folders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception when marking images for reindexing");
        }
    }

    public event Action OnFoldersChanged;

    private void NotifyFolderChanged()
    {
        _logger.LogTrace("Folders changed.");

        // TODO - invoke back on dispatcher thread....
        OnFoldersChanged?.Invoke();
    }

    /// <summary>
    ///     Indexes all of the images in a folder, optionally filtering for a last-mod
    ///     threshold and only indexing those images which have changed since that date.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="parent"></param>
    public async Task IndexFolder(DirectoryInfo folder, Folder parent)
    {
        Folder folderToScan = null;
        var foldersChanged = false;

        _logger.LogInformation($"Indexing {folder.FullName}...");

        // Get all the sub-folders on the disk, but filter out
        // ones we're not interested in.
        var subFolders = folder.SafeGetSubDirectories()
            .Where(x => x.IsMonitoredFolder())
            .ToList();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

            // Load the existing folder and its images from the DB
            folderToScan = await db.Folders
                .Where(x => x.Path.Equals(folder.FullName))
                .Include(x => x.Images)
                .FirstOrDefaultAsync();

            if (folderToScan == null)
            {
                _logger.LogInformation("Scanning new folder: {0}\\{1}", folder.Parent.Name, folder.Name);
                folderToScan = new Folder { Path = folder.FullName , Name=Path.GetFileName(folder.FullName) };
            }
            else
            {
                _logger.LogInformation("Scanning existing folder: {0}\\{1} ({2} images in DB)", folder.Parent.Name,
                folder.Name, folderToScan.Images.Count());
            }

            if (folderToScan.Id == 0)
            {
                _logger.LogInformation($"Adding new folder: {folderToScan.Path}");

                if (parent != null)
                    folderToScan.ParentId = parent.Id;

                // New folder, add it. 
                db.Folders.Add(folderToScan);
                await db.SaveChangesAsync(CancellationToken.None);
                foldersChanged = true;
            }

            // Now, check for missing folders, and clean up if appropriate.
            foldersChanged = await RemoveMissingChildDirs(db, folderToScan) || foldersChanged;

            _watcherService.CreateFileWatcher(folder);

            // Now scan the images. If there's changes it could mean the folder
            // should now be included in the folderlist, so flag it.
            await ScanFolderImages(folderToScan.Id);
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"Unexpected exception scanning folder {folderToScan.Name}: {ex.Message}");
            if (ex.InnerException != null)
                _logger.LogInformation(ex,$" Inner exception: {ex.InnerException.Message}");
        }

        // Scan subdirs recursively.
        foreach (var sub in subFolders) await IndexFolder(sub, folderToScan);
    }

    /// <summary>
    ///     Checks the folder, and any recursive children, to ensure it still exists
    ///     on the disk. If it doesn't, removes the child folders from the databas.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="folderToScan"></param>
    /// <returns>True if any folders were updated/changed</returns>
    private async Task<bool> RemoveMissingChildDirs(IApplicationDbContext db, Folder folderToScan)
    {
        var foldersChanged = false;

        try
        {
            // Now query the DB for child folders of our current folder
            var dbChildDirs = db.Folders.Where(x => x.ParentId == folderToScan.Id).ToList();

            foreach (var childFolder in dbChildDirs)
                // Depth-first removal of child folders
                foldersChanged = await RemoveMissingChildDirs(db, childFolder);

            // ...and then look for any DB folders that aren't included in the list of sub-folders.
            // That means they've been removed from the disk, and should be removed from the DB.
            var missingDirs = dbChildDirs.Where(f => !new DirectoryInfo(f.Path).IsMonitoredFolder()).ToList();

            if (missingDirs.Any())
            {
                missingDirs.ForEach(x =>
                {
                    _logger.LogInformation("Deleting folder {0}", x.Path);
                    _watcherService.RemoveFileWatcher(x.Path);
                });

                db.Folders.RemoveRange(missingDirs);

                _logger.LogInformation("Removing {0} deleted folders...", missingDirs.Count());
                // Don't use bulk delete; we want EFCore to remove the linked images
                await db.SaveChangesAsync(CancellationToken.None);
                foldersChanged = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected exception scanning for removed folders {folderToScan.Name}: {ex.Message}");
            if (ex.InnerException != null)
                _logger.LogError($" Inner exception: {ex.InnerException.Message}");
        }

        return foldersChanged;
    }

    /// <summary>
    ///     For a given folder, scans the disk to find all the images in that folder,
    ///     and then indexes all of those images for metadata etc. Optionally takes
    ///     a last-mod threshold which, if set, will mean that only images changed
    ///     since that date will be processed.
    /// </summary>
    /// <param name="folderToScan"></param>
    /// <param name="force">Force the folder to be scanned</param>
    /// <returns></returns>
    private async Task<bool> ScanFolderImages(int folderIdToScan)
    {
        var imagesWereAddedOrRemoved = false;
        var folderImageCount = 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        // Get the folder with the image list from the DB. 
        var dbFolder = await db.Folders.Where(x => x.Id == folderIdToScan)
            .Include(x => x.Images)
            .FirstOrDefaultAsync();

        // Get the list of files from disk
        var folder = new DirectoryInfo(dbFolder.Path);
        var allImageFiles = SafeGetImageFiles(folder);

        if (allImageFiles == null)
            // Null here means we weren't able to read the contents of the directory.
            // So bail, and give up on this folder altogether.
            return false;

        // First, see if images have been added or removed since we last indexed,
        // by comparing the list of known image filenames with what's on disk.
        // If they're different, we disregard the last scan date of the folder and
        // force the update. 
        var fileListIsEqual =
            allImageFiles.Select(x => x.Name).ArePermutations(dbFolder.Images.Select(y => y.Name));


        if (fileListIsEqual && dbFolder.FolderScanDate != null)
            // Number of images is the same, and the folder has a scan date
            // which implies it's been scanned previously, so nothing to do.
            return true;

        _logger.LogInformation($"New or removed images found in folder {dbFolder.Name}.");

        var watch = new Stopwatch("ScanFolderFiles");

        // Select just imagefiles, and most-recent first
        folderImageCount = allImageFiles.Count();

        int newImages = 0, updatedImages = 0;
        foreach (var file in allImageFiles)
            try
            {
                var dbImage = dbFolder.Images.FirstOrDefault(x =>
                    x.Name.Equals(file.Name, StringComparison.OrdinalIgnoreCase));

                if (dbImage != null)
                {
                    // See if the image has changed since we last indexed it
                    var fileChanged = file.FileIsMoreRecentThan(dbImage.FileLastModDate);

                    if (!fileChanged)
                    {
                        // File hasn't changed. Look for a sidecar to see if it's been modified.
                        var sidecar = dbImage.GetSideCar();

                        if (sidecar != null)
                            // If there's a sidecar, see if that's changed.
                            fileChanged = sidecar.Filename.FileIsMoreRecentThan(dbImage.LastModified.Value);
                    }

                    if (!fileChanged)
                    {
                        _logger.LogTrace($"Indexed image {dbImage.Name} unchanged - skipping.");
                        continue;
                    }
                }

                var image = dbImage;

                if (image == null) image = new Image { Name = file.Name };
                // Store some info about the disk file
                image.FileSizeBytes = (int)file.Length;
                image.FileCreationDate = file.CreationTimeUtc;
                image.FileLastModDate = file.LastWriteTimeUtc;

                image.Folder = dbFolder;

                if (dbImage == null)
                {
                    // Default the sort date to file creation date. It'll get updated
                    // later during indexing to set it to the date-taken date, if one
                    // exists.
                    image.RecentlyViewDatetime = image.FileCreationDate.ToUniversalTime();

                    _logger.LogTrace("Adding new image {0}", image.Name);
                    if (!dbFolder.Images.Any(x => x.Name.Equals(image.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        dbFolder.Images.Add(image);
                        newImages++;
                    }
                    imagesWereAddedOrRemoved = true;
                    image.MetaData = _metaDataService.ReadImageMetaData(image);
                }
                else
                {
                    db.Images.Update(image);
                    updatedImages++;

                    // Changed, so throw it out of the cache
                    //_imageCache.Evict(image.ImageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while scanning for new image {file}");
            }

        // Now look for files to remove.
        // TODO - Sanity check that these don't hit the DB
        var filesToRemove = dbFolder.Images.Select(x => x.Name).Except(allImageFiles.Select(x => x.Name));
        var dbImages = dbFolder.Images.Select(x => x.Name);
        var imagesToDelete = dbFolder.Images
            .Where(x => filesToRemove.Contains(x.Name))
            .ToList();

        if (imagesToDelete.Any())
        {
            imagesToDelete.ForEach(x => _logger.LogTrace("Deleting image {0} (ID: {1})", x.Name, x.Id));

            // Removing these will remove the associated ImageTag and selection references.
            db.Images.RemoveRange(imagesToDelete);
            //imagesToDelete.ForEach(x => _imageCache.Evict(x.ImageId));
            imagesWereAddedOrRemoved = true;
        }

        // Flag/update the folder to say we've processed it
        dbFolder.FolderScanDate = DateTime.UtcNow;
        db.Folders.Update(dbFolder);

        await db.SaveChangesAsync(CancellationToken.None);

        watch.Stop();

        _statusService.UpdateStatus(
            $"Indexed folder {dbFolder.Name}: processed {dbFolder.Images.Count()} images ({newImages} new, {updatedImages} updated, {imagesToDelete.Count} removed) in {watch.HumanElapsedTime}.");

        // Do this after we scan for images, because we only load folders if they have images.
        if (imagesWereAddedOrRemoved)
            NotifyFolderChanged();

        if (imagesWereAddedOrRemoved || updatedImages > 0)
        {
            // Should flag the metadata service here...
        }


        return imagesWereAddedOrRemoved;
    }

    /// <summary>
    ///     Marks the FolderScanDate as null, which will cause the
    ///     indexing service to pick it up and scan it for any changes.
    /// </summary>
    /// <param name="folders"></param>
    /// <returns></returns>
    public async Task MarkFoldersForScan(List<int> folderIds)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

            var queryable = db.Folders.Where(f => folderIds.Contains(f.Id));
            await queryable.ExecuteUpdateAsync(p => p.SetProperty(x => x.FolderScanDate, x => null));

            if (folderIds.Count() == 1)
                _statusService.UpdateStatus("Folder flagged for re-indexing.");
            else
                _statusService.UpdateStatus($"{folderIds.Count()} folders flagged for re-indexing.");

            _workService.FlagNewJobs(this);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception when marking folder for reindexing: {ex}");
        }
    }

    /// <summary>
    ///     Get all image files in a subfolder, and return them, ordered by
    ///     the most recently updated first.
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    public List<FileInfo> SafeGetImageFiles(DirectoryInfo folder)
    {
        var watch = new Stopwatch("GetFiles");

        try
        {
            var files = folder.GetFiles()
                .Where(x => _imageProcessService.IsImageFileType(x))
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .ThenByDescending(x => x.CreationTimeUtc)
                .ToList();

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to read files from {0}: {1}", folder.FullName, ex.Message);
            return new List<FileInfo>();
        }
        finally
        {
            watch.Stop();
        }
    }

    public void StartService()
    {
        if (EnableIndexing)
            _workService.AddJobSource(this);
        else
            _logger.LogInformation("Indexing has been disabled.");
    }

    public class IndexProcess : IProcessJob
    {
        public bool IsFullIndex { get; set; }
        public DirectoryInfo Path { get; set; }
        public IndexingService Service { get; set; }
        public bool CanProcess => true;
        public string Name { get; set; }
        public string Description => $"{Name} {Path}";
        public JobPriorities Priority => IsFullIndex ? JobPriorities.FullIndexing : JobPriorities.Indexing;
        private static readonly ILogger Logging = Log.ForContext(typeof(IndexProcess));
        public async Task Process()
        {
            await Service.IndexFolder(Path, null);

            if (IsFullIndex)
                Logging.Information("Full index compelete.");
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
