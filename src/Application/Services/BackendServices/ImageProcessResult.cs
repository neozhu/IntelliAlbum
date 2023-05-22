using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class ImageProcessResult : IImageProcessResult
{
    public bool ThumbsGenerated { get; set; }
    public string? ImageHash { get; set; }
    public string? PerceptualHash { get; set; }
}
