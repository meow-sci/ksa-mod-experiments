using System.Reflection;

namespace MeowSci.KsaAbstractions;

/// <summary>Reflection utilities for accessing private/internal KSA fields.</summary>
public static class ReflectionHelpers
{
    private static readonly BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Gets the value of a field by name from the given object, or null if not found.</summary>
    public static object? GetFieldValue(object? obj, string fieldName)
    {
        if (obj == null) return null;
        var field = obj.GetType().GetField(fieldName, All);
        return field?.GetValue(obj);
    }

    /// <summary>Sets the value of a field by name on the given object.</summary>
    public static void SetFieldValue(object? obj, string fieldName, object? value)
    {
        if (obj == null) return;
        var field = obj.GetType().GetField(fieldName, All);
        field?.SetValue(obj, value);
    }

    /// <summary>Gets the value of a field by name from the given object, cast to T, or null if not found or wrong type.</summary>
    public static T? GetFieldValue<T>(object? obj, string fieldName) where T : class
    {
        return GetFieldValue(obj, fieldName) as T;
    }
}
