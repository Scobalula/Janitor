using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.Formats.Dds;
using RedFox.Imaging.IO;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles .tex texture resources stored in Pack2 files, which are DDS images.
/// </summary>
public class TextureResourceHandler : TextureHandler
{
    private readonly DdsImageTranslator _translator = new();

    /// <inheritdoc/>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public override Image Load(Texture texture, ImageTranslatorManager translatorManager)
    {
        if (texture.UserData is not Asset { DataSource: Pack2File file })
            throw new NotSupportedException($"Texture '{texture.Name}' is not backed by a Pack2File asset.");

        using var stream = file.Open();

        return _translator.Read(stream);
    }
}
