// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Folders.DTOs;
using System.ComponentModel;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;

namespace CleanArchitecture.Blazor.Application.Features.Images.DTOs;

[Description("Images")]
public class ImageDto : IMapFrom<Image>
{
    public void Mapping(Profile profile)
    {
        profile.CreateMap<Image, ImageDto>().ReverseMap();
    }
   
    [Description("Id")]
    public int Id { get; set; }
    [Description("Folder Id")]
    public int FolderId { get; set; }
    [Description("Name")]
    public string Name { get; set; } = String.Empty;
    [Description("Comments")]
    public string? Comments { get; set; }
    [Description("File Size Bytes")]
    public int FileSizeBytes { get; set; }
    [Description("File Creation Date")]
    public DateTime FileCreationDate { get; set; }
    [Description("File Last Mod Date")]
    public DateTime FileLastModDate { get; set; }
    [Description("Recently View Datetime")]
    public DateTime? RecentlyViewDatetime { get; set; }
    [Description("Thumb Last Updated")]
    public DateTime? ThumbLastUpdated { get; set; }
    [Description("Object Detect Last Updated")]
    public DateTime? ObjectDetectLastUpdated { get; set; }
    [Description("Face Detect Last Updated")]
    public DateTime? FaceDetectLastUpdated { get; set; }
    [Description("Face Recognize Last Updated")]
    public DateTime? FaceRecognizeLastUpdated { get; set; }

    [Description("Metadata")]
    public virtual ImageMetaData MetaData { get; set; } = new();
    [Description("Hash")]
    public virtual Hash Hash { get; set; } = new();
    [Description("Tags")]
    // An image can have many tags
    public virtual List<Tag> ImageTags { get; init; } = new();
    [Description("Classification")]
    public virtual List<ImageClassification> Classification { get; init; } = new();
    [Description("Recognized Objects")]
    public virtual List<ImageObject> ImageObjects { get; init; } = new();
    [Description("Thumb Images")]
    public virtual List<ThumbImage>? ThumbImages { get; set; }
    public FolderDto Folder { get; set; } = null!;
    public override string ToString()
    {
        return $"{Name} [{Id}]";
    }
    [Description("Face Detections")]
    public virtual List<FaceDetection>? FaceDetections { get; set; }
    [Description("Process Thumb Status")]
    public int ProcessThumbStatus { get; set; }
    [Description("Detect Object Status")]
    public int DetectObjectStatus { get; set; }
    [Description("Detect Face Status")]
    public int DetectFaceStatus { get; set; }
    [Description("Recognize Face Status")]
    public int RecognizeFaceStatus { get; set; }
}

