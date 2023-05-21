﻿using CleanArchitecture.Blazor.Application.BackendServices;
using CleanArchitecture.Blazor.Application.Common.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;

namespace CleanArchitecture.Blazor.Application.Services.BackendServices;
// <summary>
///     This is the thing that drives the performance. The bottleneck for performance
///     is hitting the DB to pull back image metadata, etc., because there's a lot
///     of joins, particularly when it comes to ImageTags (which is many-to-many,
///     and for a large collection can have several million rows). So we cache, hard.
///     This is basically a read-through cache - whenever we pull images back from
///     the DB, we get their IDs (which makes the query fast) and then call
///     EnricheAndCache, whose job it is to separate the IDs for which we already
///     have cached data, and those which we don't, and load only the ones from the
///     DB that are new.
///     The key point is that this is a system-wide cache, so if multiple users are
///     using the system at once, most of their access will be from in-memory cached
///     image data, not from the DB. This massively improves performance, and reduces
///     DB concurrency, which SQLite isn't very good at.
///     The key thing to remember is that:
///     1. We have to call db.Attach before updating any of the EF Core objects
///     in the cache, because they won't have a valid DB context and
///     2. We need to be careful to explicitly evict images when things change,
///     such as re-indexing, keywords being added, thumbnails regenerated, etc.
/// </summary>
public class ImageCache : IImageCacheService
{
    private readonly MemoryCacheEntryOptions _cacheOptions;
    private readonly ILogger<ImageCache> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ServerNotifierService _notifier;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImageCache(
        ILogger<ImageCache> logger,
        IMemoryCache memoryCache, IServiceScopeFactory scopeFactory, ServerNotifierService notifier)
    {
        
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _logger = logger;
        _memoryCache = memoryCache;
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromDays(2));
    }

    /// <summary>
    ///     Get an enriched image back from the cache for a particular ID
    /// </summary>
    /// <param name="imgId"></param>
    /// <returns></returns>
    public async Task<Image> GetCachedImage(int imgId)
    {
        Image cachedImage;

        var ids = new List<int> { imgId };
        var cachedImages = await EnrichAndCache(ids);

        cachedImage = cachedImages.FirstOrDefault();

        return cachedImage;
    }

    /// <summary>
    ///     For a given list of IDs, load them into the cache, and then return.
    ///     Note, it's critical that the results are returned in the same order
    ///     as the IDs passed in, so we iterate once to find the ones not in
    ///     the cache, then cache them, then iterate again to pull them all out
    ///     of the cache, in order.
    /// </summary>
    /// <param name="imgIds"></param>
    /// <returns></returns>
    public async Task<List<Image>> GetCachedImages(ICollection<int> imgIds)
    {
        var result = new List<Image>();

        try
        {
            // First, get the list that aren't in the cache
            var needLoad = imgIds.Where(x => !_memoryCache.TryGetValue(x, out var _))
                .ToList();

            // Now load and cache them
            if (needLoad.Any())
                await EnrichAndCache(needLoad);

            // Now, re-enumerate the list, but in-order. Note that everything
            // should be in the cache this time
            foreach (var imgId in imgIds)
            {
                Image image;
                if (!_memoryCache.TryGetValue(imgId, out image))
                {
                    // Somehow an item which we just supposedly cached, is no
                    // longer in the cache. This is very bad indeed.
                    _logger.LogWarning($"Cached image {imgId} was not found in cache. Attempting reload");
                    continue;
                }

                result.Add(image);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,$"Exception during caching/enrichment.");
        }

        return result;
    }

    /// <summary>
    ///     Prime the cache with a load of most recent images
    /// </summary>
    /// <returns></returns>
    public async Task WarmUp()
    {
        try
        {
            const int warmupCount = 2000;

            _logger.LogInformation($"Warming up image cache with up to {warmupCount} most recent images.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

            var warmupIds = await db.Images.OrderByDescending(x => x.RecentlyViewDatetime)
                .Take(warmupCount)
                .Select(x => x.Id)
                .ToListAsync();

            await EnrichAndCache(warmupIds);

            _logger.LogInformation($"Image Cache primed with {warmupIds.Count} images.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error warming up cache: {ex.Message}");
        }
    }

    public async Task<Image> GetCachedImage(Image img)
    {
        Image cachedImage;


        if (!_memoryCache.TryGetValue(img.Id, out cachedImage))
        {
            _logger.LogInformation($"Cache miss for image {img.Id}");
            cachedImage = await EnrichAndCache(img);
        }

        return cachedImage;
    }

    /* Can we use a compiled query here?
    private static Func<ImageContext, List<int>, Task<List<Image>>> getImagesAsync =
            EF.CompileAsyncQuery((ImageContext context, List<int> imageIds) =>
                context.Images
                    .Where(x => imageIds.Contains(x.ImageId))
                    .Include(x => x.Folder)
                    .Include(x => x.MetaData)
                    .Include(x => x.Hash)
                    .Include(x => x.MetaData.Camera)
                    .Include(x => x.MetaData.Lens)
                    .Include(x => x.BasketEntries)
                    .Include(x => x.ImageTags.Where(y => imageIds.Contains(y.ImageId)))
                    .ThenInclude(x => x.Tag)
                    .Include(x => x.ImageObjects.Where(y => imageIds.Contains(y.ImageId)))
                    .ThenInclude(x => x.Tag)
                    .Include(x => x.ImageObjects.Where(y => imageIds.Contains(y.ImageId)))
                    .ThenInclude(x => x.Person)
                    .ToList());
    */


    private async Task<List<Image>> EnrichAndCache(List<int> imageIds)
    {
        if (!imageIds.Any())
            return new List<Image>();

        var tagwatch = new Stopwatch("EnrichCache");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        // This is THE query. It has to be fast. The ImageTags many-to-many
        // join is *really* slow on a standard EFCore query, so we have to
        // filter using a list of ImageIDs.
        // TODO: We can use AsNoTracking here and it might be faster. However,
        // when we do that, the updates in ScanMetaData don't work. Do we need
        // to do something clever, such as setting the object modified?
        // https://stackoverflow.com/questions/6969760/entity-framework-update-problem
        var images = await db.Images
            .Where(x => imageIds.Contains(x.Id))
            .Include(x => x.Folder)
            .Include(x => x.MetaData)
            .Include(x => x.Hash)
            .Include(x => x.MetaData.Camera)
            .Include(x => x.MetaData.Lens)
            .Include(x => x.ImageTags)
            .Include(x => x.ImageObjects)
            .ToListAsync();

        foreach (var enrichedImage in images) _memoryCache.Set(enrichedImage.Id, enrichedImage, _cacheOptions);

        tagwatch.Stop();

        if (images.Count() > 1)
            _logger.LogInformation($"Enriched and cached {images.Count()} in {tagwatch.ElapsedTime}ms");

        return images;
    }

    private async Task<Image> EnrichAndCache(Image image)
    {
        var enrichedImage = await GetImage(image);

        if (enrichedImage != null) _memoryCache.Set(enrichedImage.Id, enrichedImage, _cacheOptions);

        return enrichedImage;
    }

    /// <summary>
    ///     Get a single image and its metadata, ready to be cached.
    /// </summary>
    /// <param name="imageId"></param>
    /// <returns></returns>
    private async Task<Image> GetImage(Image image)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<IApplicationDbContext>();

        var watch = new Stopwatch("EnrichForCache");
        var loadtype = "unknown";

        try
        {
            // TODO: Use AsNoTracking here, for speed?

            // We're either passed an existing image, or an image id.
            if (image != null)
            {
                //loadtype = "object";
                ////var entry = db.Attach(image);

                //if (!entry.Reference(x => x.Folder).IsLoaded)
                //    await entry.Reference(x => x.Folder)
                //        .LoadAsync();

                //if (!entry.Reference(x => x.MetaData).IsLoaded)
                //    await entry.Reference(x => x.MetaData)
                //        .Query()
                //        .Include(x => x.Camera)
                //        .Include(x => x.Lens)
                //        .LoadAsync();

                //if (!entry.Reference(x => x.Hash).IsLoaded)
                //    await entry.Reference(x => x.Hash)
                //        .LoadAsync();

                //if (!entry.Collection(x => x.BasketEntries).IsLoaded)
                //    await entry.Collection(x => x.BasketEntries).LoadAsync();
            }

            if (image != null)
            {
                /// Because of this issue: https://github.com/dotnet/efcore/issues/19418
                /// we have to explicitly load the tags, rather than using eager loading.

                //if (!db.Entry(image).Collection(e => e.ImageTags).IsLoaded)
                //    // Now load the tags
                //    await db.Entry(image).Collection(e => e.ImageTags)
                //        .Query()
                //        .Include(e => e.Tag)
                //        .LoadAsync();

                //if (!db.Entry(image).Collection(e => e.ImageObjects).IsLoaded)
                //    await db.Entry(image).Collection(e => e.ImageObjects)
                //        .Query()
                //        .Include(x => x.Tag)
                //        .Include(x => x.Person)
                //        .LoadAsync();
            }
            else
            {
                throw new ArgumentException("Logic error.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,$"Exception retrieving image.");
        }
        finally
        {
            watch.Stop();
            _logger.LogInformation($"Cache enrich from {loadtype} took {watch.ElapsedTime}ms");
        }


        return image;
    }

    /// <summary>
    ///     Remove an item from the cache so it'll be reloaded from the DB.
    /// </summary>
    /// <param name="imageId"></param>
    public void Evict(int imageId)
    {
        _logger.LogInformation($"Evicting from cache: {imageId}");
        _memoryCache.Remove(imageId);

        _ = _notifier.NotifyClients<string>(NotificationType.CacheEvict, imageId.ToString());
    }

    /// <summary>
    ///     Remote a set of images from the cache
    /// </summary>
    /// <param name="imageId"></param>
    public void Evict(List<int> imageId)
    {
        imageId.ForEach(x => Evict(x));
    }

    public Task ClearCache()
    {
        var memCache = _memoryCache as MemoryCache;
        if (memCache is not null)
        {
            // Force the cache to compact 100% of the memory
            memCache.Compact(1.0);
        }

        return Task.CompletedTask;
    }
}
