using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities
{
    public class FolderMetadata
    {
        public string DisplayName { get; set; } = null!;
        public int ImageCount { get; set; }
        public int ChildImageCount { get; set; }
        public DateTime? MaxImageDate { get; set; }
        public int Depth { get; set; } = 1;

        public int TotalImages => ImageCount + ChildImageCount;

        public override string ToString()
        {
            return $"{DisplayName} [{ImageCount} images, Date: {MaxImageDate}]";
        }
    }
}
