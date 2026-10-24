# Saved District_1 navigation coverage

This tool checks the exact baked dataset selected by the District_1 manifest.
It does not bake geometry, change placements, open the main project, or touch
save profiles. Prepare the marked scratch project using the sibling
`CycleNetworkSmoke` procedure first, and do not run both tools concurrently.

The input is `Library/DistrictCycleValidation/NavigationProbe.json`: coordinates
exported by a read-only Unity scene inspection, plus the selected NavMesh asset
path, GUID, SHA-256, bake version and agent type. Native queries run in the
isolated editor with only that dataset registered, at the same world origin as
`NavMeshWorldService`. A mismatching asset or an already registered dataset
refuses the audit instead of producing ambiguous results.

To refresh coordinates, temporarily install only
`Editor/DistrictCycleNavigationProbeSession.cs` in the source project's
`Assets/Editor`, import it, and create
`Library/DistrictCycleValidation/NavigationProbe.request`. It inspects saved
scenes in preview mode and consumes that request once. Remove the installed
helper after export; do not copy it into the isolated project. The query runner
copies only `DistrictNavigationAudit.cs`. Keep the resulting JSON as evidence.

```powershell
& ./Tools/Validation/DistrictNavigation/Run.ps1
```

Use `-SnapshotPath` for a proposed-coordinate snapshot. Reports are timestamped
under `Library/DistrictCycleValidation/NavigationAudit-*.json` with their logs.
They include triangulation bounds, each sampled point and route status inside
each cycle. Actors and interactions are measured separately: a moving enemy
requires a nearby floor point; interactions prefer ground beneath their anchor,
not a potentially isolated tabletop polygon.

A warning is not automatically a gameplay defect. Scene links, carving and
cycle-operated obstacles are deliberately absent from this baked-only query.
Interaction reach still needs the actual collider and controlled character;
physics collisions and camera clearance are not verified here. Root pivots can
also differ from interaction collider centers. Keep both in coordinate exports
and inspect the target before changing any placement.

Confirmed defects and proposed positions must be checked before applying them.
This tool never moves an object on the strength of an approximate sample alone.
