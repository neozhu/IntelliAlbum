

using CleanArchitecture.Blazor.Application.Common.Utils;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Services;

public class FolderService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FolderService> _logger;
    private readonly EventConflator conflator = new(10 * 1000);
    private List<Folder> allFolders = new();
    public FolderService(
        IApplicationDbContext context,
        ILogger<FolderService> logger
        )
    {
        _context = context;
        _logger = logger;
        // Initiate pre-loading the folders.
        _ = LoadFolders();
    }
    public event Action OnChange;
    public Task<ICollection<Folder>> GetFolders()
    {
        ICollection<Folder> result = allFolders;
        return Task.FromResult(result);
    }
    public async Task LoadFolders()
    {
        var watch = new Stopwatch("GetFolders");
        _logger.LogInformation("Loading folder data...");
        try
        {
            allFolders = await _context.Folders
                .Include(x => x.Children)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading folders: {ex.Message}");
        }

        watch.Stop();
        NotifyStateChanged();
    }

    private void OnFoldersChanged()
    {
        conflator.HandleEvent(ConflatedCallback);
    }
    private void ConflatedCallback(object state)
    {
        _ = LoadFolders();
    }
    private void NotifyStateChanged()
    {
        _logger.LogInformation($"Folders changed: {allFolders.Count}");

        OnChange?.Invoke();

        //_ = _notifier.NotifyClients(NotificationType.FoldersChanged);
    }



    private static Folder CreateFolderWrapper(Folder folder, int imageCount, DateTime? maxDate)
    {
        var item = folder.MetaData;

        if (item == null)
        {
            item = new FolderMetadata
            {
                ImageCount = imageCount,
                MaxImageDate = maxDate,
                DisplayName = GetFolderDisplayName(folder)
            };

            folder.MetaData = item;
        }

        ;

        var parent = folder.Parent;

        while (parent != null)
        {
            if (parent.MetaData == null)
                parent.MetaData = new FolderMetadata { DisplayName = GetFolderDisplayName(parent) };

            if (parent.MetaData.MaxImageDate == null || parent.MetaData.MaxImageDate < maxDate)
                parent.MetaData.MaxImageDate = maxDate;

            parent.MetaData.ChildImageCount += imageCount;

            item.Depth++;
            parent = parent.Parent;
        }

        return folder;
    }
    private static string GetFolderDisplayName(Folder folder)
    {
        var display = folder.Name;

        while (display.StartsWith('/') || display.StartsWith('\\'))
            display = display.Substring(1);

        return display;
    }
}

