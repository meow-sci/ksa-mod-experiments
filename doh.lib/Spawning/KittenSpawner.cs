using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal;
using Brutal.Numerics;
using KSA;
using MeowSci.DohLib.Materials;
using MeowSci.KsaAbstractions;

namespace MeowSci.DohLib.Spawning;

/// <summary>
/// Spawns kitten entities (KittenEva) programmatically.
/// Replicates the game's EVADoor.CreateKittenEva() flow with additional features:
///   - Arbitrary positioning (vehicle-relative or absolute orbital)
///   - Batch spawning with offset chains
///   - Optional per-kitten material customization
///
/// MUST be called on the game thread (not from HTTP handlers directly).
/// </summary>
public sealed class KittenSpawner
{
    private readonly MaterialFactory _materialFactory;
    private readonly SpawnedKittenRegistry _registry;
    private int _nextKittenIndex;

    public KittenSpawner(MaterialFactory materialFactory, SpawnedKittenRegistry registry)
    {
        _materialFactory = materialFactory;
        _registry = registry;
    }

    /// <summary>Spawns kitten(s) according to the request parameters.</summary>
    public SpawnResult Spawn(SpawnRequest request)
    {
        try
        {
            return SpawnInternal(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Spawn error: {ex}");
            return SpawnResult.Failure($"Spawn error: {ex.Message}");
        }
    }

    /// <summary>Despawns a kitten by ID.</summary>
    public bool Despawn(string kittenId)
    {
        try
        {
            var entry = _registry.Get(kittenId);
            if (entry == null) return false;

            var system = Universe.CurrentSystem;
            if (system == null) return false;

            // Find the kitten in the system
            Astronomical? astro;
            if (!system.All.TryGet(kittenId, out astro))
                return false;

            if (astro is Vehicle vehicle)
            {
                vehicle.Parent?.Children.Remove(vehicle);
            }

            _registry.Unregister(kittenId);
            Console.WriteLine($"doh: Despawned '{kittenId}'");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Despawn error for '{kittenId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Despawns all kittens spawned by this mod.</summary>
    public void DespawnAll()
    {
        var ids = _registry.KittenIds.ToList();
        foreach (var id in ids)
            Despawn(id);
    }

    /// <summary>Updates the tint color on a previously spawned kitten's materials.</summary>
    public bool RecolorKitten(string kittenId, float4 newColor)
    {
        var entry = _registry.Get(kittenId);
        if (entry?.MaterialSet == null) return false;
        return entry.MaterialSet.UpdateTint(newColor);
    }

    /// <summary>Lists all available character IDs from ModLibrary.</summary>
    public string[] GetAvailableCharacters()
    {
        try
        {
            var characters = GetAllCharacterReferences();
            return characters.Select(c => c.Id).OrderBy(id => id).ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: GetAvailableCharacters error: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    // ---- Internal implementation ----

    private SpawnResult SpawnInternal(SpawnRequest request)
    {
        // Validate
        if (request.Count < 1 || request.Count > 100)
            return SpawnResult.Failure("Count must be between 1 and 100.");

        var system = Universe.CurrentSystem;
        if (system == null)
            return SpawnResult.Failure("No current system.");

        // Resolve positioning
        var pos = ResolvePositioning(request);
        if (pos.Error != null)
            return SpawnResult.Failure(pos.Error);
        if (pos.Parent == null)
            return SpawnResult.Failure("Failed to resolve positioning.");

        // Resolve character
        string characterId = request.CharacterId ?? GetRandomCharacterId();
        if (string.IsNullOrEmpty(characterId))
            return SpawnResult.Failure("No character available.");

        // Spawn loop
        var results = new List<SpawnedKittenInfo>();
        KittenMaterialSet? sharedMatSet = null;

        for (int i = 0; i < request.Count; i++)
        {
            // Generate unique name
            string kittenName = GenerateUniqueName(system);

            // Create backpack part
            Part? backpackPart = CreateBackpackPart();
            if (backpackPart == null)
                return SpawnResult.Failure("Failed to create backpack part (KittenBackPackPart not found).");

            // Calculate this kitten's position offset
            double3 chainOffset = pos.OffsetCci * (i + 1);
            double3 kittenPosCci = pos.BasePositionCci + chainOffset;
            double3 kittenVelCci = pos.VelocityCci;

            // Create KittenEva
            var kittenEva = new KittenEva(
                system,
                characterId,
                pos.Body2Cce,
                pos.BodyRates,
                pos.Parent!,
                kittenName,
                backpackPart,
                pos.ReferenceOrbit!);

            // Create orbit and teleport
            var simTime = Universe.GetElapsedSimTime();
            var orbitColor = pos.ReferenceOrbit?.OrbitLineColor ?? new byte4(255, 200, 0, 255);
            var orbit = Orbit.CreateFromStateCci(
                pos.Parent!, simTime, kittenPosCci, kittenVelCci, orbitColor);
            kittenEva.Teleport(orbit, null, null);

            // Register with parent body
            pos.Parent!.Children.Add(kittenEva);
            kittenEva.UpdatePerFrameData();

            // Apply custom materials
            KittenMaterialSet? matSet = null;
            if (request.TintColor.HasValue || request.PerKittenColors != null)
            {
                float4 color = float4.One;
                if (request.PerKittenColors != null && i < request.PerKittenColors.Length)
                    color = request.PerKittenColors[i];
                else if (request.TintColor.HasValue)
                    color = request.TintColor.Value;

                if (request.UniqueMaterialsPerKitten || i == 0)
                {
                    matSet = _materialFactory.CreateTintedMaterialSet(characterId, color);
                    if (i == 0) sharedMatSet = matSet;
                }
                else
                {
                    matSet = sharedMatSet;
                }

                if (matSet != null)
                    ApplyMaterialSetToKitten(kittenEva, matSet, characterId);
            }

            // Track in registry
            _registry.Register(kittenName, characterId, matSet);

            results.Add(new SpawnedKittenInfo
            {
                KittenId = kittenName,
                CharacterId = characterId,
                MaterialSetId = matSet?.Id,
                TintColor = request.TintColor,
                PositionCci = kittenPosCci,
                VelocityCci = kittenVelCci,
                ParentBodyName = pos.Parent!.Id
            });
        }

        Console.WriteLine($"doh: Spawned {results.Count} kitten(s)");
        return new SpawnResult
        {
            Success = true,
            SpawnedKittens = results.ToArray()
        };
    }

    private PositionResult ResolvePositioning(SpawnRequest request)
    {
        if (!string.IsNullOrEmpty(request.ReferenceVehicleId))
            return ResolveVehicleRelative(request);
        if (request.PositionCci.HasValue && request.VelocityCci.HasValue)
            return ResolveAbsolute(request);

        return new PositionResult { Error = "Either ReferenceVehicleId or PositionCci+VelocityCci required." };
    }

    private PositionResult ResolveVehicleRelative(SpawnRequest request)
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        var refVehicle = vehicles.FirstOrDefault(v => v.Id == request.ReferenceVehicleId);
        if (refVehicle == null)
            return new PositionResult { Error = $"Vehicle '{request.ReferenceVehicleId}' not found." };

        var sv = refVehicle.Orbit.StateVectors;
        var body2Cci = refVehicle.GetAsmb2Cci();
        var offsetCci = request.OffsetBodyFrame.Transform(body2Cci);

        return new PositionResult
        {
            BasePositionCci = sv.PositionCci,
            OffsetCci = offsetCci,
            VelocityCci = sv.VelocityCci,
            Body2Cce = refVehicle.Body2Cce,
            BodyRates = SafeBodyRates(refVehicle.BodyRates),
            Parent = refVehicle.Parent,
            ReferenceOrbit = refVehicle.Orbit
        };
    }

    private PositionResult ResolveAbsolute(SpawnRequest request)
    {
        if (string.IsNullOrEmpty(request.ParentBodyName))
            return new PositionResult { Error = "ParentBodyName required for absolute positioning." };

        var celestials = CelestialProvider.GetAllCelestials();
        var parent = celestials.FirstOrDefault(c => c.Id == request.ParentBodyName);
        if (parent == null)
            return new PositionResult { Error = $"Celestial body '{request.ParentBodyName}' not found." };

        // Create a reference orbit from the absolute position
        var simTime = Universe.GetElapsedSimTime();
        var tempOrbit = Orbit.CreateFromStateCci(
            parent, simTime, request.PositionCci!.Value, request.VelocityCci!.Value, new byte4(255, 200, 0, 255));

        return new PositionResult
        {
            BasePositionCci = request.PositionCci!.Value,
            OffsetCci = double3.Zero,
            VelocityCci = request.VelocityCci!.Value,
            Body2Cce = doubleQuat.Identity,
            BodyRates = double3.Zero,
            Parent = parent,
            ReferenceOrbit = tempOrbit
        };
    }

    private Part? CreateBackpackPart()
    {
        var partTemplate = FindPartTemplate("KittenBackPackPart");
        if (partTemplate == null) return null;

        var part = new Part(partTemplate.Id, partTemplate);
        part.Tree.ReinitializeDerivedValues();

        var mix = SubstanceLibrary.TryGetCombustionProcess(KeyHash.Make("MMH_NTO_1.6".AsSpan()));
        if (mix != null)
        {
            var tanks = part.SubtreeModules.Get<Tank>();
            for (int i = 0; i < tanks.Length; i++)
                tanks[i].ConfigureFor(mix);
        }

        part.Tree.RefillConsumables();
        return part;
    }

    private string GetRandomCharacterId()
    {
        var characters = GetAllCharacterReferences();
        if (characters.Count == 0) return "";
        return characters[Random.Shared.Next(characters.Count)].Id;
    }

    private string GenerateUniqueName(CelestialSystem system)
    {
        string name;
        do
        {
            name = $"Kitten_doh_{_nextKittenIndex++}";
        } while (system.All.TryGet(name, out _));
        return name;
    }

    private static double3 SafeBodyRates(double3 rates)
    {
        if (double.IsNaN(rates.X) || double.IsNaN(rates.Y) || double.IsNaN(rates.Z))
            return double3.Zero;
        return rates;
    }

    /// <summary>Finds a PartTemplate by name via reflection on ModLibrary.AllParts (internal).</summary>
    private static PartTemplate? FindPartTemplate(string partName)
    {
        try
        {
            var field = typeof(ModLibrary).GetField("AllParts",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) return null;

            var collection = field.GetValue(null);
            if (collection == null) return null;

            var findMethod = collection.GetType().GetMethod("Find");
            if (findMethod == null) return null;

            var keyHash = KeyHash.Make(partName.AsSpan());
            return findMethod.Invoke(collection, new object[] { keyHash }) as PartTemplate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: FindPartTemplate error: {ex.Message}");
            return null;
        }
    }

    /// <summary>Gets all CharacterReference entries via reflection on ModLibrary.AllCharacters (internal).</summary>
    private static List<CharacterReference> GetAllCharacterReferences()
    {
        try
        {
            var field = typeof(ModLibrary).GetField("AllCharacters",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) return new List<CharacterReference>();

            var collection = field.GetValue(null);
            if (collection == null) return new List<CharacterReference>();

            var getListMethod = collection.GetType().GetMethod("GetList");
            if (getListMethod == null) return new List<CharacterReference>();

            return getListMethod.Invoke(collection, null) as List<CharacterReference> ?? new List<CharacterReference>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: GetAllCharacterReferences error: {ex.Message}");
            return new List<CharacterReference>();
        }
    }

    /// <summary>
    /// Applies a KittenMaterialSet to a KittenEva by replacing the
    /// MaterialIndices entries on its AnimatedRenderable.
    /// </summary>
    private static void ApplyMaterialSetToKitten(KittenEva kittenEva, KittenMaterialSet matSet, string characterId)
    {
        try
        {
            // Access: kittenEva._renderable → _characterAvatar → Core.CharacterModel → MaterialIndices
            var renderableField = typeof(KittenEva).GetField("_renderable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (renderableField == null)
            {
                Console.WriteLine("doh: _renderable field not found on KittenEva.");
                return;
            }

            var renderable = renderableField.GetValue(kittenEva);
            if (renderable == null) return;

            var avatarField = renderable.GetType().GetField("_characterAvatar",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (avatarField == null)
            {
                Console.WriteLine("doh: _characterAvatar field not found on KittenRenderable.");
                return;
            }

            var avatar = avatarField.GetValue(renderable);
            if (avatar == null) return;

            // Get the Core struct
            var coreField = avatar.GetType().GetField("Core",
                BindingFlags.Public | BindingFlags.Instance);
            if (coreField == null) return;

            var core = coreField.GetValue(avatar);
            if (core == null) return;

            // Get CharacterModel from Core
            var charModelField = core.GetType().GetField("CharacterModel",
                BindingFlags.Public | BindingFlags.Instance);
            if (charModelField == null) return;

            var charModel = charModelField.GetValue(core);
            if (charModel == null) return;

            // Get MaterialIndices array
            var matIndicesField = FindFieldInHierarchy(charModel.GetType(), "MaterialIndices");
            if (matIndicesField == null)
            {
                Console.WriteLine("doh: MaterialIndices field not found on AnimatedRenderable.");
                return;
            }

            var materialIndices = matIndicesField.GetValue(charModel) as int[];
            if (materialIndices == null || materialIndices.Length == 0) return;

            // Get shared material handles for this character to know what to replace
            var sharedBodyHandle = GetSharedMaterialHandle(characterId, "CharacterBodyMaterial");
            var sharedHeadHandle = GetSharedMaterialHandle(characterId, "CharacterHeadMaterial");
            var sharedEyeHandle = GetSharedMaterialHandle(characterId, "CharacterEyeMaterial");

            Console.WriteLine($"doh: MaterialIndices[{materialIndices.Length}] = [{string.Join(", ", materialIndices)}]");
            Console.WriteLine($"doh: Shared handles — body={sharedBodyHandle}, head={sharedHeadHandle}, eye={sharedEyeHandle}");

            // Replace matching handles in MaterialIndices
            int replacements = 0;
            for (int i = 0; i < materialIndices.Length; i++)
            {
                if (sharedBodyHandle >= 0 && materialIndices[i] == sharedBodyHandle)
                {
                    materialIndices[i] = matSet.BodyMaterialHandle;
                    replacements++;
                }
                else if (sharedHeadHandle >= 0 && materialIndices[i] == sharedHeadHandle)
                {
                    materialIndices[i] = matSet.HeadMaterialHandle;
                    replacements++;
                }
                // Eyes are left untinted
            }

            // Apply fur material if available
            if (matSet.FurMaterialHandle >= 0)
                ApplyFurMaterial(avatar, matSet.FurMaterialHandle);

            Console.WriteLine($"doh: Applied material set '{matSet.Id}' to kitten ({replacements} replacements, body={sharedBodyHandle}→{matSet.BodyMaterialHandle}, head={sharedHeadHandle}→{matSet.HeadMaterialHandle})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: ApplyMaterialSetToKitten error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the shared material handle for a character's material name.
    /// Resolves: CharacterReference → CharacterTextures → PbrMaterialReference.Id → MaterialSystem.GetOrLoad
    /// </summary>
    private static int GetSharedMaterialHandle(string characterId, string materialFieldName)
    {
        try
        {
            var charRef = ModLibrary.Get<CharacterReference>(characterId);
            if (charRef?.CharacterTextures == null) return -1;

            var charTextures = charRef.CharacterTextures.Get();
            if (charTextures == null) return -1;

            var matRefField = charTextures.GetType().GetField(materialFieldName,
                BindingFlags.Public | BindingFlags.Instance);
            if (matRefField == null) return -1;

            var matRef = matRefField.GetValue(charTextures);
            if (matRef == null) return -1;

            // Call .Get() to resolve — filter for non-generic overload to avoid AmbiguousMatchException
            var methods = matRef.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var getMethod = Array.Find(methods, m => m.Name == "Get" && !m.IsGenericMethod && m.GetParameters().Length == 0);
            var resolved = getMethod?.Invoke(matRef, null);
            if (resolved == null) return -1;

            // Get .Id
            var idField = resolved.GetType().GetField("Id",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (idField == null)
            {
                var idProp = resolved.GetType().GetProperty("Id",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var idVal = idProp?.GetValue(resolved)?.ToString();
                if (string.IsNullOrEmpty(idVal)) return -1;
                return MaterialSystemAccessor.GetMaterialHandle(idVal);
            }

            var id = idField.GetValue(resolved)?.ToString();
            if (string.IsNullOrEmpty(id)) return -1;
            return MaterialSystemAccessor.GetMaterialHandle(id);
        }
        catch
        {
            return -1;
        }
    }

    private static void ApplyFurMaterial(object avatar, int furMaterialHandle)
    {
        try
        {
            // Access Fur.CatFurRenderable and replace its material reference
            var furField = avatar.GetType().GetField("Fur",
                BindingFlags.Public | BindingFlags.Instance);
            if (furField == null) return;

            var fur = furField.GetValue(avatar);
            if (fur == null) return;

            var catFurField = fur.GetType().GetField("CatFurRenderable",
                BindingFlags.Public | BindingFlags.Instance);
            if (catFurField == null) return;

            var catFurRenderable = catFurField.GetValue(fur);
            if (catFurRenderable == null) return;

            // Look for MaterialHandle or similar field on CatFurRenderable
            var matField = FindFieldInHierarchy(catFurRenderable.GetType(), "MaterialIndex");
            if (matField == null)
                matField = FindFieldInHierarchy(catFurRenderable.GetType(), "_materialHandle");
            if (matField == null)
                matField = FindFieldInHierarchy(catFurRenderable.GetType(), "MaterialIndices");

            if (matField != null && matField.FieldType == typeof(int))
            {
                matField.SetValue(catFurRenderable, furMaterialHandle);
            }
            else if (matField != null && matField.FieldType == typeof(int[]))
            {
                var indices = matField.GetValue(catFurRenderable) as int[];
                if (indices != null)
                {
                    for (int i = 0; i < indices.Length; i++)
                        indices[i] = furMaterialHandle;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: ApplyFurMaterial error: {ex.Message}");
        }
    }

    private static FieldInfo? FindFieldInHierarchy(Type? type, string fieldName)
    {
        while (type != null)
        {
            var field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        return null;
    }

    private class PositionResult
    {
        public double3 BasePositionCci;
        public double3 OffsetCci;
        public double3 VelocityCci;
        public doubleQuat Body2Cce;
        public double3 BodyRates;
        public IParentBody? Parent;
        public Orbit? ReferenceOrbit;
        public string? Error;
    }
}
