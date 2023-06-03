using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities
{
    public  class ImageClassification
    {
        public string Label { get; set; } = null!;
        public double Score { get; set; }

        public override string ToString()
        {
            return $"{Label}";
        }
    }
}
