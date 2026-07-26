// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Classification helpers over <c>AssetBundle.Assets</c> (a <c>List&lt;SerializedId&gt;</c> FIELD).
/// </summary>
/// <remarks>
/// The part-shaped types form one inheritance chain and one sibling branch —
/// <c>SubPartGameDataReference : PartGameDataReference : PartTemplate</c> and
/// <c>SubPartTemplate : PartTemplate</c> — so a bare <c>is PartTemplate</c> matches all four. Every
/// helper here tests the most derived type first. The file types are the same trap:
/// <c>MeshAtlasFileReference</c>, <c>MeshFileReference</c> and <c>TextureReference</c> all derive from
/// <c>FileReference</c>, and <c>TexturePowerReference</c> derives from <c>TextureReference</c>.
/// </remarks>
public static partial class BundleParser
{
    /// <summary>
    /// Top-level <c>&lt;Part&gt;</c> entries: a <see cref="PartTemplate" /> that is neither a
    /// <c>SubPartTemplate</c> nor a <c>PartGameDataReference</c>. These are the parts that appear in
    /// the editor's part browser and that get a thumbnail.
    /// </summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<PartTemplate> TopLevelParts(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        return bundle.Bundle.Assets
            .OfType<PartTemplate>()
            .Where(p => p is not SubPartTemplate && p is not PartGameDataReference);
    }

    /// <summary>
    /// <c>&lt;SubPart&gt;</c> entries. <c>SubPartGameDataReference</c> derives from
    /// <c>PartGameDataReference</c> rather than <c>SubPartTemplate</c>, so it is not included here.
    /// </summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<PartTemplate> SubParts(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<SubPartTemplate>();
    }

    /// <summary>
    /// Every part-shaped template in the bundle: top-level parts, sub-parts and game-data entries.
    /// Useful for rules that walk <c>Components</c> regardless of which flavour declared them.
    /// </summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<PartTemplate> AllPartTemplates(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<PartTemplate>();
    }

    /// <summary>
    /// <c>&lt;PartGameData&gt;</c> and <c>&lt;SubPartGameData&gt;</c> entries
    /// (<c>SubPartGameDataReference</c> derives from <c>PartGameDataReference</c>).
    /// </summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<PartGameDataReference> GameData(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<PartGameDataReference>();
    }

    /// <summary>Top-level <c>&lt;PbrMaterial&gt;</c> entries.</summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<PbrMaterialReference> Materials(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<PbrMaterialReference>();
    }

    /// <summary>
    /// Every file-backed entry: <c>&lt;MeshAtlas&gt;</c>, <c>&lt;MeshFile&gt;</c>,
    /// <c>&lt;Texture&gt;</c>, <c>&lt;Shader&gt;</c>... all derive from <see cref="FileReference" />.
    /// </summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<FileReference> Files(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<FileReference>();
    }

    /// <summary>Top-level <c>&lt;Texture&gt;</c> entries.</summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<TextureReference> Textures(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<TextureReference>();
    }

    /// <summary>Top-level <c>&lt;MeshAtlas&gt;</c> entries.</summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<MeshAtlasFileReference> MeshAtlases(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<MeshAtlasFileReference>();
    }

    /// <summary>Top-level <c>&lt;MeshFile&gt;</c> entries.</summary>
    /// <param name="bundle">The bundle to classify.</param>
    public static IEnumerable<MeshFileReference> MeshFiles(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Bundle.Assets.OfType<MeshFileReference>();
    }

    /// <summary>
    /// Every <see cref="TextureReference" /> the bundle would load from disk: top-level
    /// <c>&lt;Texture&gt;</c> entries, the five channels of every top-level <c>&lt;PbrMaterial&gt;</c>,
    /// and the channels of any material declared inline inside a model component. Entries with an empty
    /// <c>Path</c> are pure references to an existing texture and are skipped.
    /// </summary>
    /// <param name="bundle">The bundle to walk.</param>
    public static IEnumerable<TextureReference> AllTextureFiles(ParsedBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        foreach (TextureReference texture in Textures(bundle))
        {
            if (!string.IsNullOrEmpty(texture.LocalPath))
            {
                yield return texture;
            }
        }

        foreach (PbrMaterialReference material in Materials(bundle))
        {
            foreach (TextureReference channel in MaterialChannels(material))
            {
                if (!string.IsNullOrEmpty(channel.LocalPath))
                {
                    yield return channel;
                }
            }
        }

        foreach (PartTemplate template in AllPartTemplates(bundle))
        {
            foreach (ModelComponent model in ModelComponents(template))
            {
                if (model.Material is null)
                {
                    continue;
                }

                foreach (TextureReference channel in MaterialChannels(model.Material))
                {
                    if (!string.IsNullOrEmpty(channel.LocalPath))
                    {
                        yield return channel;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The non-null texture channels of a material, in KSA's declaration order
    /// (Diffuse, Normal, AoRoughMetal, Emissive, ThinFilm).
    /// </summary>
    /// <param name="material">The material to inspect.</param>
    public static IEnumerable<TextureReference> MaterialChannels(PbrMaterialReference material)
    {
        ArgumentNullException.ThrowIfNull(material);

        if (material.DiffuseReference is not null)
        {
            yield return material.DiffuseReference;
        }

        // NormalReference is a TexturePowerReference, which derives from TextureReference.
        if (material.NormalReference is not null)
        {
            yield return material.NormalReference;
        }

        if (material.PBRMap is not null)
        {
            yield return material.PBRMap;
        }

        if (material.EmissiveMap is not null)
        {
            yield return material.EmissiveMap;
        }

        if (material.ThinFilmMap is not null)
        {
            yield return material.ThinFilmMap;
        }
    }

    /// <summary>
    /// The <c>&lt;PartModel&gt;</c> / <c>&lt;PartModelGlass&gt;</c> / <c>&lt;PartModelDynamic&gt;</c>
    /// components of a template, normalised into a single shape. All three dereference
    /// <c>Material.DiffuseReference</c>, <c>.NormalReference</c> and <c>.PBRMap</c> without a null
    /// check when writing instance data to the GPU, so validation treats them identically.
    /// </summary>
    /// <param name="template">The template whose <c>Components</c> to walk.</param>
    public static IEnumerable<ModelComponent> ModelComponents(PartTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        foreach (ModuleBase.TemplateDataBase component in template.Components)
        {
            switch (component)
            {
                case PartModelModule.Template model:
                    yield return new ModelComponent("PartModel", model.Id, model.Mesh, model.Material);
                    break;
                case PartModelGlassModule.Template glass:
                    yield return new ModelComponent("PartModelGlass", glass.Id, glass.Mesh, glass.Material);
                    break;
                case PartModelDynamicModule.Template dynamicModel:
                    yield return new ModelComponent(
                        "PartModelDynamic", dynamicModel.Id, dynamicModel.Mesh, dynamicModel.Material);
                    break;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="template" /> declares a <c>&lt;MeshView&gt;</c> component, which the
    /// editor needs for picking.
    /// </summary>
    /// <param name="template">The template to inspect.</param>
    public static bool HasMeshView(PartTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Components.OfType<MeshViewModule.Template>().Any();
    }

    /// <summary>
    /// The id a <see cref="FileReference" /> will end up with once <c>OnDataLoad</c> runs: its
    /// declared <c>Id</c>, or — when that is empty — the mod-relative path KSA falls back to
    /// (<c>FileReference.OnDataLoad</c> assigns <c>Id = ModPath</c>).
    /// </summary>
    /// <param name="file">The file reference to identify.</param>
    /// <param name="modDirectory">The absolute mod folder the file's <c>Path</c> resolves against.</param>
    public static string EffectiveFileId(FileReference file, string modDirectory)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!string.IsNullOrEmpty(file.Id))
        {
            return file.Id;
        }

        if (string.IsNullOrEmpty(file.LocalPath) || string.IsNullOrEmpty(modDirectory))
        {
            return string.Empty;
        }

        // Mirrors Mod.GetPath: Path.Combine(DirectoryPath, localPath) with separators corrected.
        // An approximation of KSA's Filepath.CorrectSeparators is deliberate — a mismatch can only
        // make a collision check miss, never fire falsely.
        return Path.Combine(modDirectory, file.LocalPath)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// A <c>&lt;PartModel&gt;</c>-family component reduced to the three things validation cares about.
    /// </summary>
    /// <param name="ElementName">The XML element name, for messages.</param>
    /// <param name="Id">The component's declared id.</param>
    /// <param name="Mesh">The referenced mesh, or null when none was declared.</param>
    /// <param name="Material">The referenced or inline material, or null when none was declared.</param>
    public sealed record ModelComponent(
        string ElementName,
        string Id,
        MeshReference? Mesh,
        PbrMaterialReference? Material);
}
