# Half-Life Unified SDK Map Decompiler

The Half-Life Unified SDK Map Decompiler is a cross-platform map decompiler for Half-Life 1 BSP version 29 (Half-Life Alpha 0.52) and 30 files.

Unlike bspc and tools derived from it this decompiler does not support other Quake engine derivative BSP files.

Whereas bspc was written in C this tool is written in C# to make its source code more accessible and easier to maintain.

User configuration files for this application are stored in `AppData/Roaming/Half-Life Unified SDK` on Windows, and `home/.config` on Unix systems.

Only Half-Life Alpha 0.52 and original Half-Life BSP files are supported. Quake 1/2/3 and Source maps, as well as engine offshoots like Svengine and Paranoia 2 are not supported.

Due to a lack of data in the BSP file the original brushes cannot be restored. Decompiled maps will never be 100% accurate, will be missing some geometry and will have invalid geometry in some cases.

This tool is part of the Half-Life Unified SDK. See the main [Unified SDK](https://github.com/SamVanheer/halflife-unified-sdk) repository for more information.

Based on Quake 3's bspc tool: https://github.com/id-Software/Quake-III-Arena

Includes code from Sledge by Daniel 'Logic & Trick' Walder: https://github.com/LogicAndTrick/sledge
Includes code from Sledge.Formats by Daniel 'Logic & Trick' Walder: https://github.com/LogicAndTrick/sledge-formats

# Decompiler strategies

2 decompilers strategies are supported: Tree-based and Face-To-Brush.

## Tree-based decompiler

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

## Face-To-Brush decompiler

This decompiler works by converting each brush face in the map to a brush of its own.

This uses the map's visual meshes used to render brush models. This does not include `CLIP` brushes and brushes whose texture is stripped by the compiler (e.g. `NULL`).

The process used to generate brushes works by first merging faces that have matching texture properties and that form a single flat and convex polygon.

Each face is then converted to a brush by cloning the polygon, inverting it and offsetting it by the inverse of the face normal to form the back face. Additional faces are generated to connect the two faces.

Finally, each brush is converted to its map source file representation.

# Requirements

You will need the .NET 6 or newer Desktop Runtime (the .NET SDK includes the runtime): https://dotnet.microsoft.com/en-us/download

# LICENSE

See [LICENSE](/LICENSE) for the GNU GENERAL PUBLIC LICENSE
See [Sledge_LICENSE](/Sledge_LICENSE) for the BSD 3-Clause license
See [Sledge_Formats_LICENSE](/Sledge_Formats_LICENSE) for the MIT license
