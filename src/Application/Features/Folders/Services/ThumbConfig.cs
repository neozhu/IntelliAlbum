using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Services;
public class ThumbConfig : IThumbConfig
{
    public ThumbSize size { get; set; }
    public bool useAsSource { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public bool cropToRatio { get; set; } = false;
    public bool batchGenerate { get; set; } = true;
}
