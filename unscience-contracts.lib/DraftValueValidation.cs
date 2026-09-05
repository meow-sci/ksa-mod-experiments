using System;
using System.Text.Json;
namespace MeowSci.Unscience.Contracts;

/// <summary>Data-only validation shared by explicit feature bindings.</summary>
public static class DraftValueValidation
{
    public static void Range(double value, double min, double max, string field)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new JsonException($"{field} must be between {min} and {max}.");
    }
    public static void Json(JsonElement value)
    {
        int budget = 100000;
        Visit(value, ref budget);
    }
    public static void RequiredShape(JsonElement value, JsonElement defaults)
    {
        if (value.ValueKind == JsonValueKind.Null && defaults.ValueKind != JsonValueKind.Null)
            throw new JsonException("Required authoring data cannot be null.");
        if (value.ValueKind != JsonValueKind.Object || defaults.ValueKind != JsonValueKind.Object) return;
        foreach (var field in defaults.EnumerateObject())
            if (value.TryGetProperty(field.Name, out var child)) RequiredShape(child, field.Value);
    }
    private static void Visit(JsonElement value, ref int budget)
    {
        if (--budget < 0) throw new JsonException("Authoring value is too large.");
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                if (!value.TryGetDouble(out double number) || !double.IsFinite(number) || Math.Abs(number) > 3.4028235e38)
                    throw new JsonException("Authoring number is outside the supported range.");
                break;
            case JsonValueKind.String:
                if (value.GetString()!.Length > 262144) throw new JsonException("Authoring text is too long.");
                break;
            case JsonValueKind.Array:
                if (value.GetArrayLength() > 10000) throw new JsonException("Too many authoring items.");
                foreach (var element in value.EnumerateArray()) Visit(element, ref budget);
                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject()) Visit(property.Value, ref budget);
                break;
        }
    }
}
