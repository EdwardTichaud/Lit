# Managed Smoke Harness

Run without launching Unity:

```powershell
dotnet run --project Assets/CombatRealTime/Migration/Network/Tests~/NetworkAuthoritySmokeTests.csproj
```

Requires .NET 9 SDK. No external test packages. The harness compiles the actual
authority, contracts and reservation sources against minimal managed engine
doubles. The trailing `~` keeps the doubles outside Unity asset compilation.

31 scenarios cover server-only admission, registration/allowlist/assignment,
ownership, invalid ranges/targets/input, replay/rate limiting, cooldown aliases,
generation/epoch invalidation, duplicate impacts, pair contention, idempotent
release, death/disconnect/despawn/ownership/assignment/timeout cleanup,
cross-scene commands/impacts, unrelated additive churn, participant unload,
explicit/NavMesh world invalidation and executor exceptions; stable GUID catalog
identity/collision rejection; supported/unsupported family classification and
clip marker validation; contact ordering, self-capable Intercession policies,
executor attachment, explicit post-despawn cleanup and retained cooldowns.

These tests do NOT validate native Unity destroyed-object behavior, actual
scene loading, transport, RPC serialization/postprocessing, player movement,
timeline playback, authoritative combat damage or host/remote-client gameplay.
Production runtime sources were additionally compiled with the current project
source set against installed Unity/NGO references. Six Unity-visible NUnit
catalog/marker/ranking/enemy-health tests live in `../Editor/CombatNetworkCatalogTests.cs`.
Those require the parent's Unity test pass; they are not run by this harness.
This is a source compile, not Unity RPC postprocessing or a network runtime test.
