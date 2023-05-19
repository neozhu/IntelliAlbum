using CleanArchitecture.Blazor.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Services;
public enum ExportType
{
    Download = 1,
    Email = 2,
    Wordpress = 3,
    Facebook = 4,
    Twitter = 5,
    Instagram = 6
}

public enum ExportSize
{
    FullRes = 1,
    Large = 2,
    Medium = 3,
    Small = 4,
    ExtraLarge = 5
}
public interface IExportSettings
{
    ExportType Type { get; set; }
    ExportSize Size { get; set; }

    bool KeepFolders { get; set; }
    string WatermarkText { get; set; }
    int MaxImageSize { get; }
}
