using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public interface IImageProcessResult
{
    bool ThumbsGenerated { get; set; }
    string? ImageHash { get; set; }
    string? PerceptualHash { get; set; }
}
public enum ThumbSize
{
    Unknown = -1,
    ExtraLarge = 0,
    Large = 1,
    Big = 2,
    Medium = 3,
    Preview = 4,
    Small = 5
}
public interface IThumbConfig
{
    public ThumbSize size { get; }
    public bool useAsSource { get; }
    public int width { get; }
    public int height { get; }
    public bool cropToRatio { get; }
    public bool batchGenerate { get; }
}
