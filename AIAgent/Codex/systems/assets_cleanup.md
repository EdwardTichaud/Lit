# Downloaded assets and quarantine

Read this page only for asset cleanup or restoration work.

## Invariants

- Downloaded packs can contain live game dependencies. Never remove a complete
  pack on the basis of its name, build-scene membership, or Git ignore status.
- Preserve recursive dependencies of all external project/package assets,
  ProjectSettings GUID references, code path references, shaders, scripts,
  Resources and StreamingAssets. Keep complete animation libraries used by
  the existing path-based installers until those selectors are audited.
- Keep asset GUIDs and metadata. No texture quality reduction is implicit in
  cleanup. Never remove another task's files or Unity caches as a shortcut.
- Use explicit UTF-8 when reading Unity JSON reports; Windows default encodings
  create false missing-file reports for accented filenames.

## Tools

`Assets/Editor/LitDownloadedAssetAudit.cs` is an explicit, read-only Unity menu
and batch entry point. The 14 downloaded-pack roots are listed explicitly in
`Tools/AssetCleanup/scopes.json`; they cover Fab, UnityAssets and standalone
vendor folders. It writes the
dependency audit under Library. It does not run continuously in the editor.

`Tools/AssetCleanup/README.md` documents quarantine, cross-check and restoration.
Archive and verify files on D: before removing source files from Assets. Refuse
changed serialized dependencies and newly introduced code asset references.
Keep Unity closed during removal and restoration.

## First pass: 2026-10-18

- Archive: `D:/LitAssetArchive/2026-10-18-fab/assets.zip`.
- Restore manifest: same directory, `manifest.json`.
- Removed from Assets: 7,558 assets and their metadata, 21,853,578,821 bytes.
- MedCastle contributes 12,534,538,967 bytes to this reduction; its referenced
  meshes, materials and textures remain. HDRP settings inside IdaFaber remain.
- TwinBladesBundle and Mobility_Starter remain complete for authoring tools.
- Independent retained-file GUID scan passes after removal. All audited retained
  Assets paths exist. Eight quarantine/restore safety tests pass.
- Unity 6000.6.4f1 post-cleanup audit exits successfully with zero remaining
  Fab candidates and 35,905 preserved dependency paths. No audited retained
  Assets path is missing after Unity refresh. Log:
  `Logs/DownloadedAssetAuditPostCleanup.log`.
- This is a source-drive/project reduction, with a retained backup on D:, not
  a claim of net disk savings across both drives or validated rendered gameplay.

## Expanded pass: 2026-10-18

- Archive: `D:/LitAssetArchive/2026-10-18-other-packs/`.
- Removed: 1,429 assets and metadata, 6,171,847,403 bytes.
- Combined: 8,987 assets, 17,974 files, 28,025,426,224 bytes removed from Assets.
- Werewolf: 2.160 -> 0.438 GB; Sci-Fi Dog: 1.243 -> 0.337 GB.
- Explicit Nina controller, counter audio and terrain-tree authoring references
  are retained. Character Effects remains an active package: Scar prefabs and
  ghost materials directly reference its scripts, effects and textures.
- Combined serialized GUID cross-check for both archives passes; all retained
  Assets paths exist after refresh. No archived asset has reappeared.
- Final Unity 6000.6.4f1 expanded audit succeeds with 14 scopes, zero remaining
  candidates and 34,476 preserved dependency paths.
- Ten targeted Unity Edit Mode cases pass (Nina controller/scene, ghost prefabs,
  flame prefabs and light profiles). Report: `Logs/AssetCleanupAcceptance.xml`.
- Thirteen quarantine/restore safety tests pass, including multi-scope bounds,
  open-Unity refusal, stale references, pre-1980 file timestamps, partial archive
  recovery, and refusal to overwrite later work during restoration.
- For a complete rollback, restore other-packs first, then fab. This restores
  dependencies that an earlier pass retained but a later pass removed.
- Full rendered gameplay has not been replayed. These checks establish asset
  presence/dependencies and targeted loading/animation contracts, not a new
  whole-game performance or visual-quality qualification.

## Animation/controller consolidation: 2026-10-25

Targeted cleanup of Lucian, Juggernaut, StoneGolem and Werewolf. Archive:
D:/LitAssetArchive/2026-10-25-animation-cleanup-101902. The prior archive ending
101544 preserves the original Generic StoneGolem importer metadata.
StoneGolem's main model now owns the valid Humanoid avatar previously used by
the workshop; its GUID remains unchanged. Latest author controller retained.
Execution/Executed1 adaptations retired; CounterHit Timeline/signals and the
previous facing/foot calibration work retained. Required overrides and shared
Player_Model remain. Report: Library/CharacterAnimationCleanup/report.json.
Final removal completed with Unity closed: four assets and four metadata
files, SHA-256 archive equality verified and no retained serialized/code GUID
references found immediately before removal. Restore details:
closed-editor-removals.json in the archive. Reimport and rendered gameplay
must still be qualified after reopening Unity.
