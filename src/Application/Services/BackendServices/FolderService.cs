

using CleanArchitecture.Blazor.Application.Common.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Blazor.Application.BackendServices;
/// <summary>
///     Service to load all of the folders monitored by Damselfly, and present
///     them as a single collection to the UI.
/// </summary>
public class FolderService : IFolderService
{
    private readonly IndexingService _indexingService;
    private readonly ServerNotifierService _notifier;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FolderService> _logger;
    private readonly EventConflator conflator = new(10 * 1000);
    private List<Folder> allFolders = new();
    public FolderService(
        IndexingService indexingService, 
        ServerNotifierService notifier,
        IServiceScopeFactory scopeFactory,
        ILogger<FolderService> logger
        )
    {
        _indexingService = indexingService;
        _notifier = notifier;
        _scopeFactory = scopeFactory;
        _logger = logger;
        // After we've loaded the data, start listening
        _indexingService.OnFoldersChanged += OnFoldersChanged;
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();
        var watch = new Stopwatch("GetFolders");
        _logger.LogInformation("Loading folder data...");
        try
        {
            allFolders = await db.Folders
                .Include(x => x.Children)
                .Select(x => CreateFolderWrapper(x, x.Images.Count, x.Images.Max(i => i.RecentlyViewDatetime)))
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

        _ = _notifier.NotifyClients(NotificationType.FoldersChanged);
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

