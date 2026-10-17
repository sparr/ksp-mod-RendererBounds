# Renderer Bounds

`README.md` covers what the mod does and how to install and build it. This
file holds what a contributor needs beyond that.

The plugin targets .NET Framework 4.8 and is built with the `KSPBuildTools`
NuGet package.

## Layout

- `src/RendererBounds/` is the plugin source and its `RendererBounds.csproj`.
- `GameData/RendererBounds/` is the folder players install. Config, localization, and
  the `.version` file are tracked there. `Plugins/`, `LICENSE`, and `README.md`
  in it are build output and are ignored.
- `Directory.Build.props` holds everything shared by all projects: metadata,
  the mod folder location, and the import of `Version.props`.
- `RendererBounds.props.user` (ignored, copied from `RendererBounds.props.user.example`)
  holds the path to the local KSP install.
- `install.sh` copies the built mod folder into a KSP install.
- `netkan/RendererBounds.netkan` is the CKAN metadata to submit to `KSP-CKAN/NetKAN`.
  Once it is merged there, the copy upstream is the one that counts.
- `.github/workflows/` uses `KSPBuildTools`, pinned to the same version as the
  NuGet package in the csproj. Bump both together. `build.yml` and
  `validate.yml` call its reusable workflows. `create-release.yml` calls its
  actions directly, because the reusable release workflow names the zip after
  the repository and the zip is named after the mod.

## How it works

- `Loader.cs` transpiles each method that collects a part's renderer bounds,
  replacing every `Renderer.bounds` read with `TightBounds.Of(renderer)`. The
  targets are stock `PartGeometryUtil.GetPartRendererBounds`, the prefix
  KSPCommunityFixes' `PartBoundsIgnoreDisabledTransforms` uses in its place,
  and `RP0.KCTUtilities.GetPartRendererBounds` while RP-1 still has it. A target
  that is not loaded is skipped. The log line per target gives the number of
  reads replaced, and 0 means that method's IL changed and the patch needs
  updating.
- Which renderers count stays with the patched method. This mod only changes
  how each one is measured.
- `TightBounds.cs` caches each mesh's hull by instance ID and recomputes it when
  the mesh's vertex count or local bounds change, which is how procedural parts
  rebuilding a mesh in place are noticed. Skinned meshes are baked on every
  call; in Unity 2019.4 `BakeMesh` output already includes the transform's
  scale. Unreadable meshes and other renderer types keep `Renderer.bounds`.
- `MeshHull.cs` holds the geometry and uses only managed UnityEngine value
  types, so the unit tests compile it. It drops non-finite and duplicate
  vertices, has QuickHull3D build the hull in doubles, and maps the hull's
  vertex indices back to the original float vertices. It falls back to the
  distinct vertices when QuickHull3D throws: `ArgumentException` for flat or
  otherwise degenerate input, anything else is logged.
- `ThirdParty/QuickHull3D/` is a git submodule of `BioFluidix/QuickHull3D`, a
  C# port of John E. Lloyd's Java QuickHull3D. Only `QuickHull3D/*.cs` is
  compiled into the plugin, not its `Properties/`, test project, or solution.
  It merges coplanar faces, so a vertex in the middle of a flat face or a
  straight edge is never a hull vertex, and its tolerance scales with the
  coordinates. Its own `Point3d`/`Vector3d` types are double-precision classes
  carried over from the Java original. If it needs changes, fork it and point
  the submodule at the fork.
- `THIRD-PARTY-NOTICES.md` holds QuickHull3D's MIT license and Lloyd's notice,
  which his terms require to be kept. The build copies it into the mod folder.
- Clone with `git clone --recursive`, or run `git submodule update --init`
  after cloning.

## Building

`dotnet build` at the repo root. On Linux, `KSPBuildTools` does not detect a
Steam install, so `RendererBounds.props.user` or `KSP_ROOT` is required.

The game install is only read for reference assemblies, and nothing is
deployed into it, provided `RendererBounds.props.user` sets
`KSPBT_InstallCKANDependencies` to `false` as the example file does. Without
that, a `ModReference` with a `CKANIdentifier` makes `KSPBuildTools` run
`ckan install` against the install on every build and restore.

`KSPBuildTools` generates the `[KSPAssembly]` attribute from the version, and a
`[KSPAssemblyDependency]` for each `ModReference` in the csproj. A dependency
that is missing at runtime is silent: KSP skips the plugin with a warning in
`KSP.log` and nothing on screen. The dependency is declared under the DLL's
file name unless the `ModReference` sets `KSPAssemblyName`. Harmony needs
`HarmonyKSP`, because that is the name `0Harmony.dll` declares.

## Installing into a game

`install.sh` writes each file under a temporary name and renames it into
place, so it is safe to run while the game is running. The new build loads at
the game's next start.

## Version

The version is written in two tracked files, `Version.props` and
`GameData/RendererBounds/RendererBounds.version`. Both are generated from the
`.versiontemplate` file beside them. Do not edit the generated files. Edit the
templates when the content around the version needs to change, and regenerate.

The `create-release` workflow regenerates both. To do it locally, use
`update-version.sh` from the `KSPBuildTools` repository:

```sh
update-version.sh 1.2.3 .versiontemplate
```

## Changelog

`CHANGELOG.md` is in the format `yaclog` reads. Add entries as bullets under
`## Unreleased`. The release workflow renames that section to the version and
date, and uses `yaclog-ksp` to generate `GameData/RendererBounds/changelog.cfg` for the
Kerbal Changelog mod. Do not edit `changelog.cfg` by hand. The table at the
top of `CHANGELOG.md` supplies that file's metadata. `yaclog-ksp` silently
drops any row whose key or value contains a hyphen.

## Releasing

Run the `create-release` workflow from the GitHub Actions tab with a version.
It updates the changelog and version files, commits, tags, pushes to the
branch it was run on, builds in Release, and creates a draft GitHub release
with the zip attached. Publishing the draft is a manual step.

## Config files

- Name ModuleManager passes after the mod: `@PART[...]:FOR[RendererBounds]`. Use
  `:NEEDS[OtherMod]` for patches that support another mod.
- Braces go on their own lines. KSP's parser silently misreads
  `NODE { a = b }` written on one line.
- State written at runtime goes in `GameData/RendererBounds/PluginData/`, which
  ModuleManager does not read.
- Localization tags are `#LOC_RendererBounds_...` in
  `GameData/RendererBounds/Localization/`.
