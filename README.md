# Half-Life Unified SDK Map Decompiler

The Half-Life Unified SDK Map Decompiler is a cross-platform map decompiler for Half-Life 1 BSP version 29 (Half-Life Alpha 0.52) and 30 files.

Quake 1/2/3 and Source maps, as well as engine offshoots like Svengine and Paranoia 2 are not supported.

User configuration files for this application are stored in `AppData/Roaming/Half-Life Unified SDK` on Windows, and `home/.config` on Unix systems.

Due to a lack of data in the BSP file the original brushes cannot be restored.
This data is stored in a lossy format, so a perfect recreation is not possible.
Decompiled maps will never be 100% accurate, will be missing some geometry and will have invalid geometry in some cases.

This tool is part of the Half-Life Unified SDK. See the main [Unified SDK](https://github.com/twhl-community/halflife-unified-sdk) repository for more information.

Based on Quake 3's bspc tool: https://github.com/id-Software/Quake-III-Arena

Includes code from Sledge by Daniel 'Logic & Trick' Walder: https://github.com/LogicAndTrick/sledge

Includes code from Sledge.Formats by Daniel 'Logic & Trick' Walder: https://github.com/LogicAndTrick/sledge-formats

# Requirements

The graphical interface requires Windows 8 or newer. The command line version requires Windows 7 or newer.

You will need the .NET 6 Desktop Runtime (the .NET SDK includes the runtime): https://dotnet.microsoft.com/en-us/download

Make sure to install the latest version. .NET runtimes receive updates over time that require the latest minor version to be installed to work with programs built to use them.

# Decompiler Strategies

2 decompilers strategies are supported: Tree-Based and Face-To-Brush.

## Tree-Based Decompiler

This decompiler works by processing the [Binary Space Partitioning](https://en.wikipedia.org/wiki/Binary_space_partitioning) tree for hull 0 (point hull) for each brush model.

The world itself is model 0 and contains all brushes not tied to a brush entity. Each brush entity adds one brush model to the map.

The process used to generate brushes works by first calculating the bounding box for the model, then adding 8 units extra to it to account for walls on the outermost brush faces in the map.

A brush of that size is then created, and is split in 2 repeatedly by walking the BSP tree.

The resulting set of brushes is an approximate representation of the original map, but is not perfectly accurate.

The texturing phase tries to find the faces that the generated brush faces are the closest match to.

If brush optimization is set to best texture match the brush is split if needed to more closely match the original brush face.

If brush merging is enabled brushes that are found to have the same contents (e.g. solid, water, etc) and texture properties and that form a convex brush are merged together.

Finally, each brush is converted to its map source file representation.

Brushes that have no textures on any faces are skipped. This includes brushes that originated as `CLIP` or `NULL` textured brushes. This can leave some brush entities without any brushes.

## Face-To-Brush Decompiler

This decompiler works by converting each brush face in the map to a brush of its own.

This uses the map's visual meshes used to render brush models. This does not include `CLIP` brushes and brushes whose texture is stripped by the compiler (e.g. `NULL`).

The process used to generate brushes works by first merging faces that have matching texture properties and that form a single flat and convex polygon.

Each face is then converted to a brush by cloning the polygon, inverting it and offsetting it by the inverse of the face normal to form the back face. Additional faces are generated to connect the two faces.

Finally, each brush is converted to its map source file representation.

# Options Explained In-Depth

Most options are self-explanatory but a few are not so obvious.

## Always generate origin brushes for brush entities

Normally brush entities only have an origin brush generated for them when the entity has a non-zero `origin` keyvalue. This option causes origin brushes to be generated even if it is undefined or zero.

## Trigger entity classname wildcards

This option allows a list of classnames to be specified which should have the `AAATRIGGER` texture applied to any brush faces whose texture could not be automatically detected.

This supports the use of [wildcards](https://en.wikipedia.org/wiki/Matching_wildcards) to allow matching all entities of a certain class, for example to match all trigger entities:
```
trigger_*
```

# Command Line Version

To use the command line version run `MapDecompilerCmdLine.exe` or `dotnet MapDecompilerCmdLine.dll` (for Linux users) in a command line window.

Running without arguments prints help text. You may need to install `dotnet-suggest` first for this to work:
```bat
dotnet tool install -g dotnet-suggest
```

The available commands are listed. The commands should be:
* Tree
* FaceToBrush

Running the command `MapDecompilerCmdLine.exe <command> -h` prints the help text for each command, which shows the list of available options for that command.

To decompile a map run that command with the options you wish to choose.

For example:
```bat
MapDecompilerCmdLine.exe Tree "path/to/Half-Life/valve/maps/c1a0.bsp" --apply-null true
```

This will decompile the map `c1a0` and apply `NULL` to all brush faces newly generated by the decompiler. The result will be placed in the current working directory which should be the directory containing `MapDecompilerCmdLine.exe` unless otherwise specified.

# Changelog

Common to both:

* More crash-resistent with error reporting when a crash does occur
* Fixed crashes when loading BSP files compiled with more modern compilers that apply optimizations the decompiler did not account for
* Fixed problems parsing entity data in some cases
* Use double precision maths to process maps. This increases the accuracy of calculations and solves problems with complex brushwork ending up deformed, and also solves problems with some maps (e.g. `cz_lostcause`) getting stuck in an infinite loop due to lack of precision
* Added support for Half-Life Alpha BSP format
* Correct normals and vertices that are slightly off the axis normal (fixes some bad brushes in `cz_recoil` and likely other maps)
* Increased maximum map size to allow decompilation of maps that use maximum engine limits. Some maps used the absolute maximum limit of [-16384, 16384] allowed in the original engine which didn't decompile correctly. Was originally [-4096, 4096], is now [-1,048,576, 1,048,576], larger than any known Half-Life 1 engine variant
* Ignore duplicate worldspawn entities if they exist in the map (was causing world brushes to be repeatedly decompiled and placed at the entity's origin)
* Added option to apply `NULL` to generated faces. Note that due to inherent limitations in decompiler accuracy some faces that appear valid will be textured with `NULL`
* Added option to always generate origin brushes for brush entities even if the origin is `0 0 0`
* Added check to detect when BSP files have version `1329865020`. These are actually XML or HTML files, usually the result of a FastDL server refusing to allow downloads and sending an error page which the game interprets as the file. `1329865020` is the integer value of the characters `<!DO` treated as one value

## Tree-based decompiler compared to bspc

* Fixed some brushes being extremely large due to rounding errors
* Added option to extract embedded textures to a wad file (matches BSP2Map behavior)
* Removed obsolete map file limits
* Fixed cleared map bounds having arbitrary maximum size (some BSP format variants exceed the old size, but there are other places where low limits are in place)
* Added checks to skip invalid models (e.g. HL Alpha `c2a4b` has `ambient_generic` entities that reference brush models that don't exist)
* Fixed decompiler checking brush faces not part of the current model when matching textures (e.g. in `c1a0` the doors that open at the start had the wrong texture)
* Fixed some Day of Defeat maps causing decompilation failure due to Null contents (the compiler is supposed to convert Null contents to Solid contents, which the compiler used for those maps evidently did not do)
* Fixed cases where faces are incorrectly thought to lie on a winding that it is parallel to. This caused some faces to get a seemingly random texture, actually a texture used on a face that happens to be on the same plane as the one created by the root BSP tree node
* Fixed exterior faces (faces that touch the void outside the map) generated by compiler being considered for texturing. They will still receive textures but are not checked for potential matches with existing faces since there aren't any
* Added wildcard matching to apply `AAATRIGGER` to brushes lacking any textures (referred to as "trigger entity classname wildcards")
* Increased plane hashes array size to match BSP file format's `MAX_MAP_PLANES` limit

## Face-to-brush decompiler compared to BSP2Map

* Fixed "texture axis perpendicular to face" problems when loading decompiled maps in a level editor
* Improved accuracy of decompiled brushes

# LICENSE

See [LICENSE](/LICENSE) for the GNU GENERAL PUBLIC LICENSE

See [Sledge_LICENSE](/Sledge_LICENSE) for the BSD 3-Clause license

See [Sledge_Formats_LICENSE](/Sledge_Formats_LICENSE) for the MIT license
