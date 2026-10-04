# Janitor

Janitor is an asset extractor for Northlight Engine Games. It provides a CLI and UI for exporting various assets including Animations, Models, Sounds, and more.

## Supported Games and Content

Janitor recognizes common Northlight resources and converts the following:

| Asset Type | Quantum Break | Control | Alan Wake 2 | FBC: Firebreak | CONTROL Resonant | Formats / Notes |
|---|:---:|:---:|:---:|:---:|:---:|---|
| Models | ❌ | ❌ | ✅ | ✅ | ✅ | `.semodel`, `.cast`, `.fbx`, `.psk`, `.gltf`, `.ma`, `.md5`, `.obj`, `.smd`, `.xmodel_export`, `.xmodel_bin` |
| Animations | ❌ | ❌ | ✅ | ✅ | ✅ | `.seanim`, `.cast` |
| Textures | ❌ | ❌ | ✅ | ✅ | ✅ | `.dds`, `.jpg`, `.png`, `.tga`, `.bmp`, `.tiff` |
| Materials | ❌ | ❌ | 🟡 | 🟡 | 🟡 | Exports textures grouped by material |
| Audio | ❌ | ❌ | ✅ | ✅ | ✅ | `.wav`, `.flac` |

> **Note:** Additional formats supported by the backend may also work, but only the formats listed above are currently confirmed.

Janitor can also export any game file as raw bytes. Unsupported asset types fall back to raw export by default.

## Getting Started

Download the latest release ZIP, extract it, and run either:

- `Janitor.UI.exe` for the graphical interface
- `Janitor.CLI.exe` for the command-line interface

## Using the UI

Launch `Janitor.UI.exe` and select **Load Files** to load one or more Northlight `.rmdtoc` files.

Once loaded, Janitor displays the discovered assets in the main asset list. Each entry shows its **name**, **type**, and additional **information** where available.

The main actions are:

- **Load Files** — Load one or more `.rmdtoc` sources.
- **Preview Selected** — Preview supported asset types. Unsupported assets can still be inspected as raw bytes.
- **Export All** — Export all assets currently included by the active filters.
- **Export Selected** — Export the currently selected asset or assets.
- **Double-click an asset** — Quickly export that asset.
- **Manage Sources** — View loaded sources and unload individual sources.
- **Clear All** — Remove all loaded assets and unload all sources.
- **Settings** — Open Janitor's configuration options.
- **About** — Display application and version information.

The asset list can be filtered by **asset type** and **source**, and sorted by **name**, **information**, or **type**.

The search bar supports wildcard matching, making it easy to narrow large asset lists to specific names or patterns. For example: `*character*.binfbx`

The status bar at the bottom of the window displays the current asset count and the latest operation status.

<!-- UI screenshot can be placed here -->

## Using the CLI

Launch `Janitor.CLI.exe` to enter Janitor's interactive command-line interface.

Commands such as `/mount` and `/help` can be used to manage sources and discover available functionality.

<!-- CLI screenshot can be placed here -->

## Disclaimer

Janitor is an unofficial community tool and is not affiliated with, endorsed by, or supported by Remedy Entertainment.

Janitor does not distribute game assets or game files. Users are responsible for ensuring that their use of extracted content complies with applicable licenses, terms of service, and copyright laws.

Game names, trademarks, and other intellectual property belong to their respective owners.

## Credits

Janitor is built on top of the RedFox libraries for asset processing, format conversion, and related functionality.

Janitor uses (indirectly or directly from RedFox) the following libraries:

