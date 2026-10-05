using Janitor.Metadata;
using Janitor.Pack2FileSystem;
using Microsoft.Extensions.Logging;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Materials;
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Janitor.AssetHandling;

public class MaterialResourceHandler : IAssetHandler
{
    private const uint LegacyVersion = 0x12;

    private const uint MetadataResourcesVersion = 0x14;

    /// <inheritdoc/>
    public bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".material", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        var material = result.GetData<Material>();
        var manager = context.GetRequiredService<ImageTranslatorService>().Manager;
        var materialDirectory = Path.Combine(context.OutputDirectory, result.Asset.Name);
        var imageFormats = context.Configuration.GetOption("ImageFormats", TextureHandler.DefaultFormats);
        var relativeImages = context.Configuration.GetOption("RelativeImages", false);
        var skipExistingImages = context.Configuration.GetOption("SkipExistingImages", true);

        foreach (var texture in material.EnumerateChildren<Texture>())
        {
            var texturePath = Path.Combine(materialDirectory, relativeImages ? Path.GetFileName(texture.Name) : texture.Name);

            texture.FilePath = TextureHandler.ExportTexture(texture, imageFormats, manager, texturePath, skipExistingImages);
        }
    }

    /// <inheritdoc/>
    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        using var reader = new BinaryReader(file.Open());

        var resourceTable = context.GetRequiredService<ResourceTableService>().Resources;
        var material = new Material(Path.GetFileNameWithoutExtension(asset.Name));
        ILogger logger = context.AssetManager.Logger;

        var version = reader.ReadUInt32();

        if (version != LegacyVersion && version != MetadataResourcesVersion)
            throw new InvalidDataException($"Invalid material resource version: 0x{version:X8}.");

        var settingsCount = reader.ReadInt32();
        for (int i = 0; i < settingsCount; i++)
        {
            // I need a better understanding on how these multiple tables
            // tie in, might be the multiple variants in the mesh data?
            var table = ReadKeyValueStorage(reader);

            // For now - let's just take the first table
            material.Attributes ??= table;
        }

        var textureResourceIds = version == LegacyVersion ? ReadLegacyResources(reader) : ReadResources(reader, file);
        var textureTable = new Texture?[textureResourceIds.Length];
        var textureMap = new Dictionary<ulong, Texture>();

        for (int i = 0; i < textureResourceIds.Length; i++)
        {
            var textureResourceId = textureResourceIds[i];

            if (!textureMap.TryGetValue(textureResourceId, out var texture))
            {
                if (!resourceTable.TryGetValue(textureResourceId, out var textureFile))
                    continue;
                if (textureFile.Data is not Asset textureAsset)
                    continue;

                var textureResult = await context.ReadAsync(textureAsset, cancellationToken);

                texture = textureResult.GetData<Texture>();
                textureMap[textureResourceId] = texture;
                material.AddNode(texture);
            }

            textureTable[i] = texture;
        }

        // Finally bind the materials/textures
        // TODO: Could do with a better way of doing this
        // Could do a refactor of how RedFox handles this
        if (material.Attributes is not null)
        {
            foreach (var (key, value) in material.Attributes)
            {
                if (value is not MaterialTextureSlot slot)
                    continue;

                var slotTexture = textureTable[slot.Index];

                if (slotTexture is null)
                    continue;

                switch (key)
                {
                    case "ColorMap":
                        material.DiffuseMapName = key;
                        material.DiffuseMap = slotTexture;
                        break;
                    case "NormalMap":
                        material.NormalMapName = key;
                        material.NormalMap = slotTexture;
                        break;
                    case "SpecularMap":
                        material.SpecularMapName = key;
                        material.SpecularMap = slotTexture;
                        break;
                    default:
                        material.Connect(key, slotTexture);
                        break;
                }
            }
        }

        // Trailing material descriptor fields.
        var name = ReadString(reader);
        var trailingInt = reader.ReadInt32();
        var trailingUInt = reader.ReadUInt32();
        var flag0 = reader.ReadByte();
        var flag1 = reader.ReadByte();
        var flag2 = reader.ReadByte();
        var flag3 = reader.ReadByte();

        logger.LogDebug("Read material {Material} version 0x{Version:X8} with {TextureCount} texture references", material.Name, version, textureResourceIds.Length);

        return new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = material,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return true;
    }

    /// <summary>
    /// Reads the extensions, sub materials and texture resource IDs stored inline in version 0x12 materials.
    /// </summary>
    private static ulong[] ReadLegacyResources(BinaryReader reader)
    {
        // Need to look into this
        var extensionCount = reader.ReadInt32();
        for (int i = 0; i < extensionCount; i++)
        {
            var extensionType = reader.ReadInt32();
            var shaderResourceId = reader.ReadUInt64();
            var extensionSettings = ReadKeyValueStorage(reader);
        }

        // TODO: Again need to see how these are tied into mesh
        // does the game just draw each of these on top? Notice
        // a face material might have sub-material for pores/detail
        var subMaterialCount = reader.ReadInt32();
        reader.ReadStructArray<ulong>(subMaterialCount);

        var textureCount = reader.ReadInt32();
        return reader.ReadStructArray<ulong>(textureCount).ToArray();
    }

    /// <summary>
    /// Reads the extension settings of version 0x14 materials, whose resource IDs live in rend::MaterialMetadata.
    /// </summary>
    private static ulong[] ReadResources(BinaryReader reader, Pack2File file)
    {
        if (!file.TryParseMetadata<MaterialMetadata>("rend::MaterialMetadata", out var metadata))
            throw new InvalidDataException($"Missing rend::MaterialMetadata on '{file.FullPath}'.");

        foreach (var _ in metadata.ExtensionResourceIDs)
        {
            var extensionType = reader.ReadInt32();
            var extensionSettingsCount = reader.ReadInt32();

            for (int i = 0; i < extensionSettingsCount; i++)
                ReadKeyValueStorage(reader);
        }

        return [.. metadata.TextureResourceIDs];
    }

    private static Dictionary<string, object> ReadKeyValueStorage(BinaryReader reader)
    {
        var entryCount = reader.ReadInt32();
        var table = new Dictionary<string, object>(entryCount);

        for (int i = 0; i < entryCount; i++)
        {
            var key = ReadString(reader);
            var type = reader.ReadInt32();

            object value = type switch
            {
                0  => reader.ReadSingle(),
                1  => new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                2  => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                3  => new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                4  => new MaterialTextureSlot(reader.ReadInt32()),
                5  => reader.ReadInt32(),
                6  => reader.ReadInt64(),
                12 => reader.ReadByte(),
                16 => reader.ReadInt32(),
                17 => (reader.ReadInt32(), reader.ReadInt32()),
                _  => throw new InvalidDataException($"Unsupported KeyValueStorage entry type {type} for key '{key}'."),
            };

            table[key] = value;
        }

        return table;
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length <= 0)
            return string.Empty;

        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }
}
