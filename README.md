[![License: MIT](https://img.shields.io/badge/License-MIT-greed.svg)](LICENSE)

## Features
- Extract Boundary

----

## Dependencies
- [Utilities](https://github.com/Besjan/Utilities)
- [OsmSharp](https://github.com/OsmSharp/core)
- [protobuf-net](https://github.com/protobuf-net/protobuf-net)
- [ProjNet](https://github.com/NetTopologySuite/ProjNet4GeoAPI)
- [protobuf-net](https://github.com/protobuf-net/protobuf-net)
- [MessagePack](https://github.com/neuecc/MessagePack-CSharp)

----

## Notes
- Use [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) to quickly install NuGet packages in Unity
    - To install OsmSharp add "<package id="OsmSharp" version="6.2.0" />" in packages.config.
    - To install protobuf-net add "<package id="protobuf-net" version="2.3.7" />" in packages.config.
    - To install ProjNet add "<package id="ProjNet" version="2.0.0" />" in packages.config.
- If there are conflicts with "System.Runtime.CompilerServices.Unsafe.dll" from Unity Collections package, copy the later from ".../Library/PackageCache" to "Packages" and delete "System.Runtime.CompilerServices.Unsafe.dll".
