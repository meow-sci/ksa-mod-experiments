using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Brutal.Numerics;

namespace MeowSci.KsaAbstractions;

public static class DraftJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { IncludeFields = true, MaxDepth = 48 };
        options.Converters.Add(new Components<float3>(v => new[] { (double)v.X, v.Y, v.Z }, a => new((float)a[0], (float)a[1], (float)a[2]), 3));
        options.Converters.Add(new Components<float4>(v => new[] { (double)v.X, v.Y, v.Z, v.W }, a => new((float)a[0], (float)a[1], (float)a[2], (float)a[3]), 4));
        options.Converters.Add(new Components<float2>(v => new[] { (double)v.X, v.Y }, a => new((float)a[0], (float)a[1]), 2));
        options.Converters.Add(new Components<double3>(v => new[] { v.X, v.Y, v.Z }, a => new(a[0], a[1], a[2]), 3));
        return options;
    }
    public static JsonElement Encode<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
    public static T Decode<T>(JsonElement data) => data.Deserialize<T>(Options)!;
    public static T Clone<T>(T value) => Decode<T>(Encode(value));

    private sealed class Components<T>(Func<T, double[]> read, Func<double[], T> create, int count) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var values = JsonSerializer.Deserialize<double[]>(ref reader, options);
            if (values == null || values.Length != count || Array.Exists(values, v => !double.IsFinite(v) || Math.Abs(v) > float.MaxValue))
                throw new JsonException("Invalid vector components.");
            return create(values);
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, read(value), options);
    }
}
