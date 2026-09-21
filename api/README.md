# Generated mod API

Edit `api/rts.json` to change the public contract. The dependency-free Python 3
generator emits C# 6 into `RelativeTopSpeed/RtsApi.cs` and
`RelativeTopSpeed/RtsApiBackend.cs`. Both outputs are committed to Git.

```sh
python3 tools/api/generate.py
python3 tools/api/generate.py --check
python3 -m unittest discover -s tools/api -p 'test_*.py'
dotnet test tests/RelativeTopSpeed.Tests -c Release
```

The production project runs generation before building. Python 3 must be on PATH;
if your interpreter is named `python`, pass `-p:ApiGeneratorPython=python` to
`dotnet build`. IDE design-time builds skip generation. The test project compiles
the committed outputs; CI checks freshness before running tests so stale files
cannot be silently regenerated and accepted.

Each method declares its public `name`, `returns`, ordered `parameters` (each with
`type` and `name`), and documentation `summary`. Optional `providerMethod` maps the
public name to a differently named implementation. For example,
`GetAccelerationByDirection` maps to the existing `GetAccelerationsByDirection`.
Implement method bodies in the provider yourself; generation only creates the
bridge. Overloads are not supported because endpoint dictionary keys are names.

Supported signature types are `float`, `double`, `int`, `long`, `bool`, `string`,
`IMyCubeGrid`, and arrays of those types; returns also support `void`. Parameters
are passed by value, with at most 16 parameters. Extend the generator explicitly
when adding another shared framework/game type. Custom mod-defined types are not
appropriate shared delegate signatures across separately compiled assemblies.
Use ordinary C# identifiers and avoid language keywords for method/class names.

Consumers still copy just `RtsApi.cs`; they do not install Python or any runtime
dependency. Channel 2772681332, the `ApiEndpointRequest` handshake, public method
names/signatures, and direct delegate calls remain compatible. All required
delegates are validated before any are assigned. Load/unload behavior lives in
`tools/api/templates`; edit those templates rather than generated files when
changing discovery or lifecycle behavior.

Adding a required method means new clients require an endpoint providing it.
Generation does not add version negotiation or make breaking contract changes
backward compatible. Existing clients continue to ignore additional methods.
