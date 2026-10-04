# Third-Party Notices

Janitor is licensed under the **GNU General Public License v3.0** (see [LICENSE](LICENSE)).

Janitor is built on libraries and native binaries authored by Scobalula (the Janitor author) and
by third parties. Each component remains the property of its respective authors and is used under
the terms of its own license. The full text of every license referenced here is included in
[`third_party/licenses/`](third_party/licenses) and is copied into every published release
archive under `licenses/`.

If you redistribute Janitor, you must retain these notices and license texts.

## Scobalula projects

These libraries are authored by Scobalula and are MIT licensed. They are listed separately from
the third-party components below.

| Component | Version | License | License text |
|---|---|---|---|
| RedFox.Audio, RedFox.Audio.ADPCM, RedFox.Audio.Flac, RedFox.Audio.OpenAL, RedFox.Audio.Opus | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.Avalonia.Themes | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.Compression, RedFox.Compression.Deflate, RedFox.Compression.LZ4, RedFox.Compression.ZStandard | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.Cryptography.MurMur3 | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.GameExtraction, RedFox.GameExtraction.CommandLine, RedFox.GameExtraction.Hashing, RedFox.GameExtraction.Mcp, RedFox.GameExtraction.UI | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.Graphics3D, RedFox.Graphics3D.Avalonia, RedFox.Graphics3D.Formats, RedFox.Graphics3D.OpenGL, RedFox.Graphics3D.Rendering, RedFox.Graphics3D.Silk | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.Imaging, RedFox.Imaging.Formats, RedFox.Imaging.Vulkan | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| RedFox.IO, RedFox.IO.ProcessMemory, RedFox.Patterns, RedFox.Plugins, RedFox.Plugins.Python | 2026.10.4 | MIT | [RedFox-LICENSE.txt](third_party/licenses/RedFox-LICENSE.txt) |
| Cast.NET | 2026.9.26.1 | MIT | [Cast.NET-LICENSE.txt](third_party/licenses/Cast.NET-LICENSE.txt) |
| CallOfFile | 2026.9.26.2 | MIT | [CallOfFile-LICENSE.txt](third_party/licenses/CallOfFile-LICENSE.txt) |

## Third-party libraries

### Runtime and UI

| Component | Version | Author | License | License text |
|---|---|---|---|---|
| .NET Runtime | 10.0 | .NET Foundation and Contributors | MIT | [dotnet-runtime-LICENSE.txt](third_party/licenses/dotnet-runtime-LICENSE.txt) |
| Avalonia (Base, Controls, Desktop, Fonts.Inter, Themes.Fluent, Skia, HarfBuzz, Win32, Native, X11, FreeDesktop, Remote.Protocol) | 12.1.3 | AvaloniaUI OÜ | MIT | [Avalonia-LICENSE.txt](third_party/licenses/Avalonia-LICENSE.txt) |
| Avalonia.Controls.DataGrid | 12.1.2 | AvaloniaUI OÜ | MIT | [Avalonia-LICENSE.txt](third_party/licenses/Avalonia-LICENSE.txt) |
| Avalonia.Angle.Windows.Natives (ANGLE) | 2.1.27548.20260419 | The ANGLE Project Authors | BSD 3-Clause | [ANGLE-LICENSE.txt](third_party/licenses/ANGLE-LICENSE.txt) |
| SkiaSharp | 3.119.4 | Microsoft / Xamarin | MIT | [SkiaSharp-HarfBuzzSharp-MIT.txt](third_party/licenses/SkiaSharp-HarfBuzzSharp-MIT.txt) |
| Skia (native, via SkiaSharp) | 3.119.4 | Google Inc. | BSD 3-Clause | [Skia-LICENSE.txt](third_party/licenses/Skia-LICENSE.txt) |
| HarfBuzzSharp | 8.3.1.3 | Microsoft / Xamarin | MIT | [SkiaSharp-HarfBuzzSharp-MIT.txt](third_party/licenses/SkiaSharp-HarfBuzzSharp-MIT.txt) |
| HarfBuzz (native, via HarfBuzzSharp) | 8.3.1.3 | HarfBuzz contributors | Old MIT | [HarfBuzz-LICENSE.txt](third_party/licenses/HarfBuzz-LICENSE.txt) |
| CommunityToolkit.Mvvm | 8.4.2 | .NET Foundation and Contributors | MIT | [CommunityToolkit.Mvvm-LICENSE.txt](third_party/licenses/CommunityToolkit.Mvvm-LICENSE.txt) |
| Silk.NET (Core, Maths, OpenAL, OpenGL, Vulkan, Input, Windowing, GLFW) | 2.23.0 | .NET Foundation and Contributors | MIT | [Silk.NET-LICENSE.txt](third_party/licenses/Silk.NET-LICENSE.txt) |
| Silk.NET.OpenAL.Soft.Native (OpenAL Soft) | 1.23.1 | OpenAL Soft / kcat | LGPL 2.0-or-later | [OpenAL-Soft-LICENSE.txt](third_party/licenses/OpenAL-Soft-LICENSE.txt) |
| GLFW (via Ultz.Native.GLFW / Silk.NET.GLFW) | 3.4 | Marcus Geelnard, Camilla Löwy | Zlib | [GLFW-LICENSE.txt](third_party/licenses/GLFW-LICENSE.txt) |
| Tmds.DBus.Protocol | 0.94.1 | Tom Deseyn and contributors | MIT | [Tmds.DBus.Protocol-LICENSE.txt](third_party/licenses/Tmds.DBus.Protocol-LICENSE.txt) |
| MicroCom.Runtime | 0.11.6 | Nikita Tsukanov | MIT | [MicroCom-LICENSE.txt](third_party/licenses/MicroCom-LICENSE.txt) |
| pythonnet (Python.Runtime) | 3.1.0 | Python.NET contributors | MIT | [Python.NET-LICENSE.txt](third_party/licenses/Python.NET-LICENSE.txt) |

### CLI

| Component | Version | Author | License | License text |
|---|---|---|---|---|
| Spectre.Console and Spectre.Console.Ansi | 0.57.2 | Patrik Svensson, Phil Scott, Nils Andresen, Cédric Luthi | MIT | [Spectre.Console-LICENSE.txt](third_party/licenses/Spectre.Console-LICENSE.txt) |
| PrettyPrompt | 6.0.5 | Will Fuqua | MPL 2.0 | [PrettyPrompt-LICENSE.txt](third_party/licenses/PrettyPrompt-LICENSE.txt) |
| TextCopy | 6.2.1 | CopyText contributors | MIT | [TextCopy-LICENSE.txt](third_party/licenses/TextCopy-LICENSE.txt) |
| Model Context Protocol C# SDK (ModelContextProtocol, ModelContextProtocol.Core) | 2.2.0 | Model Context Protocol / LF Projects, LLC | Apache 2.0 | [ModelContextProtocol-LICENSE.txt](third_party/licenses/ModelContextProtocol-LICENSE.txt) |
| K4os.Compression.LZ4 | 1.3.8 | Milosz Krajewski | MIT | [K4os.Compression.LZ4-LICENSE.txt](third_party/licenses/K4os.Compression.LZ4-LICENSE.txt) |
| Microsoft.Extensions.*, Microsoft.DotNet.PlatformAbstractions, Microsoft.Extensions.DependencyModel | 10.0.12 / 9.0.9 / 3.1.6 | .NET Foundation and Contributors | MIT | [Microsoft.Extensions-LICENSE.txt](third_party/licenses/Microsoft.Extensions-LICENSE.txt) |

## Bundled native libraries

These native binaries ship inside the Janitor UI and CLI output. The license column covers the
library itself; several statically link further permissive components, listed in the next section.

| Binary | Library | Author | License | License text |
|---|---|---|---|---|
| `opus.dll` | Opus | Xiph.Org, Skype Limited, and contributors | BSD 3-Clause | [Opus-LICENSE.txt](third_party/licenses/Opus-LICENSE.txt) |
| `libFLAC.dll` | FLAC | Josh Coalson, Xiph.Org Foundation | BSD 3-Clause | [FLAC-LICENSE.txt](third_party/licenses/FLAC-LICENSE.txt) |
| `vorbis.dll` | libvorbis | Xiph.Org Foundation | BSD 3-Clause | [libvorbis-LICENSE.txt](third_party/licenses/libvorbis-LICENSE.txt) |
| `liblz4.dll` | LZ4 | Yann Collet | BSD 2-Clause | [LZ4-LICENSE.txt](third_party/licenses/LZ4-LICENSE.txt) |
| `libzstd.dll` | Zstandard | Meta Platforms, Inc. and affiliates | BSD 3-Clause | [Zstandard-LICENSE.txt](third_party/licenses/Zstandard-LICENSE.txt) |
| `miniz.dll` | miniz | RAD Game Tools, Valve Software, Rich Geldreich | MIT | [miniz-LICENSE.txt](third_party/licenses/miniz-LICENSE.txt) |
| `glfw3.dll` | GLFW | Marcus Geelnard, Camilla Löwy | Zlib | [GLFW-LICENSE.txt](third_party/licenses/GLFW-LICENSE.txt) |
| `soft_oal.dll` | OpenAL Soft | OpenAL Soft / kcat | LGPL 2.0-or-later | [OpenAL-Soft-LICENSE.txt](third_party/licenses/OpenAL-Soft-LICENSE.txt) |
| `av_libglesv2.dll` | ANGLE | The ANGLE Project Authors | BSD 3-Clause | [ANGLE-LICENSE.txt](third_party/licenses/ANGLE-LICENSE.txt) |
| `libSkiaSharp.dll` | Skia | Google Inc. | BSD 3-Clause | [Skia-LICENSE.txt](third_party/licenses/Skia-LICENSE.txt) |
| `libHarfBuzzSharp.dll` | HarfBuzz | HarfBuzz contributors | Old MIT | [HarfBuzz-LICENSE.txt](third_party/licenses/HarfBuzz-LICENSE.txt) |
| `msquic.dll`, `System.IO.Compression.Native.dll`, `clr*.dll`, `coreclr.dll`, `host*.dll` | .NET Runtime | .NET Foundation and Contributors | MIT | [dotnet-runtime-LICENSE.txt](third_party/licenses/dotnet-runtime-LICENSE.txt) |

### Statically linked components inside bundled native libraries

The following third-party components are compiled into the native binaries above (verified against
the shipped Windows x64 binaries) and are covered by their own licenses.

| Component | Found in | Author | License | License text |
|---|---|---|---|---|
| libogg | `vorbis.dll`, `libFLAC.dll` | Xiph.Org Foundation | BSD 3-Clause | [libogg-LICENSE.txt](third_party/licenses/libogg-LICENSE.txt) |
| libpng | `libSkiaSharp.dll` | PNG Reference Library Authors | libpng 2.0 | [libpng-LICENSE.txt](third_party/licenses/libpng-LICENSE.txt) |
| libjpeg-turbo | `libSkiaSharp.dll` | Independent JPEG Group, D. R. Commander | IJG / BSD 3-Clause | [libjpeg-turbo-LICENSE.txt](third_party/licenses/libjpeg-turbo-LICENSE.txt) |
| zlib | `libSkiaSharp.dll` | Jean-loup Gailly, Mark Adler | Zlib | [zlib-LICENSE.txt](third_party/licenses/zlib-LICENSE.txt) |
| Expat | `libSkiaSharp.dll` | Thai Open Source Software Center, Clark Cooper | MIT | [Expat-LICENSE.txt](third_party/licenses/Expat-LICENSE.txt) |
| libwebp | `libSkiaSharp.dll` | Google Inc. | BSD 3-Clause | [libwebp-LICENSE.txt](third_party/licenses/libwebp-LICENSE.txt) |
| libtiff | `libSkiaSharp.dll` | Sam Leffler, Silicon Graphics | libtiff | [libtiff-LICENSE.txt](third_party/licenses/libtiff-LICENSE.txt) |
| giflib | `libSkiaSharp.dll` | Eric S. Raymond | MIT | [giflib-LICENSE.txt](third_party/licenses/giflib-LICENSE.txt) |
| Adobe DNG SDK | `libSkiaSharp.dll` | Adobe Systems Incorporated | Adobe DNG SDK License | [Adobe-DNG-SDK-LICENSE.txt](third_party/licenses/Adobe-DNG-SDK-LICENSE.txt) |
| {fmt} | `soft_oal.dll` | Victor Zverovich and contributors | MIT | [fmt-LICENSE.txt](third_party/licenses/fmt-LICENSE.txt) |

> This product includes DNG technology under license by Adobe Systems Incorporated.

## Other bundled components

| Component | Author | License | License text |
|---|---|---|---|
| Vorbis codebooks (`packed_codebooks.bin`) | Xiph.Org Foundation / aoTuV | BSD 3-Clause | [libvorbis-LICENSE.txt](third_party/licenses/libvorbis-LICENSE.txt) |

## Referenced or ported into RedFox (not distributed as separate files)

The following projects were used as references or had code ported into the RedFox libraries that
Janitor depends on. They are not distributed as standalone files but are credited here.

| Component | Author | License |
|---|---|---|
| DirectXTex | Microsoft | MIT |
| DirectXMath | Microsoft | MIT |
| DirectXMesh | Microsoft | MIT |
| GDeflate (Microsoft DirectStorage) | Microsoft | MIT |
| libdeflate | Eric Biggers | MIT |

## OpenAL Soft source offer

OpenAL Soft (`soft_oal.dll`) is distributed under the GNU Lesser General Public License
(LGPL) 2.0 or later. In accordance with that license, the corresponding source code for the
exact version distributed with each release is included in the release archive under
`licenses/source/OpenAL-Soft-1.23.1-d3875f3.zip`, and is also available from
<https://github.com/kcat/openal-soft/tree/d3875f3>.

## Trademarks

Game names, engine names, and all other trademarks are the property of their respective owners.
Janitor is an unofficial community tool and is not affiliated with, endorsed by, or supported by
Remedy Entertainment. See the disclaimer in [README.md](README.md).
