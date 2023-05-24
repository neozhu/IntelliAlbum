using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities
{
    public class ImageObject
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public ObjectTypes Type { get; set; } = ObjectTypes.Object;
        public Tag? Tag { get; set; } 
        public double Score { get; set; }
        public int RectX { get; set; }
        public int RectY { get; set; }
        public int RectWidth { get; set; }
        public int RectHeight { get; set; }

        public ImageAvatar? Avatar { get; set; }
    }
}
