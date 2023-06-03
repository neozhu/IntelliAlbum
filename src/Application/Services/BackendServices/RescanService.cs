using CleanArchitecture.Blazor.Application.BackendServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Services.BackendServices;

public class RescanService : IRescanService
{


    private readonly IndexingService _indexingService;
    private readonly MetaDataService _metaDataService;
    private readonly FaceDetectService _faceDetectService;
    private readonly FaceRecognizeService _faceRecognizeService;
    private readonly ThumbnailService _thumbService;

    public RescanService(ThumbnailService thumbService,
        IndexingService indexingService,
        MetaDataService metaDataService, 
        FaceDetectService faceDetectService,
        FaceRecognizeService faceRecognizeService)
    {
        _thumbService = thumbService;
        _indexingService = indexingService;
        _metaDataService = metaDataService;
        _faceDetectService = faceDetectService;
        _faceRecognizeService = faceRecognizeService;
    }

    public async Task MarkAllForRescan(RescanTypes rescanType)
    {
        var providers = GetService(rescanType);

        await Task.WhenAll(providers.Select(x => x.MarkAllForScan()));
    }

    public async Task MarkFolderForRescan(RescanTypes rescanType, int folderId)
    {
        var providers = GetService(rescanType);

        await Task.WhenAll(providers.Select(x => x.MarkFolderForScan(folderId)));
    }

    public async Task MarkImagesForRescan(RescanTypes rescanType, ICollection<int> imageIds)
    {
        var providers = GetService(rescanType);

        await Task.WhenAll(providers.Select(x => x.MarkImagesForScan(imageIds)));
    }

    public async Task ClearFaceThumbs()
    {
        await Task.Run(() => _thumbService.ClearFaceThumbs());
    }

    private ICollection<IRescanProvider> GetService(RescanTypes type)
    {
        var providers = new List<IRescanProvider>();

        if (type.HasFlag(RescanTypes.FaceDetection))
            providers.Add(_faceDetectService);
        if (type.HasFlag(RescanTypes.FaceRecognition))
            providers.Add(_faceRecognizeService);
        if (type.HasFlag(RescanTypes.Thumbnails))
            providers.Add(_thumbService);
        if (type.HasFlag(RescanTypes.Indexing))
            providers.Add(_indexingService);

        if (providers.Count() != BitOperations.PopCount((ulong)type))
            throw new ArgumentException($"Unknown rescan service {type}");

        return providers;
    }
}