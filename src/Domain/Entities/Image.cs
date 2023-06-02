

using System.ComponentModel.DataAnnotations.Schema;
using static System.Net.Mime.MediaTypeNames;

namespace CleanArchitecture.Blazor.Domain.Entities
{
    public class Image: BaseAuditableEntity
    {
        public int Id { get; set; }
        public int FolderId { get; set; }
        public virtual Folder Folder { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Comments { get;set; }
        public int FileSizeBytes { get; set; }
        public DateTime FileCreationDate { get; set; }
        public DateTime FileLastModDate { get; set; }

        // Date used for search query orderby
        public DateTime? RecentlyViewDatetime { get; set; }

        public virtual ImageMetaData? MetaData { get; set; }
      
        public virtual Hash Hash { get; set; } = new();
        // An image can have many tags
        public virtual ICollection<Tag>? ImageTags { get; set; }

        public virtual List<ImageClassification>? Classification { get; set; }
        public DateTime? ObjectDetectLastUpdated { get; set; }
        public virtual List<ImageObject>? ImageObjects { get; set; }

        public DateTime? ThumbLastUpdated { get; set; }
        public virtual List<ThumbImage>? ThumbImages { get; set; }
        public DateTime? FaceDetectLastUpdated { get; set; }
        public DateTime? FaceRecognizeLastUpdated { get; set; }
        public virtual List<FaceDetection>? FaceDetections { get; set; }
        public bool HasPerson { get; set; }

        [NotMapped]
        public string FullPath => Path.Combine(Folder.Path, Name);
        [NotMapped]
        public string RawImageUrl => $"/rawimage/{Id}";
        [NotMapped]
        public string DownloadImageUrl => $"/dlimage/{Id}";
        public string ThumbUrl(ThumbSize size)
        {
            return $"/thumb/{size}/{Id}?nocache={this?.FileLastModDate:yyyyMMddHHmmss}";
        }
        public override string ToString()
        {
            return $"{Name} [{Id}]";
        }
        public int ProcessThumbStatus { get; set; }
        public int DetectObjectStatus { get; set; }
        public int DetectFaceStatus { get; set; }
        public int RecognizeFaceStatus { get; set; }


        public string? Keywords { get; set; }
        
    }
}
