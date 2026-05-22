---
name: harmony
description: 'Expert in use of Harmony Lib for runtime patching of dotnet C# code'
license: MIT
---


# Overview

Use HarmoneyLib effectively by following best practices for the library

# Harmony Library Reference (AI-Optimized)

> HarmonyLib runtime patching library for .NET/Mono. Patches methods without modifying DLLs on disk.

## Core Concepts

- **Patches coexist**: Multiple Harmony patches on the same method do not conflict
- **Patch methods MUST be static**: Instance state stored in static variables
- **Unique ID required**: Use reverse domain notation (e.g., `com.example.mymod`)

## Setup

```csharp
using HarmonyLib;

// Create instance
var harmony = new Harmony("com.example.mymod");

// Apply all annotated patches in assembly
harmony.PatchAll();

// Or manual patching
harmony.Patch(originalMethod, 
    prefix: new HarmonyMethod(typeof(MyPatch).GetMethod("Prefix")),
    postfix: new HarmonyMethod(typeof(MyPatch).GetMethod("Postfix")));
```

## Patch Types

### 1. Prefix
Runs **before** original. Can skip original and modify arguments.

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class MyPatch
{
    // Return false to skip original (and subsequent prefixes with side effects)
    // Return true or void to continue
    static bool Prefix(ref int someArg, ref int __result)
    {
        someArg = 10;        // Modify argument (needs ref)
        __result = 42;       // Set return value (needs ref)
        return false;        // Skip original
    }
}
```

### 2. Postfix
Runs **after** original. Always runs (unless exception thrown). Preferred for compatibility.

```csharp
static void Postfix(ref int __result, int someArg)
{
    __result *= 2;  // Modify return value (needs ref)
}

// Pass-through postfix (for IEnumerable or special cases)
static IEnumerable<T> Postfix(IEnumerable<T> __result)
{
    foreach (var item in __result)
        yield return ModifyItem(item);
}
```

### 3. Finalizer
Wraps original + all patches in try/catch. Handles/suppresses exceptions.

```csharp
// Suppress all exceptions
static Exception Finalizer() => null;

// Observe exception
static void Finalizer(Exception __exception)
{
    if (__exception != null) Log(__exception);
}

// Replace exception
static Exception Finalizer(Exception __exception)
{
    return __exception != null ? new CustomException(__exception) : null;
}
```

### 4. Transpiler
Modifies IL instructions at patch time (not runtime). Advanced use only.

```csharp
static IEnumerable<CodeInstruction> Transpiler(
    IEnumerable<CodeInstruction> instructions,
    ILGenerator generator,      // Optional: for labels/locals
    MethodBase original)        // Optional: original method info
{
    var codes = new List<CodeInstruction>(instructions);
    // Modify codes list
    return codes;
}
```

**CodeMatcher** utility for transpilers:
```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    return new CodeMatcher(instructions)
        .MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Foo), "Bar")))
        .SetOperandAndAdvance(AccessTools.Method(typeof(MyClass), "MyReplacement"))
        .InstructionEnumeration();
}
```

### 5. Reverse Patch
Copies original method into your stub. Call private/protected methods directly.

```csharp
[HarmonyPatch(typeof(TargetClass), "PrivateMethod")]
class MyPatch
{
    // Stub signature must match original (instance methods need instance as first param)
    [HarmonyReversePatch]
    static int CallPrivateMethod(TargetClass instance, int arg)
    {
        // Stub body replaced by original's IL
        throw new NotImplementedException("Stub");
    }
}
// Usage: int result = MyPatch.CallPrivateMethod(targetInstance, 5);
```

Types: `HarmonyReversePatchType.Original` (default, unpatched) or `Snapshot` (with existing transpilers)

## Injected Arguments

| Name | Type | Description |
|------|------|-------------|
| `__instance` | class type | `this` for instance methods |
| `__result` | return type | Return value (use `ref` to modify) |
| `__state` | any | Share state between Prefix/Postfix (same class only) |
| `___fieldName` | field type | Private field access (3 underscores, use `ref` to modify) |
| `__args` | `object[]` | All arguments array |
| `__originalMethod` | `MethodBase` | Original method info (cannot call it) |
| `__runOriginal` | `bool` | Whether original will/did run |
| `someArg` | matches original | By name (use `ref` to modify) |
| `__0`, `__1`, etc. | matches original | By index |

## Annotations

### Target Specification

```csharp
// Basic - type + method name
[HarmonyPatch(typeof(MyClass), "MyMethod")]

// With argument types (for overloads)
[HarmonyPatch(typeof(MyClass), "MyMethod", new Type[] { typeof(int), typeof(string) })]

// Property getter/setter
[HarmonyPatch(typeof(MyClass), "PropertyName", MethodType.Getter)]
[HarmonyPatch(typeof(MyClass), "PropertyName", MethodType.Setter)]

// Constructor
[HarmonyPatch(typeof(MyClass), MethodType.Constructor)]
[HarmonyPatch(typeof(MyClass), MethodType.Constructor, new Type[] { typeof(int) })]

// Generics - must patch specific closed type
[HarmonyPatch(typeof(MyClass<string>), "Method")]

// Split across multiple attributes
[HarmonyPatch(typeof(MyClass))]
[HarmonyPatch("MyMethod")]
[HarmonyPatch(new Type[] { typeof(int) })]
```

### Patch Method Attributes

```csharp
[HarmonyPrefix]      // or name method "Prefix"
[HarmonyPostfix]     // or name method "Postfix"  
[HarmonyTranspiler]  // or name method "Transpiler"
[HarmonyFinalizer]   // or name method "Finalizer"
```

### Priority & Ordering

```csharp
[HarmonyPriority(Priority.High)]     // Run earlier (higher = earlier)
[HarmonyPriority(Priority.Low)]      // Run later
[HarmonyBefore("other.mod.id")]      // Run before specific mod
[HarmonyAfter("other.mod.id")]       // Run after specific mod
```

Priority values: `First=0`, `VeryHigh=100`, `High=200`, `Higher=300`, `Normal=400`, `Lower=500`, `Low=600`, `VeryLow=700`, `Last=800`

## Auxiliary Methods

```csharp
[HarmonyPatch(typeof(MyClass), "MyMethod")]
class MyPatch
{
    // Called before patching; return false to skip this patch class
    static bool Prepare(MethodBase original, Harmony harmony)
    {
        return original != null; // Example: only patch if method exists
    }
    
    // Dynamic target selection (replaces [HarmonyPatch] target)
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(SomeClass), "SomeMethod");
    }
    
    // Multiple targets
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(A), "Method");
        yield return AccessTools.Method(typeof(B), "Method");
    }
    
    // Called after patching; can handle/suppress exceptions
    static Exception Cleanup(MethodBase original, Exception ex)
    {
        return null; // Suppress exception
    }
}
```

## Utilities

### AccessTools
Reflection helper. All methods ignore visibility (public/private/etc).

```csharp
AccessTools.Method(typeof(MyClass), "MethodName")
AccessTools.Method(typeof(MyClass), "MethodName", new[] { typeof(int) })
AccessTools.Field(typeof(MyClass), "fieldName")
AccessTools.Property(typeof(MyClass), "PropertyName")
AccessTools.Constructor(typeof(MyClass), new[] { typeof(int) })
AccessTools.Inner(typeof(MyClass), "NestedClassName")
AccessTools.TypeByName("Namespace.ClassName")
```

### Traverse
Fluent reflection with null-safety.

```csharp
// Read private field
var value = Traverse.Create(instance).Field("_privateField").GetValue<int>();

// Set private field
Traverse.Create(instance).Field("_privateField").SetValue(42);

// Call private method
Traverse.Create(instance).Method("PrivateMethod", arg1, arg2).GetValue();

// Chain access
var deep = Traverse.Create(obj).Field("a").Property("B").Field("c").GetValue();
```

## Execution Order

1. **Prefixes** (highest priority first)
   - Void/no-ref prefixes always run (side-effect free)
   - First `return false` skips remaining prefixes with side effects AND original
2. **Original** (possibly transpiled)
3. **Postfixes** (lowest priority first) - always run unless exception
4. **Finalizers** (lowest priority first) - always run, handle exceptions

## Edge Cases & Limitations

| Issue | Cause | Workaround |
|-------|-------|------------|
| Patch not called | Method inlined by JIT | Patch caller instead, or use `[MethodImpl(MethodImplOptions.NoInlining)]` if you control the code |
| Generics shared | Reference types share implementation | Check `__instance.GetType()` in patch; value types usually not shared |
| Can't patch static constructor | Runs before patching | Time patching carefully, or accept it runs at wrong time |
| Can't patch native/extern | No IL to modify | Transpiler-only patch returning new implementation (loses ability to call original) |
| MissingMethodException (Unity) | Patching before Unity initialization | Delay patching until after scene load |
| base.Method() not working | Resolved at compile time | Use Reverse Patch |
| InvalidProgramException | Method has no RET instruction | Use Transpiler to fix IL |

## Debugging

```csharp
// Enable debug logging (writes to Desktop/harmony.log.txt)
Harmony.DEBUG = true;

// Or per-patch
[HarmonyDebug]
[HarmonyPatch(...)]
class MyPatch { }

// Environment variables
// HARMONY_NO_LOG=1        - Disable logging
// HARMONY_LOG_FILE=path   - Custom log path
```

## Common Patterns

### Skip Original Conditionally
```csharp
static bool Prefix(SomeType __instance)
{
    if (__instance.ShouldSkip)
    {
        return false; // Skip original
    }
    return true; // Run original
}
```

### Wrap Original in Try/Catch
```csharp
static Exception Finalizer(Exception __exception)
{
    if (__exception != null)
        Logger.Error($"Caught: {__exception}");
    return null; // Suppress
}
```

### Replace Method Call in Original
```csharp
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    return new CodeMatcher(instructions)
        .MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Original), "OldMethod")))
        .SetOperandAndAdvance(AccessTools.Method(typeof(MyClass), "NewMethod"))
        .InstructionEnumeration();
}
```

### Access Private Nested Type
```csharp
static MethodBase TargetMethod()
{
    var nestedType = AccessTools.Inner(typeof(OuterClass), "PrivateNestedClass");
    return AccessTools.Method(nestedType, "TargetMethod");
}
```

### Patch All Overloads
```csharp
static IEnumerable<MethodBase> TargetMethods()
{
    return typeof(MyClass)
        .GetMethods(AccessTools.all)
        .Where(m => m.Name == "OverloadedMethod");
}
```
