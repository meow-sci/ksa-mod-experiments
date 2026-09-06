using System;

namespace MeowSci.PebblesLib;

/// <summary>Detached RGBA8 material conversion; no renderer, decoder or game dependencies.</summary>
internal sealed partial record GlbPixels(int Width, int Height, byte[] Data)
{
    internal static GlbPixels Diffuse(GlbPixels? image, float[] factor)
    {
        var output = Blank(image?.Width ?? 1, image?.Height ?? 1);
        for (int i = 0; i < output.Data.Length; i += 4)
        {
            for (int c = 0; c < 3; c++)
            {
                float source = image == null ? 1 : Linear(image.Data[i + c] / 255f);
                output.Data[i + c] = Byte(Srgb(source * factor[c]));
            }
            output.Data[i + 3] = 255; // Clutter diffuse alpha is terrain tint/gamma, not transparency.
        }
        return output;
    }

    internal static GlbPixels Pbr(GlbPixels? mr, GlbPixels? ao, float metallic, float roughness, float strength)
    {
        var output = Blank(Math.Max(mr?.Width ?? 1, ao?.Width ?? 1), Math.Max(mr?.Height ?? 1, ao?.Height ?? 1));
        for (int y = 0; y < output.Height; y++) for (int x = 0; x < output.Width; x++)
        {
            int i = (y * output.Width + x) * 4;
            output.Data[i] = Byte(1 + (Sample(ao, x, y, output, 0) - 1) * strength);
            output.Data[i + 1] = Byte(Sample(mr, x, y, output, 1) * roughness);
            output.Data[i + 2] = Byte(Sample(mr, x, y, output, 2) * metallic);
            output.Data[i + 3] = 255;
        }
        return output;
    }

    internal static GlbPixels Normal(GlbPixels image, float scale)
    {
        var output = Blank(image.Width, image.Height);
        for (int i = 0; i < output.Data.Length; i += 4)
        {
            float x = (image.Data[i] / 255f * 2 - 1) * scale, y = (image.Data[i + 1] / 255f * 2 - 1) * scale;
            float z = image.Data[i + 2] / 255f * 2 - 1;
            float length = MathF.Sqrt(x * x + y * y + z * z);
            if (length < 1e-12f) { x = y = 0; z = length = 1; }
            output.Data[i] = Byte(x / length * .5f + .5f); output.Data[i + 1] = Byte(y / length * .5f + .5f);
            output.Data[i + 2] = Byte(z / length * .5f + .5f); output.Data[i + 3] = 255;
        }
        return output;
    }

    internal static GlbPixels Opacity(GlbPixels? image, float factor, float cutoff)
    {
        var output = Blank(image?.Width ?? 1, image?.Height ?? 1);
        for (int i = 0; i < output.Data.Length; i += 4)
        {
            float alpha = (image == null ? 1 : image.Data[i + 3] / 255f) * factor;
            byte coverage = alpha >= cutoff ? (byte)255 : (byte)0;
            output.Data[i] = output.Data[i + 1] = output.Data[i + 2] = coverage; output.Data[i + 3] = 255;
        }
        return output;
    }

    private static float Sample(GlbPixels? image, int x, int y, GlbPixels destination, int channel) => image == null ? 1 :
        image.Data[((y * image.Height / destination.Height) * image.Width + x * image.Width / destination.Width) * 4 + channel] / 255f;
    private static GlbPixels Blank(int width, int height) => new(width, height, new byte[checked(width * height * 4)]);
    private static byte Byte(float x) => (byte)Math.Clamp((int)MathF.Round(x * 255), 0, 255);
    private static float Linear(float x) => x <= .04045f ? x / 12.92f : MathF.Pow((x + .055f) / 1.055f, 2.4f);
    private static float Srgb(float x) => x <= .0031308f ? x * 12.92f : 1.055f * MathF.Pow(x, 1 / 2.4f) - .055f;
}
