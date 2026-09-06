using System;
using System.Linq;
using System.Numerics;
using MeowSci.PebblesLib;

internal static class GlbPixelChecks
{
    public static void Run()
    {
        // These known sRGB values distinguish linear-light factor baking from multiplying bytes.
        var original = new GlbPixels(1, 1, [128, 64, 255, 9]);
        var diffuse = GlbPixels.Diffuse(original, [.5f, .25f, 0, .1f]);
        Check(diffuse.Data.SequenceEqual(new byte[] { 92, 30, 0, 255 }), "Diffuse factors multiply linear color and preserve separate opacity semantics");
        Check(original.Data.SequenceEqual(new byte[] { 128, 64, 255, 9 }), "Conversion leaves decoded source pixels unchanged");
        var constant = GlbPixels.Diffuse(null, [.5f, 1, 0, 0]);
        Check(constant.Width == 1 && constant.Height == 1 && constant.Data.SequenceEqual(new byte[] { 188, 255, 0, 255 }), "Untextured factors become encoded constant colors");
        var unit = GlbPixels.Diffuse(original, [1, 1, 1, 1]);
        Check(unit.Data.Take(3).SequenceEqual(original.Data.Take(3)), "Unit factor preserves source RGB");

        // glTF MR channels G/B are combined with a separate AO image and factors.
        var mr = new GlbPixels(1, 1, [17, 200, 100, 0]);
        var ao = new GlbPixels(2, 1, [0, 77, 99, 0, 255, 1, 2, 0]);
        var pbr = GlbPixels.Pbr(mr, ao, .5f, .25f, .5f);
        Check(pbr.Width == 2 && pbr.Height == 1, "PBR output retains larger source dimensions");
        Check(pbr.Data.SequenceEqual(new byte[] { 128, 50, 50, 255, 255, 50, 50, 255 }), "AO strength and MR factors pack into native RGB channels");
        Check(GlbPixels.Pbr(null, null, 0, .5f, 1).Data.SequenceEqual(new byte[] { 255, 128, 0, 255 }), "Missing maps use independent constant factors");
        Check(GlbPixels.Pbr(null, ao, 1, 1, 0).Data[0] == 255, "Zero AO strength removes occlusion");
        var verticalAo = new GlbPixels(1, 2, [0, 0, 0, 255, 255, 0, 0, 255]);
        var horizontalMr = new GlbPixels(2, 1, [0, 40, 80, 255, 0, 120, 160, 255]);
        var tiled = GlbPixels.Pbr(horizontalMr, verticalAo, 1, 1, 1);
        Check(tiled.Width == 2 && tiled.Height == 2 && tiled.Data.SequenceEqual(new byte[]
            { 0, 40, 80, 255, 0, 120, 160, 255, 255, 40, 80, 255, 255, 120, 160, 255 }), "Differently shaped maps resample by UV in both axes");

        var normal = new GlbPixels(1, 1, [200, 128, 230, 0]);
        var zero = GlbPixels.Normal(normal, 0);
        Check(zero.Data.SequenceEqual(new byte[] { 128, 128, 255, 255 }), "Zero normal strength removes tangent displacement");
        var n1 = DecodeNormal(GlbPixels.Normal(normal, 1)); var n2 = DecodeNormal(GlbPixels.Normal(normal, 2));
        Check(Math.Abs(n1.Length() - 1) < .015f && Math.Abs(n2.Length() - 1) < .015f, "Normal scaling renormalizes the encoded vector");
        Check(n2.X > n1.X && n2.Z < n1.Z, "Increased normal scale increases tangent displacement");

        var alpha = new GlbPixels(3, 1, [1, 2, 3, 127, 4, 5, 6, 128, 7, 8, 9, 255]);
        var mask = GlbPixels.Opacity(alpha, 1, .5f);
        Check(mask.Data.SequenceEqual(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255 }), "Alpha masks compare source alpha with threshold and write red coverage");
        Check(GlbPixels.Opacity(alpha, .5f, .5f).Data.SequenceEqual(new byte[]
            { 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255, 255 }), "Alpha factor participates before inclusive cutoff");
        Check(GlbPixels.Opacity(null, .4f, .5f).Data[0] == 0 && GlbPixels.Opacity(null, .5f, .5f).Data[0] == 255,
            "Untextured opacity respects factor and equality at cutoff");
        Console.WriteLine("PASS: GLB pure diffuse/PBR/normal/opacity conversion and source pixel isolation.");
    }

    private static Vector3 DecodeNormal(GlbPixels pixels) => new Vector3(pixels.Data[0], pixels.Data[1], pixels.Data[2]) / 255 * 2 - Vector3.One;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("GLB pixel check failed: " + message);
    }
}
