using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;

namespace CleanArchitecture.Blazor.Application.Services.BackendServices;
public interface IImageCacheService
{
    Task<Image> GetCachedImage(int imgId);
    Task<List<Image>> GetCachedImages(ICollection<int> imgIds);
    Task ClearCache();
}
