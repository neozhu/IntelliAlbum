﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities;

public class FaceDetection
{
    public string? ThumbUrl { get; set; }
    public string? Name { get; set; }
    public float Probability { get; set; }
    public float Similarity { get; set; }
    public int RectX { get; set; }
    public int RectY { get; set; }
    public int RectWidth { get; set; }
    public int RectHeight { get; set; }
    public string? FileName { get; set; }
    //public float[]? Embedding { get; set; }

    public override string ToString()
    {
        return $"{Name}";
    }
}

