using CleanArchitecture.Blazor.Application.Common.Utils;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Photoshop;
using MetadataExtractor.Formats.Xmp;

using Directory = MetadataExtractor.Directory;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;
using ImageProcessingException = MetadataExtractor.ImageProcessingException;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.FileSystem;

namespace CleanArchitecture.Blazor.Application.BackendServices;
public class MetaDataService
{
    private readonly ILogger<MetaDataService> _logger;
    public MetaDataService(ILogger<MetaDataService> logger)
    {
        _logger = logger;
    }
    /// <summary>
    ///     Read the metadata, and handle any exceptions.
    /// </summary>
    /// <param name="imagePath"></param>
    /// <returns>Metadata, or Null if there was an error</returns>
    private IReadOnlyList<Directory> SafeReadImageMetadata(string imagePath)
    {
        var watch = new Stopwatch("ReadMetaData");

        IReadOnlyList<Directory> metadata = null;

        if (File.Exists(imagePath))
            try
            {
                metadata = ImageMetadataReader.ReadMetadata(imagePath);
            }
            catch (ImageProcessingException ex)
            {
                _logger.LogError(ex, "Metadata read for image {0}", imagePath);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "File error reading metadata for {0}", imagePath);
            }

        watch.Stop();

        return metadata;
    }

    /// <summary>
    ///     Pull out the XMP face area so we can convert it to a real face in the DB
    /// </summary>
    /// <param name="xmpDirectory"></param>
    /// <returns></returns>
    private List<ImageObject> ReadXMPFaceRegionData(XmpDirectory xmpDirectory, Image image, string orientation)
    {
        try
        {
            var newFaces = new List<ImageObject>();

            var nvps = xmpDirectory.XmpMeta.Properties
                .Where(x => !string.IsNullOrEmpty(x.Path))
                .ToDictionary(x => x.Path, y => y.Value);

            var iRegion = 0;
            var (flipH, flipV, switchOrient) = FlipHorizVert(orientation);

            while (true)
            {
                iRegion++;

                var regionBase = $"mwg-rs:Regions/mwg-rs:RegionList[{iRegion}]/mwg-rs:";

                // Check if there's a name for the next region. If not, we've probably done them all
                if (!nvps.ContainsKey($"{regionBase}Name"))
                    break;

                var name = nvps[$"{regionBase}Name"];
                var type = nvps[$"{regionBase}Type"];
                var xStr = nvps[$"{regionBase}Area/stArea:x"];
                var yStr = nvps[$"{regionBase}Area/stArea:y"];
                var wStr = nvps[$"{regionBase}Area/stArea:w"];
                var hStr = nvps[$"{regionBase}Area/stArea:h"];

                var x = Convert.ToDouble(xStr);
                var y = Convert.ToDouble(yStr);
                var w = Convert.ToDouble(wStr);
                var h = Convert.ToDouble(hStr);

                if (switchOrient)
                {
                    var xTemp = y;
                    y = x;
                    x = xTemp;
                }

                if (flipH)
                    x = 1 - x;

                if (flipV)
                    y = 1 - y;

                //var newPerson = new Person
                //{
                //    Name = name,
                //    LastUpdated = DateTime.UtcNow,
                //    State = Person.PersonState.Identified
                //};

                var newFace = new ImageObject
                {

                    RectX = (int)((x - w / 2) * image.MetaData.Width),
                    RectY = (int)((y - h / 2) * image.MetaData.Height),
                    RectHeight = (int)(h * image.MetaData.Height),
                    RectWidth = (int)(w * image.MetaData.Width),
                    Type = ObjectTypes.Person,
                    Score = 100,
                    Name = string.Empty,

                };

                newFaces.Add(newFace);
            }

            return newFaces;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception while parsing XMP face/region data");
        }

        return new List<ImageObject>();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="image"></param>
    public ImageMetaData ReadImageMetaData(Image image)
    {
        var keywords = new string[0];
        List<ImageObject> faceobjects = new List<ImageObject>();
        var imgMetaData = new ImageMetaData();
        //imgMetaData.EXIFData = new List<DirectoryBase>();
        try
        {
            var metadata = SafeReadImageMetadata(image.FullPath);

            if (metadata != null)
            {
                //foreach (var directory in metadata)
                //    foreach (var tag in directory.Tags)
                //        imgMetaData.EXIFData.Add(new DirectoryBase() { Name = tag.Name, Description = tag.Description });

                var filetypeDirectory = metadata.OfType<FileTypeDirectory>().FirstOrDefault();
                if (filetypeDirectory != null)
                {
                    imgMetaData.MimeType = filetypeDirectory.SafeExifGetString(FileTypeDirectory.TagDetectedFileMimeType);
                    var filetype= filetypeDirectory.SafeExifGetString(FileTypeDirectory.TagDetectedFileTypeName);
                    imgMetaData.FileType = filetype;
                }
                var filemetaDirectory = metadata.OfType<FileMetadataDirectory>().FirstOrDefault();
                if (filemetaDirectory != null)
                {
                    var filesize = filemetaDirectory.SafeGetExifInt(FileMetadataDirectory.TagFileSize);
                    imgMetaData.FileSize = filesize;
                    imgMetaData.LastUpdated = filemetaDirectory.SafeGetExifDateTime(FileMetadataDirectory.TagFileModifiedDate);
                }
                var subIfdDirectory = metadata.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                if (subIfdDirectory != null)
                {
                    imgMetaData.DateTaken = subIfdDirectory.SafeGetExifDateTime(ExifDirectoryBase.TagDateTimeDigitized);
                   

                    if (imgMetaData.DateTaken == DateTime.MinValue)
                        imgMetaData.DateTaken =
                            subIfdDirectory.SafeGetExifDateTime(ExifDirectoryBase.TagDateTimeOriginal);

                    imgMetaData.Height = subIfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagExifImageHeight);
                    imgMetaData.Width = subIfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagExifImageWidth);

                    if (imgMetaData.Width == 0)
                        imgMetaData.Width = subIfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagImageWidth);
                    if (imgMetaData.Height == 0)
                        imgMetaData.Height = subIfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagImageHeight);

                    imgMetaData.ISO = subIfdDirectory.SafeExifGetString(ExifDirectoryBase.TagIsoEquivalent);
                    imgMetaData.FNum = subIfdDirectory.SafeExifGetString(ExifDirectoryBase.TagFNumber);
                    imgMetaData.Exposure = subIfdDirectory.SafeExifGetString(ExifDirectoryBase.TagExposureTime);
                    imgMetaData.Rating = subIfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagRating);

                    var lensMake = subIfdDirectory.SafeExifGetString(ExifDirectoryBase.TagLensMake);
                    var lensModel = subIfdDirectory.SafeExifGetString("Lens Model");
                    var lensSerial = subIfdDirectory.SafeExifGetString(ExifDirectoryBase.TagLensSerialNumber);

                    // If there was no lens make/model, it may be because it's in the Makernotes. So attempt
                    // to extract it. This code definitely works for a Leica Panasonic lens on a Panasonic body.
                    // It may not work for other things.
                    if (string.IsNullOrEmpty(lensMake) || string.IsNullOrEmpty(lensModel))
                    {
                        var makerNoteDir = metadata.FirstOrDefault(x =>
                            x.Name.Contains("Makernote", StringComparison.OrdinalIgnoreCase));
                        if (makerNoteDir != null)
                            if (string.IsNullOrEmpty(lensModel))
                                lensModel = makerNoteDir.SafeExifGetString("Lens Type");
                    }

                    if (!string.IsNullOrEmpty(lensMake) || !string.IsNullOrEmpty(lensModel))
                    {
                        if (string.IsNullOrEmpty(lensModel) || lensModel == "N/A")
                            lensModel = "Generic " + lensMake;

                        imgMetaData.Lens = new Lens { Make = lensMake, Model = lensModel, Serial = lensSerial };
                    }

                    var flash = subIfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagFlash);

                    imgMetaData.FlashFired = (flash & 0x1) != 0x0;
                }

                var jpegDirectory = metadata.OfType<JpegDirectory>().FirstOrDefault();

                if (jpegDirectory != null)
                {
                    
                    if (imgMetaData.Width == 0)
                        imgMetaData.Width = jpegDirectory.SafeGetExifInt(JpegDirectory.TagImageWidth);
                    if (imgMetaData.Height == 0)
                        imgMetaData.Height = jpegDirectory.SafeGetExifInt(JpegDirectory.TagImageHeight);
                }
                else if (metadata.OfType<PngDirectory>().FirstOrDefault() is not null)
                {
                    var pngDirectory = metadata.OfType<PngDirectory>().FirstOrDefault();
                    if (imgMetaData.Width == 0)
                        imgMetaData.Width = pngDirectory.SafeGetExifInt(PngDirectory.TagImageWidth);
                    if (imgMetaData.Height == 0)
                        imgMetaData.Height = pngDirectory.SafeGetExifInt(JpegDirectory.TagImageHeight);
                }
                else if (metadata.OfType<BmpHeaderDirectory>().FirstOrDefault() is not null)
                {
                    var bmpDirectory = metadata.OfType<BmpHeaderDirectory>().FirstOrDefault();
                    if (imgMetaData.Width == 0)
                        imgMetaData.Width = bmpDirectory.SafeGetExifInt(BmpHeaderDirectory.TagImageWidth);
                    if (imgMetaData.Height == 0)
                        imgMetaData.Height = bmpDirectory.SafeGetExifInt(BmpHeaderDirectory.TagImageHeight);
                }

                var gpsDirectory = metadata.OfType<GpsDirectory>().FirstOrDefault();

                if (gpsDirectory != null)
                {
                    var location = gpsDirectory.GetGeoLocation();

                    if (location != null)
                    {
                        imgMetaData.Longitude = location.Longitude;
                        imgMetaData.Latitude = location.Latitude;
                    }
                }

                var orientation = "1"; // Default
                var IfdDirectory = metadata.OfType<ExifIfd0Directory>().FirstOrDefault();

                if (IfdDirectory != null)
                {
                    var exifDesc = IfdDirectory.SafeExifGetString(ExifDirectoryBase.TagImageDescription).SafeTrim();
                    imgMetaData.Description = exifDesc;

                    imgMetaData.Copyright = IfdDirectory.SafeExifGetString(ExifDirectoryBase.TagCopyright).SafeTrim();

                    orientation = IfdDirectory.SafeExifGetString(ExifDirectoryBase.TagOrientation);
                    var camMake = IfdDirectory.SafeExifGetString(ExifDirectoryBase.TagMake);
                    var camModel = IfdDirectory.SafeExifGetString(ExifDirectoryBase.TagModel);
                    var camSerial = IfdDirectory.SafeExifGetString(ExifDirectoryBase.TagBodySerialNumber);
                    imgMetaData.Rating = IfdDirectory.SafeGetExifInt(ExifDirectoryBase.TagRating);

                    if (!string.IsNullOrEmpty(camMake) || !string.IsNullOrEmpty(camModel))
                        imgMetaData.Camera = new Camera { Make = camMake, Model = camModel, Serial = camSerial };

                    if (NeedToSwitchWidthAndHeight(orientation))
                    {
                        // It's orientated rotated. So switch the height and width
                        var temp = imgMetaData.Width;
                        imgMetaData.Width = imgMetaData.Height;
                        imgMetaData.Height = temp;
                    }
                }

                var IPTCdir = metadata.OfType<IptcDirectory>().FirstOrDefault();

                if (IPTCdir != null)
                {
                    var caption = IPTCdir.SafeExifGetString(IptcDirectory.TagCaption).SafeTrim();
                    var byline = IPTCdir.SafeExifGetString(IptcDirectory.TagByLine).SafeTrim();
                    var source = IPTCdir.SafeExifGetString(IptcDirectory.TagSource).SafeTrim();
                    var category = IPTCdir.SafeExifGetString(IptcDirectory.TagCategory).SafeTrim();
                    var city = IPTCdir.SafeExifGetString(IptcDirectory.TagCity).SafeTrim();
                    imgMetaData.Caption = caption;
                    imgMetaData.Category = category;
                    imgMetaData.City = city;
                    if (!string.IsNullOrEmpty(imgMetaData.Copyright))
                        imgMetaData.Copyright = IPTCdir.SafeExifGetString(IptcDirectory.TagCopyrightNotice).SafeTrim();
                    imgMetaData.Credit = IPTCdir.SafeExifGetString(IptcDirectory.TagCredit).SafeTrim();

                    if (string.IsNullOrEmpty(imgMetaData.Credit) && !string.IsNullOrEmpty(source))
                        imgMetaData.Credit = source;

                    if (!string.IsNullOrEmpty(byline))
                    {
                        if (!string.IsNullOrEmpty(imgMetaData.Credit))
                            imgMetaData.Credit += $" ({byline})";
                        else
                            imgMetaData.Credit += $"{byline}";
                    }

                    // Stash the keywords in the dict, they'll be stored later.
                    var keywordList = IPTCdir?.GetStringArray(IptcDirectory.TagKeywords);
                    if (keywordList != null)
                        keywords = keywordList;
                }
                else if (metadata.OfType<PhotoshopDirectory>().FirstOrDefault() is not null)
                {
                    var psDirectory = metadata.OfType<PhotoshopDirectory>().FirstOrDefault();
                    if (psDirectory != null)
                    {
                        imgMetaData.Caption = psDirectory.SafeExifGetString(PhotoshopDirectory.TagCaption);
                        imgMetaData.Copyright = psDirectory.SafeExifGetString(PhotoshopDirectory.TagCopyright);
                    }
                }

                var xmpDirectory = metadata.OfType<XmpDirectory>().FirstOrDefault();

                if (xmpDirectory != null) faceobjects = ReadXMPFaceRegionData(xmpDirectory, image, orientation);
            }

            if (imgMetaData.Width != 0 && imgMetaData.Height != 0)
                imgMetaData.AspectRatio = imgMetaData.Width / (double)imgMetaData.Height;

            var keywordsSummary = keywords.Any() ? $", found {keywords.Count()} keywords." : string.Empty;
            _logger.LogInformation($"Read metadata for {image.FullPath} (ID: {image.Id}) {keywordsSummary}");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading image metadata for {0}", image.FullPath);
            image.MetaData = null;
        }

        imgMetaData.TagKeywords = string.Join(", ", keywords);
        image.MetaData = imgMetaData;
        image.Keywords = $"{imgMetaData}";  
        return imgMetaData;
    }








    /// <summary>
    ///     Some image editing apps such as Lightroom, On1, etc., do not persist the keyword metadata
    ///     in the images by default. This can mean you keyword-tag them, but those keywords are only
    ///     stored in the sidecars. Damselfly only scans keyword metadata from the EXIF image data
    ///     itself.
    ///     So to rectify this, we can either read the sidecar files for those keywords, and optionally
    ///     write the missing keywords to the Exif Metadata as we index them.
    /// </summary>
    /// <param name="img"></param>
    /// <param name="keywords"></param>
    private List<string> GetSideCarKeywords(Image img, string[] keywords, bool tagsWillBeWritten)
    {
        var watch = new Stopwatch("GetSideCarKeywords");

        var sideCarTags = new List<string>();

        var sidecar = img.GetSideCar();

        if (sidecar != null)
        {
            // We need to be really careful here, to discount unicode-encoding differences, because otherwise
            // we get into an infinite loop where we write one string to the KeywordOperations table, it gets
            // picked up by the ExifService, written to the image using ExifTool - but with slightly different
            // character encoding - and then the next time we come through here and check, the keywords look
            // different. Rinse and repeat. :-s
            var imageKeywords = keywords.Select(x => x.Sanitise());
            var sidecarKeywords = sidecar.GetKeywords().Select(x => x.Sanitise());

            var missingKeywords = sidecarKeywords
                .Except(imageKeywords, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingKeywords.Any())
            {
                var messagePredicate = tagsWillBeWritten ? "" : "not ";
                // Only write this log entry if we're actually going to write sidecar files.
                _logger.LogInformation(
                    $"Image {img.Name} is missing {missingKeywords.Count} keywords present in the {sidecar.Type} sidecar ({sidecar.Filename.Name}). Tags will {messagePredicate}be written to images.");
                sideCarTags = sideCarTags.Union(missingKeywords, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        watch.Stop();

        return sideCarTags;
    }

    /// <summary>
    ///     These are the orientation strings:
    ///     "Top, left side (Horizontal / normal)",
    ///     "Top, right side (Mirror horizontal)",
    ///     "Bottom, right side (Rotate 180)",
    ///     "Bottom, left side (Mirror vertical)",
    ///     "Left side, top (Mirror horizontal and rotate 270 CW)",
    ///     "Right side, top (Rotate 90 CW)",
    ///     "Right side, bottom (Mirror horizontal and rotate 90 CW)",
    ///     "Left side, bottom (Rotate 270 CW)"
    /// </summary>
    /// <param name="orientation"></param>
    /// <returns></returns>
    private bool NeedToSwitchWidthAndHeight(string orientation)
    {
        return orientation switch
        {
            "5" => true,
            "6" => true,
            "7" => true,
            "8" => true,
            "Top, left side (Horizontal / normal)" => false,
            "Top, right side (Mirror horizontal)" => false,
            "Bottom, right side (Rotate 180)" => false,
            "Bottom, left side (Mirror vertical)" => false,
            "Left side, top (Mirror horizontal and rotate 270 CW)" => true,
            "Right side, top (Rotate 90 CW)" => true,
            "Right side, bottom (Mirror horizontal and rotate 90 CW)" => true,
            "Left side, bottom (Rotate 270 CW)" => true,
            _ => false
        };
    }

    /// <summary>
    ///     See NeedToSwitchWidthAndHeight for the states
    ///     1 = 0 degrees: the correct orientation, no adjustment is required.
    ///     2 = 0 degrees, mirrored: image has been flipped back-to-front.
    ///     3 = 180 degrees: image is upside down.
    ///     4 = 180 degrees, mirrored: image has been flipped back-to-front and is upside down.
    ///     5 = 90 degrees: image has been flipped back-to-front and is on its side.
    ///     6 = 90 degrees, mirrored: image is on its side.
    ///     7 = 270 degrees: image has been flipped back-to-front and is on its far side.
    ///     8 = 270 degrees, mirrored: image is on its far side.
    /// </summary>
    /// <param name="orientation"></param>
    /// <returns>
    ///     Three bools: whether the h value should be flipped, whether the w value should be flipped and whether both
    ///     should be swapped
    /// </returns>
    private (bool, bool, bool) FlipHorizVert(string orientation)
    {
        return orientation switch
        {
            "3" => (true, true, false),
            "Top, right side (Mirror horizontal)" => (true, false, false),
            "Bottom, right side (Rotate 180)" => (true, true, false),
            "Bottom, left side (Mirror vertical)" => (false, true, false),
            "6" => (false, false, true),

            // TODO: Guessing these, but worth a shot. :)
            "4" => (false, false, true),
            "8" => (false, false, true),
            _ => (false, false, false)
        };
    }










    public static void GetImageSize(string fullPath, out int width, out int height)
    {
        IReadOnlyList<Directory> metadata;

        width = height = 0;
        metadata = ImageMetadataReader.ReadMetadata(fullPath);

        var jpegDirectory = metadata.OfType<JpegDirectory>().FirstOrDefault();

        if (jpegDirectory != null)
        {
            width = jpegDirectory.SafeGetExifInt(JpegDirectory.TagImageWidth);
            height = jpegDirectory.SafeGetExifInt(JpegDirectory.TagImageHeight);
            if (width == 0 || height == 0)
            {
                var subIfdDirectory = metadata.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                width = jpegDirectory.SafeGetExifInt(ExifDirectoryBase.TagExifImageWidth);
                height = jpegDirectory.SafeGetExifInt(ExifDirectoryBase.TagExifImageHeight);
            }
        }
    }
    public static void GetImageSize(ImageMetaData imageMetaData, out int width, out int height)
    {
        width = imageMetaData.Width;
        height = imageMetaData.Height;
    }

}
