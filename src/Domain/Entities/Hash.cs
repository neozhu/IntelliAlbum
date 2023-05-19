using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Domain.Entities;

public class Hash
{
    // The MD5 image hash. 
    public string MD5ImageHash { get; set; } = null!;

    // Four slices of the perceptual hash (split to allow
    // us to precalculate matches so we only have to calc
    // hamming distance on a subset of images.
    public string PerceptualHex1 { get; set; } = null!;
    public string PerceptualHex2 { get; set; } = null!;
    public string PerceptualHex3 { get; set; } = null!;
    public string PerceptualHex4 { get; set; } = null!;

    private ulong PerceptualHashValue => (ulong)Convert.ToInt64(HexPerceptualHash, 16);

    private string HexPerceptualHash
    {
        get
        {
            try
            {
                return $"{PerceptualHex1}{PerceptualHex2}{PerceptualHex3}{PerceptualHex4}";
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public double SimilarityTo(Hash other)
    {
        if (other == null)
            return 0.0;

        var similarity = Similarity(PerceptualHashValue, other.PerceptualHashValue);

        return similarity;
    }

    public bool HasPerceptualHash()
    {
        return !string.IsNullOrEmpty(PerceptualHex1) &&
               !string.IsNullOrEmpty(PerceptualHex1) &&
               !string.IsNullOrEmpty(PerceptualHex1) &&
               !string.IsNullOrEmpty(PerceptualHex1);
    }

    public void SetFromHexString(string hexHash)
    {
        try
        {
            var fullHex = hexHash.PadLeft(16, '0');

            var chunks = fullHex.Chunk(4).Select(x => new string(x)).ToArray();

            if (chunks.Length == 4)
            {
                PerceptualHex1 = chunks[0];
                PerceptualHex2 = chunks[1];
                PerceptualHex3 = chunks[2];
                PerceptualHex4 = chunks[3];
            }
        }
        catch
        {
            PerceptualHex1 = string.Empty;
            PerceptualHex2 = string.Empty;
            PerceptualHex3 = string.Empty;
            PerceptualHex4 = string.Empty;
        }
    }
    public double Similarity(ulong hash1, ulong hash2)
    {
        return (64 - BitOperations.PopCount(hash1 ^ hash2)) / 64.0;
    }
}

