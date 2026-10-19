# Downloaded asset cleanup

The Unity menu `Tools/Lit/Assets/Audit Downloaded Packs (Read Only)` writes
`Library/LitDownloadedAssetAudit.json`. Its explicit downloaded-pack roots are
listed in `Tools/AssetCleanup/scopes.json`.
All external project/package assets are roots, including scenes outside the
build. Scripts, shaders, Resources and StreamingAssets within packs are also
roots. The recursive Unity dependency set is retained.

Review and cross-check before quarantine:

```powershell
python Tools/AssetCleanup/quarantine.py --project C:/Users/pierr/git/Lit
python Tools/AssetCleanup/verify_references.py --project C:/Users/pierr/git/Lit
```

An apply requires an explicit new archive directory outside the project:

```powershell
python Tools/AssetCleanup/quarantine.py --project C:/Users/pierr/git/Lit --archive D:/LitAssetArchive/unique-run-name --apply
```

The apply refuses stale audits, changed dependencies, changed GUIDs and sizes,
paths outside the audited packs, and references from retained files. Every file
and its metadata is archived and SHA-256 checked before any source is removed.
Keep Unity closed during quarantine and restore. A single pack can be selected
with `--pack MedCastle`.

Multiple cleanup passes have separate archives. For a complete restoration,
restore them in reverse chronological order so dependencies retained by an
earlier pass and removed by a later pass are available again.

If a dependency change refused removal after backup, refresh the Unity audit
and repeat with `--resume` and the same archive directory. It rechecks the
stored bytes and current sources instead of copying the backup again. Changes
to C# arithmetic are allowed only when the audited asset-path/GUID tokens are
identical; new asset references still refuse removal. Serialized asset changes
always require a fresh audit.

Restore without overwriting changed assets:

```powershell
python Tools/AssetCleanup/restore.py D:/LitAssetArchive/unique-run-name
```

Archive storage remains in use on the destination drive; quarantine frees the
Unity project and its source drive, not the total space across both drives.
Do not purge the archive until the relevant game flows have been validated.
The tools do not remove Unity caches, change texture quality, migrate gameplay
references, or claim that arbitrary runtime-constructed asset paths are proven
safe by static dependency analysis. Review such loading code before applying.
