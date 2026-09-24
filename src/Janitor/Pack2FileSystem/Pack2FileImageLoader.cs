using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.Graphics3D;
using System;
using System.Collections.Generic;
using System.Text;
using RedFox.Imaging.Formats.Dds;

namespace Janitor.Pack2FileSystem;

/// <summary>
/// Handles loading textures from <see cref="Pack2File"/>.
/// </summary>
public sealed class Pack2FileImageLoader : ITextureLoader
{
    private readonly DdsImageTranslator _translator = new();

    /// <summary>
    /// Gets the shared <see cref="Pack2File"/> image loader instance.
    /// </summary>
    public static Pack2FileImageLoader Shared { get; } = new();

    /// <inheritdoc/>
    public Image Load(Texture texture, ImageTranslatorManager translatorManager)
    {
        if (texture.UserData is not Pack2File file)
            throw new NotSupportedException("Only Pack2File textures are supported for Pack2FileImageLoader.");

        // These are basically DDS files
        using var stream = file.Open();
        return _translator.Read(stream);
    }
}
