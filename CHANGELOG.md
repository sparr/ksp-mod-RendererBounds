# Changelog

All notable changes to this project will be documented in this file.

| modName | Renderer Bounds     |
| license | MIT         |
| author  | Clarence "Sparr" Risher |

## Unreleased

- Measure part renderer bounds from the convex hull of each mesh's vertices instead of `Renderer.bounds`, in stock `PartGeometryUtil.GetPartRendererBounds`, KSPCommunityFixes' replacement for it, and RP-1's copy of it.
- `enabled` setting in `Settings.cfg`.
