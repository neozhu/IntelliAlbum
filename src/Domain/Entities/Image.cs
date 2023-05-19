

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

        public virtual ImageMetaData MetaData { get; set; } = new();
        public virtual Hash Hash { get; set; } = new();
        // An image can have many tags
        public virtual List<Tag> ImageTags { get; init; } = new();

        public virtual List<ImageClassification> Classification { get; init; } = new();
        public virtual List<ImageObject> ImageObjects { get; init; } = new();

        public override string ToString()
        {
            return $"{Name} [{Id}]";
        }
    }
}
