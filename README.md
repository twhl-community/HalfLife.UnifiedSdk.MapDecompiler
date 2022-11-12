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

# Requirements

You will need the .NET 6 or newer Desktop Runtime (the .NET SDK includes the runtime): https://dotnet.microsoft.com/en-us/download

# LICENSE

See [LICENSE](/LICENSE) for the GNU GENERAL PUBLIC LICENSE
See [Sledge_LICENSE](/Sledge_LICENSE) for the BSD 3-Clause license
See [Sledge_Formats_LICENSE](/Sledge_Formats_LICENSE) for the MIT license
