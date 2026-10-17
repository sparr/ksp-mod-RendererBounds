# Renderer Bounds

Measures craft size from part mesh geometry instead of inflated axis aligned renderer bounding boxes.

## Why?

KSP's craft size calculations use axis aligned bounding boxes around each part. This isn't a problem, since only the axis aligned dimensions are counted.

Except that's not true. The bounding box that counts in world coordinates is around the bounding box of the part in its own local coordinates, so if you rotate a part 45 degrees then its world dimensions will increase by 41% even if the part is a cylinder.

Except that's still not true. The part's bounding box is calculated from the bounds of each mesh, and each mesh can have its own rotation transform. So a cylinder mesh that's been rotated 45 degrees will increase the part's dimension by 41%, then if you rotate the part 45 degrees that compounds *another* 41%, for a total of +100% to the size of the part.

I created this mod because I was tired of 1.25m tanks making my craft 1.8-2.5m wide, so they wouldn't fit in my 2x2m LC, among many other less straightforward failure modes.

## How?

Renderer Bounds measures the part's renderers (effectively the meshes) from their actual vertices, using a convex hull of each mesh, so a rotated part measures its true extent. For an ordinary mesh that is never larger than before. A skinned mesh (ladders, parachutes, some solar panels) is measured from its current pose, which its stock bounding box can lag behind.

It changes the bounds that every user of `PartGeometryUtil.GetPartRendererBounds` sees: craft size and the size limit checks, the Engineer's Report, cargo bay and inventory fit, and craft thumbnails.

Which renderers count is unchanged. KSPCommunityFixes and RP-1 each discount certain renderers, and they will continue to do so with this mod enabled.

## Minutiae

Set `enabled = false` in `GameData/RendererBounds/Settings.cfg` to turn it off.

Craft files store their size when saved, so an existing craft shows its old size until it is saved again in the editor.

## Installing

Install with [CKAN](https://github.com/KSP-CKAN/CKAN), or download the latest zip from [releases](https://github.com/sparr/ksp-mod-RendererBounds/releases) and copy `GameData/RendererBounds` into your KSP `GameData` folder.

Requires KSP 1.12 and [Harmony](https://github.com/KSPModdingLibs/HarmonyKSP).

## Building

You need the [.NET SDK](https://dotnet.microsoft.com/download) (8 or later) and a KSP 1.12 install.

1. Clone with `git clone --recursive`, or run `git submodule update --init` in an existing clone.
2. Copy `RendererBounds.props.user.example` to `RendererBounds.props.user` and set the path to your KSP install. Alternatively, set the `KSP_ROOT` environment variable.
3. Run `dotnet build`.

The plugin is written to `GameData/RendererBounds/Plugins/`. Run `./install.sh` to copy the mod folder into the KSP install.

## License

MIT. See `LICENSE`.

`src/RendererBounds/ThirdParty/QuickHull3D/` is a git submodule of [QuickHull3D](https://github.com/BioFluidix/QuickHull3D), BioFluidiX's MIT-licensed C# port of [John E. Lloyd's QuickHull3D](https://www.cs.ubc.ca/~lloyd/java/quickhull3d.html). Its license and Lloyd's notice ship in the mod folder as `THIRD-PARTY-NOTICES.md`.
