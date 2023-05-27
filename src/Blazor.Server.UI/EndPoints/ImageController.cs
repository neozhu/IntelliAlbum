using Blazor.Server.UI.Pages.Authentication;
using CleanArchitecture.Blazor.Application.BackendServices;
using CleanArchitecture.Blazor.Application.Common.Utils;
using CleanArchitecture.Blazor.Application.Services.BackendServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Blazor.Server.UI.EndPoints;
[Microsoft.AspNetCore.Mvc.Route("images")]
[ApiController]
public class ImageController : Controller
{
    private ILogger<ImageController> _logger;

    public ImageController(ILogger<ImageController> logger)
    {
        _logger = logger;
    }

    [Produces("image/jpeg")]
    [HttpGet("/dlimage/{imageId}")]
    public async Task<IActionResult> Image(string imageId, CancellationToken cancel,
        [FromServices] ImageCache imageCache)
    {
        return await Image(imageId, cancel, imageCache, true);
    }

    [Produces("image/jpeg")]
    [HttpGet("/rawimage/{imageId}")]
    public async Task<IActionResult> Image(string imageId, CancellationToken cancel,
        [FromServices] ImageCache imageCache, bool isDownload = false)
    {
        var watch = new Stopwatch("ControllerGetImage");

        IActionResult result = Redirect("/no-image.png");

        if (int.TryParse(imageId, out var id))
            try
            {
                var image = await imageCache.GetCachedImage(id);

                if (cancel.IsCancellationRequested)
                    return result;

                if (image != null)
                {
                    string downloadFilename = null;

                    if (isDownload)
                        downloadFilename = image.Name;

                    if (cancel.IsCancellationRequested)
                        return result;

                    result = PhysicalFile(image.FullPath, "image/jpeg", downloadFilename);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"No thumb available for /rawmage/{imageId}: ");
            }

        watch.Stop();

        return result;
    }

    [Produces("image/jpeg")]
    [HttpGet("/thumb/{thumbSize}/{imageId}")]
    public async Task<IActionResult> Thumb(string thumbSize, string imageId, CancellationToken cancel,
        [FromServices] ImageCache imageCache, [FromServices] ThumbnailService thumbService)
    {
        var watch = new Stopwatch("ControllerGetThumb");

        IActionResult result = Redirect("/no-image.png");

        if (Enum.TryParse<ThumbSize>(thumbSize, true, out var size) && int.TryParse(imageId, out var id))
            try
            {
                _logger.LogTrace($"Controller - Getting Thumb for {imageId}");

                var image = await imageCache.GetCachedImage(id);

                if (cancel.IsCancellationRequested)
                    return result;

                if (image != null)
                {
                    if (cancel.IsCancellationRequested)
                        return result;

                    _logger.LogTrace($" - Getting thumb path for {imageId}");

                    var file = new FileInfo(image.FullPath);
                    var imagePath = thumbService.GetThumbPath(file, size);
                    var gotThumb = true;


                    if (!System.IO.File.Exists(imagePath))
                    {
                        gotThumb = false;
                        _logger.LogTrace($" - Generating thumbnail on-demand for {image.Name}...");

                        if (cancel.IsCancellationRequested)
                            return result;

                        var conversionResult = await thumbService.ConvertFile(image, false, size);

                        if (conversionResult.ThumbsGenerated)
                            gotThumb = true;
                    }

                    if (cancel.IsCancellationRequested)
                        return result;

                    if (gotThumb)
                    {
                        _logger.LogTrace($" - Loading file for {imageId}");

                        result = PhysicalFile(imagePath, "image/jpeg");
                    }

                    _logger.LogTrace($"Controller - served thumb for {imageId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Unable to process /thumb/{thumbSize}/{imageId}");
            }
        watch.Stop();
        return result;
    }

    
 
}
