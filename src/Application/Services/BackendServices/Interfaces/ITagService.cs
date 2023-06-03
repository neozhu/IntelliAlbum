using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public interface ITagService
{
    event Action OnFavouritesChanged;
    event Action<ICollection<string>> OnUserTagsAdded;

    Task<ICollection<Tag>> GetFavouriteTags();
    Task<bool> ToggleFavourite(Tag tag);

    Task UpdateTagsAsync(ICollection<int> imageIds, ICollection<string> tagsToAdd, ICollection<string> tagsToDelete,
        int? userId = null);

    //Task SetExifFieldAsync(ICollection<int> imageIds, ExifOperation.ExifType exifType, string newValue,
    //    int? userId = null);
}

public interface IRecentTagService
{
    Task<ICollection<string>> GetRecentTags();
}

public interface ITagSearchService
{
    Task<ICollection<Tag>> SearchTags(string filterText);
    Task<ICollection<Tag>> GetAllTags();
    Task<Tag> GetTag(int tagId);
}
