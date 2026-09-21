# Validation and performance

## Verified result (2026-09-20, versioned grid settings)

- SENetworkAPI 2.0.0: pinned to `3a83159f63cad90916df351e801c2b8e17031544`. All six production files, the license, and imported upstream tests match the repository byte-for-byte.
- Release suite: **430 passed**, zero failed/skipped, including upstream regressions and mod settings integration.
- Production coverage: **96.53% lines** (1114/1154), **91.28% branches** (680/745). Test code and stubs are excluded.
- Real-game Release build and MDK whitelist analysis pass. Upstream retains obsolete handler/cleanup APIs, so CS0618 warnings are expected and left visible.
- `git diff --check`: passed.

These results supersede the earlier locally modified networking suite. Upstream's tests explicitly document its unauthenticated sender IDs; passing them does not establish network authorization safety.

## Automated checks

The suite is entirely C# and compiles all shipped mod sources directly. It replaces the earlier Python curve-extraction harness.

| Area | Checks |
| --- | --- |
| Configuration | Every numeric field, NaN/infinities, negatives, randomized normalization, ordering, idempotence, independent defaults; grouped boost/resistance normalization, required groups, schema version fallback and round trips |
| Curves | Endpoint/midpoint behavior, default curves, bounded sweeps; arbitrary point counts, sorted knots, plateaus, rising/falling segments, invalid-point rejection and allocation-free binary search |
| Storage | World precedence, local migration, missing files, invalid XML and outdated-file preservation, version recovery, chat notices on host and client, write failures, client read/write restrictions |
| Serialization | XML/protobuf round trips, global tags 1–6, grid tags 23–24, version tag 25 and runtime notice tag 26; removed tags 7–22 remain reserved; compressed packets |
| Simulation | Drag, hard caps, coasting, static grids, zero mass, pre-existing grids, delayed physics, duplicates, removal, activation across subgrids |
| Lifecycle/UI | Reload notifications, world limits, parachutes, HUD throttling and absent players, remote controls, session restart cleanup |
| Mod API | Discovery before/after backend startup, delegate results, malformed endpoints, duplicate load, unload, speed caps and all thrust directions |
| Networking | Complete imported upstream suite: commands, compression, batching/coalescing, unreliable delivery, fetch, private/offline sessions, lifecycle, routing and documented sender-identity limitations; mod integration tests for settings fetch/reply, reload promotion checks and cleanup |
| Performance | No group queries when filters are disabled; zero warmed-loop managed allocations with recording disabled |

Tests use simulated game services, adapted from the neighboring SENetworkAPI test harness and extended for this mod. They do not emulate Havok, the game's compression format, or the game's exact protobuf fork. Real assembly compilation plus the MDK whitelist analyzer checks the actual API surface and script restrictions separately. No game assemblies are copied into the test output.

## Optimization pass: API allocations and grid activation

```sh
DOTNET_TieredCompilation=0 dotnet run --project tests/RelativeTopSpeed.Benchmarks -c Release
```

The harness warms each scenario for 600 frames and measures another 600 at 100, 1,000 and 10,000 grids. Scenarios cover drag without filters, ships satisfying their own thrust filter, ten-grid mechanical groups sharing thrust, and repeated scalar max-speed queries. Grid mass is 1,000,000 kg so the cruise calculation exercises interpolation. Force recording uses counters; HUD is disabled. Scalar query timings use grids without thrust blocks to isolate API overhead; separate regressions cover six directions, multiple thrusters, changing thrust/mass, rounding and array ownership.

A local .NET 9 Release comparison with tiered compilation disabled:

| Scenario, 10,000 grids × 600 frames | Before | After |
| --- | ---: | ---: |
| Scalar max-speed query time | 208.409 ms | 107.278 ms |
| Scalar max-speed query allocations | 528,000,000 bytes | 0 bytes |
| Mechanical-group queries, shared thrust | 30,000 | 3,000 |
| Mechanical-group queries, self-contained ships | 30,000 | 0 |
| Unfiltered drag simulation time | 62.332 ms | 61.372 ms |
| Shared-thrust simulation time | 67.577 ms | 67.115 ms |

Full results: [before](optimization-before.csv), [after](optimization-after.csv). Timings are single-run observations and vary by machine and runtime; query counts and allocated bytes are the stronger evidence. Simulation frame timing is essentially unchanged in this lightweight harness. These are mod-code measurements against simulated services, not Space Engineers FPS or Havok performance.

Changes:

- A six-field value type accumulates directional acceleration. Scalar max-speed/HUD boost queries avoid temporary arrays; public array methods still return independent caller-owned arrays. Values are recomputed on each query so cargo/thrust changes remain visible.
- Activation checks skip mechanical-group queries when the ship satisfies its own filters. Otherwise all members share one eligibility result within that refresh. Both successful and failed results are discarded on the next refresh or settings change; group topology and block changes are not cached indefinitely.
- Hidden HUD/debug output exits before consulting player/controller state.
- The test object-builder key now implements typed equality/hash code so simulated dictionary lookups do not introduce reflection/boxing allocations absent from the real key. Both final benchmark runs used the same corrected stubs.

The pinned SENetworkAPI production files remain unchanged. Regression tests cover cache invalidation after block, settings and topology changes, scalar allocation behavior and API result equivalence.

## In-game acceptance checks still required

Automated tests and a successful whitelist build cannot establish real multiplayer physics behavior. Before publishing:

1. Load the reported 600 m/s config and confirm `/rts hud` shows finite cruise speeds for large and small grids below, inside and above each mass range.
2. Fly with boosting on; verify resistance above cruise and the grid-specific boost cap. Disable boosting, coast, add cargo and reload settings; verify the new cruise limit is applied without oscillation.
3. Test rotor/piston subgrids with thrust and cockpit filters, converting ships to stations and back, spawning/pasting grids and deleting moving grids.
4. Join a dedicated server and a private listen-server world. Confirm client settings/HUD match the server and ordinary clients follow reload promotion checks. This does not establish sender authenticity: upstream accepts packet-supplied IDs and settings updates, as described in the README.
5. Reload settings with remote-control blocks present and verify their sliders/limits. Leave and reopen a world to check handler cleanup.

Changes are prepared in the repository; no Workshop publication or running-world deployment is performed by this refactor.
