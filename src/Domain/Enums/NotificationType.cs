using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain;
public enum NotificationType
{
    [Description("Status Changed")]
    StatusChanged ,
    [Description("Folders Changed")]
    FoldersChanged,
    [Description("Work Status Changed")]
    WorkStatusChanged,
    [Description("Cache Evict")]
    CacheEvict,
    [Description("Favourites And Recents Changed")]
    FavouritesAndRecentsChanged,
    [Description("Basket Changed")]
    BasketChanged,
    [Description("System Settings Changed")]
    SystemSettingsChanged
}
