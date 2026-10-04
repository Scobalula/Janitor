# Janitor

Janitor is a desktop asset browser and extractor for Northlight Engine games, including **Alan Wake 2**, **Control**, and **FBC: Firebreak**. Use it to browse game assets, preview supported content, and export selected items or a whole source to files you can use in other tools.

Janitor is designed around the graphical app. You do not need to use the command line for normal browsing and extraction.

## What you can extract

Janitor recognizes common Northlight resources and converts supported assets into useful formats:

- **Models:** `.cast` and `.semodel`
- **Skeletons:** `.cast` and `.semodel`
- **Animations:** `.seanim` and `.cast`
- **Textures:** `.dds` by default; other image formats can be selected in Settings when a suitable image translator is available
- **Wwise audio:** `.wav` by default, with the option to keep audio in its original `.wem` form
- **Other files:** raw files are available as a fallback, preserving their original data and extension

Exact support varies between games and asset versions. A file may be extractable as raw data even when Janitor cannot preview or convert it.

## Get started

1. Download and unpack a Janitor release, then launch `Janitor.UI.exe`.
2. Select **Load Files** and choose one or more `.rmdtoc` files from a supported game.
3. Keep the game’s related package files in their original locations. A TOC can refer to data in companion files, so moving or renaming only the TOC may prevent Janitor from finding its assets.
4. Browse the asset list. Search by name, filter by type or source, or change the sort order.
5. Select an asset and choose **Preview Selected**, or double-click an item. Preview support depends on the asset type.
6. Choose **Export Selected** for the current selection, or **Export All** to export everything from the loaded sources. **Export All includes filtered-out assets too**; use **Export Selected** when you want to export only particular search results.
7. When export finishes, choose **Open Export Folder** in the progress window.

Use **Manage Sources** to review or unload mounted files. **Clear All** unloads every source currently in the app.

## Settings

Open **Settings** from the left sidebar. Settings are saved for future Janitor sessions.

### Export folder

Choose the folder where extracted files should be written. The initial location is Janitor’s default export folder for your user account.

### Models and animations

- **Model formats:** `.cast` and `.semodel` by default.
- **Skeleton formats:** `.cast` and `.semodel` by default. Skeleton-only files export their bone hierarchy and bind pose without requiring a mesh.
- **Skip skeletons that already exist:** on by default. Janitor can still create a missing format if another selected skeleton format already exists.
- **Animation formats:** `.seanim` and `.cast` by default.
- **Export images with models:** on by default, so a model’s referenced textures are exported with it.
- **Export model images to the model’s folder:** off by default; turn it on to keep a model’s images beside the model export.
- **Read all LODs / variants:** on by default. Turn these off when you only want a smaller export of each model.
- **Skip existing models:** on by default. Remove old files before exporting again if you want to regenerate them.

### Images

- **Image formats:** `.dds` by default.
- **Skip existing images:** on by default.
- **Export material images to a flat folder:** gathers a material’s textures together rather than keeping their original relative paths.

### Audio

- **Convert Wwise audio:** on by default; recognized audio is converted to the selected format.
- **Audio formats:** `.wav` by default.
- **Skip existing audio:** on by default.

Turn off **Convert Wwise audio** to keep the original `.wem` files. Audio that Janitor cannot decode is kept in its original form.

### Raw files

Enable **Read and export raw assets** to export files as stored in the game packages instead of converting recognized resources. This also changes what the previewer reads. Raw export is useful when a resource is unsupported or when you need the original file for another tool.

### Preview

The Preview settings adjust scene orientation, model fitting, lighting, and audio playback. They affect how content is displayed in Janitor, not the source files.

## What the previews show

Depending on the selected asset, Janitor can show a 3D scene, image, audio player, table, or hexadecimal view. Standalone skeletons open in the 3D preview; use the **Bones** toggle to show or hide their hierarchy. Some resources have no specialized preview; that does not necessarily mean they cannot be exported. Try raw export for files that Janitor does not recognize.

## Current limitations

- Janitor loads Northlight `.rmdtoc` sources for the supported games.
- Keep companion game package files available in the locations referenced by each TOC.
- Material and audio conversion can vary with the game version and resource data. Unsupported resources are best retained as raw files.

## Command-line option

Janitor also includes `Janitor.CLI` for command-line workflows. Its `names` command can build bone and audio name tables from mounted sources. For everyday browsing, previewing, and exporting, use `Janitor.UI`.

## Need help?

Open **Settings → Open Log Folder** to find Janitor’s logs. When reporting a problem, include the affected game, the source `.rmdtoc`, the asset name, what you expected, and any relevant log details.

For project updates and issue reporting, visit the [Janitor GitHub repository](https://github.com/Scobalula/Janitor).
