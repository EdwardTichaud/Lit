# Cycle dialogue and Timeline smoke

Windows, matching Unity editor and Windows Standalone/Mono support required.

```powershell
& ./Tools/Validation/CycleNetworkSmoke/Run.ps1
```

This copies runtime code, embedded packages and the TMP UI resources into a marked
disposable project under `Library/DistrictCycleRuntimeProject`. It does not open,
save or rebuild the main project's scenes. The test application's company/product
names isolate its persistent data from Lit. Connections bind to loopback only.

The build helper refuses to operate outside the marked validation project; the
runtime helper refuses to install in an application with another company/product.
Neither helper lives in the production Assets folder.

Reports, player and logs live under `Library/DistrictCycleValidation`. Each run
uses a new report directory, records hashes of the tested source files, builds
without graphics and starts real independent player processes. The initial import
can take several minutes. Subsequent runs reuse the isolated project cache.

Scenarios: normal host/client; occupied client UI; interrupted dialogue and retry;
client disconnect; late arrival during a delayed presentation; all clients leaving
a dedicated server; controller disable/reenable during playback.

Native Timeline scenarios use a single real player process and an authored
one-second animation, without a character or save profile:

```powershell
& ./Tools/Validation/CycleNetworkSmoke/Run.ps1 -Scenarios timeline,timeline_pause,timeline_stop,timeline_external_stop,timeline_service,timeline_missing,normal
```

They check natural completion, a pause longer than the authored duration,
explicit handle cancellation, direct Director cancellation, service loss and
an unbound active track. The public animation field must actually advance;
the completion notification must be unique and the Director's authored wrap
mode must be restored. The normal host/client check is included above to
exercise the existing network path as well. Timeline modes are opt-in; the
default run retains the seven dialogue/network scenarios.

The `sequence` and `sequence_interrupt` scenarios connect two real player
processes and run the native Timeline through `CycleController` itself:

```powershell
& ./Tools/Validation/CycleNetworkSmoke/Run.ps1 -Scenarios sequence,sequence_interrupt
```

The first requires one shared successful outcome and a replicated memory
milestone on both peers. The second stops the client's first graph, requires
a shared failure with presentation restored and no milestone, then explicitly
retries on the host. Both peers must animate two attempts but report exactly
one final success. A second terminal interaction keeps the fixture loaded for
inspection. No fake acknowledgement is sent: the production coroutine observes
the actual playback handle and sends its normal NGO result RPC.

These sequence fixtures deliberately have no character, Cinemachine shot or
group-start gate. They validate the shared playback/result barrier, not the
character locks, rendezvous votes or the authored Ardent scene. Those require
separate full-character integration and gameplay passes.

Use `-Scenarios normal,disable` for a subset, or `-UnityExe` for a nonstandard
editor install location. Do not run two invocations against the same validation
directory. Runtime source copies are refreshed; old code copies are removed only
inside that marked disposable Assets folder.

These assertions verify the actual UI lifecycle, NGO RPC transport and narrative
progression protocol. They do **not** verify HDRP appearance, physical player
interaction, navigation, the authored quest scenes or their Timeline camera shots.
Keep the cycle EditMode tests and complete gameplay acceptance passes as well.
