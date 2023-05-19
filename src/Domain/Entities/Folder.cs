using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities
{
    public class Folder: BaseAuditableEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Path { get; set; } = null!;
        public int? ParentId { get; set; }
        public virtual Folder? Parent { get; set; }
        public virtual ICollection<Folder>? Children { get; } = new HashSet<Folder>();
        public virtual ICollection<Image>? Images { get; } = new HashSet<Image>();
        public DateTime? FolderScanDate { get; set; }
        public FolderMetadata? MetaData { get; set; }
        public override string ToString()
        {
            return $"{Path} [{Id}] {MetaData?.ToString()}";
        }
        public IEnumerable<Folder> Subfolders
        {
            get
            {
                var thisId = new[] { this };

                if (Children != null)
                    return Children.SelectMany(x => x.Subfolders).Concat(thisId);

                return thisId;
            }
        }
        public IEnumerable<Folder> ParentFolders
        {
            get
            {
                if (Parent != null)
                    return Parent.ParentFolders.Concat(new[] { Parent });

                return Enumerable.Empty<Folder>();
            }
        }
        public bool HasSubFolders => Children != null && Children.Any();
    }
}
