# Relative Top Speed

Space Engineers mod that derives ship cruise speeds from mass, optionally allowing thrust-dependent boosting above cruise speed.

## Build and test

The game compiles the C# sources under `RelativeTopSpeed/Data/Scripts` when loading the mod. The build below checks those same sources against the installed game assemblies and the MDK mod whitelist. It targets .NET Framework 4.8 with C# 6.

```sh
dotnet build RelativeTopSpeed.csproj -c Release -p:GameBin="/path/to/SpaceEngineers/Bin64"
```

`SE_BIN64` can supply the game directory instead. Common Steam locations are detected automatically. Game DLLs are referenced locally and are not included in this repository.

The C# xUnit suite needs a .NET SDK supporting `net9.0`, but does not need the game:

```sh
dotnet test tests/RelativeTopSpeed.Tests -c Release
dotnet test tests/RelativeTopSpeed.Tests --collect:"XPlat Code Coverage" --settings tests/coverage.runsettings --results-directory TestResults
dotnet run --project tests/RelativeTopSpeed.Benchmarks -c Release
```

Tests compile the actual production files with test-only game services. They use real XML and protobuf serialization. Physics and transport behavior are simulated; the real-assembly/whitelist build and in-game testing provide separate checks. See [validation and performance](docs/validation.md).

## Configuration

`RelativeTopSpeed.cfg` in world storage takes precedence over the local-storage template. `/rts load` reloads the world config; remote players need Space Master, Admin, or Owner permission. `/rts config` displays the active config and `/rts hud` toggles ship statistics.

New configs use a list of mass/speed points for each grid size:

```xml
<LargeGrid>
  <MaxBoostSpeed>400</MaxBoostSpeed>
  <ResistanceMultiplier>1.5</ResistanceMultiplier>
  <CruiseCurve>
    <Point Mass="200000" Speed="250" />
    <Point Mass="1000000" Speed="250" />
    <Point Mass="2000000" Speed="200" />
    <Point Mass="5000000" Speed="160" />
    <Point Mass="8000000" Speed="140" />
  </CruiseCurve>
</LargeGrid>
```

Place this inside `<Settings>` alongside `<Version>1</Version>`. `<SmallGrid>` uses the same format, with its own points. [Complete example config](examples/RelativeTopSpeed.cfg) includes both curves and a 600 m/s world limit.

- Use **two or more points**, with no fixed maximum count. Mass is in kg; speed is in m/s.
- Points are sorted by mass on load and interpolated linearly. Equal speeds create plateaus; rising or falling segments are supported.
- Below the first mass and above the last, the respective endpoint speed is used.
- Masses must be finite, nonnegative and unique. Speeds must be finite and positive. Duplicate masses, empty/single-point lists and invalid numbers reject the configuration.
- Speeds above the world `SpeedLimit` are capped to it when validated. The grid boost cap is raised if necessary to cover the highest cruise point.
- `EnableBoosting=true` allows thrust-dependent speeds above cruise, with resistance and the configured boost cap. `false` enforces the mass-based cruise speed, including coasting after mass/config changes.

### Configuration version

The root `<Settings>` includes `<Version>1</Version>`. `Settings.CurrentVersion` is the schema version; increment it when a release needs administrators to review their configuration. Both grid groups are required and contain `MaxBoostSpeed`, `ResistanceMultiplier` and `CruiseCurve`. The flat settings and Hermite curves have been removed.

If the version is missing or differs from the current version, the mod uses fresh defaults and **preserves the original file**. It logs the mismatch and announces in chat that defaults remain active until the configuration is updated. The notice is also sent to multiplayer clients, including players joining later, and waits until a local player is available before appearing.

Update the file to the current format shown in the [complete example](examples/RelativeTopSpeed.cfg), set `<Version>1</Version>`, then run `/rts load`. Changing the version alone does not migrate flat settings. Server and clients should run the same mod version.

New files contain the current version and default point curves. Malformed current-version files are preserved for correction; failed reloads retain the active settings, while invalid startup files use defaults and log the reason.

## Code layout

| Files | Responsibility |
| --- | --- |
| `Settings.cs`, `Settings.Storage.cs` | Serialized settings, shared validation, storage and world definitions |
| `GridSpeedSettings.cs`, `SpeedMath.cs` | Mass interpolation, drag, excess-speed and acceleration calculations |
| `RelativeTopSpeed.cs` | Session initialization, settings propagation and cleanup |
| `RelativeTopSpeed.Simulation.cs` | Grid tracking, force application and HUD |
| `RelativeTopSpeed.Api.cs`, `RelativeTopSpeed.Commands.cs` | Ship queries and chat commands |
| `RtsApi.cs`, `RtsApiBackend.cs`, `RemoteControl.cs` | Mod API discovery and remote-control integration |
| `SENetworkAPI` | Packet dispatch, serialization, commands and property synchronization |

## Updating an installed mod

To upload to the existing Workshop item **1359618037**, follow [publishing instructions](docs/publishing.md). The inner `RelativeTopSpeed` folder now contains the required `modinfo.sbmi`.

Replace the entire `RelativeTopSpeed/Data/Scripts/RTS` directory, including the new files, and restart the world. Update the server and clients together. The public delegate names, API channel, network channel and serialized field numbers remain unchanged.

The refactor changes several observable behaviors: local reloads notify subscribers; API speed estimates use the same physics mass as enforcement and respect boost caps; non-boosted coasting ships are capped; delayed-physics grids are tracked; private multiplayer sessions synchronize settings; and remote reload permission checks the sender ID supplied by SENetworkAPI. Networking and API handlers are detached when the session ends.

## SENetworkAPI dependency

The bundled dependency is an unchanged copy of [Gauge/SENetworkAPI](https://github.com/Gauge/SENetworkAPI) 2.0.0 at commit `3a83159f63cad90916df351e801c2b8e17031544`, with its MIT license. Its upstream C# test suite is included under `tests/RelativeTopSpeed.Tests/Upstream`. See [source provenance](RelativeTopSpeed/Data/Scripts/RTS/SENetworkAPI/UPSTREAM.md).

Upstream uses legacy message handlers: sender IDs are claimed by the packet, and property transfer direction is not enforced on receipt. The remote reload promotion check is therefore not a reliable authorization boundary against modified clients, and settings packets inherit the same trust limitation. These are documented [upstream limitations](https://github.com/Gauge/SENetworkAPI/blob/3a83159f63cad90916df351e801c2b8e17031544/docs/known-issues.md), covered by its sender-identity tests. Upstream also expects a single space between `/rts` and its command.

When updating this dependency, replace its six C# files and license together, preserve their exact contents, and rerun the upstream and mod integration suites.
