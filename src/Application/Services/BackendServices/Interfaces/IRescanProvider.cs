using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;

[Flags]
public enum RescanTypes
{
    None = 0,
    Indexing = 1,
    Metadata = 2,
    Thumbnails = 4,
    FaceDetection = 5,
    FaceRecognition=6
}
public interface IRescanService
{
    Task MarkFolderForRescan(RescanTypes rescanType, int folderId);
    Task MarkImagesForRescan(RescanTypes rescanType, ICollection<int> imageIds);
    Task MarkAllForRescan(RescanTypes rescanType);

    Task ClearFaceThumbs();
}

public interface IRescanProvider
{
    Task MarkFolderForScan(int folderId);
    Task MarkImagesForScan(ICollection<int> imageIds);
    Task MarkAllForScan();
}
