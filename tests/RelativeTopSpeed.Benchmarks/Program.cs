using System;
using System.Diagnostics;
using RelativeTopSpeed;
using Sandbox.Game.Entities;
using SEStubs;
using VRage.Game;
using VRageMath;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using System.Collections.Generic;
using Mod = RelativeTopSpeed.RelativeTopSpeed;

Console.WriteLine("Simulated game boundary; measures mod work, not Havok or game frame time.");
Console.WriteLine("scenario,grids,frames,elapsed_ms,bytes_allocated,force_calls,group_queries");
foreach (string scenario in new[] { "drag", "own-thrust", "shared-thrust", "max-speed-query" })
foreach (int count in new[] { 100, 1000, 10000 })
{
    using var game = FakeGame.StartDedicatedServer();
    var settings = Settings.CreateDefault();
    settings.IgnoreGridsWithoutThrust = scenario == "own-thrust" || scenario == "shared-thrust";
    settings.IgnoreGridsWithoutCockpit = false;
    game.Utilities.World[Settings.Filename] = settings.ToString();
    var mod = new Mod();
    mod.Init(new MyObjectBuilder_SessionComponent());
    var grids = new MyCubeGrid[count];
    for (int i = 0; i < count; i++)
    {
        var grid = grids[i] = new MyCubeGrid();
        grid.Physics.LinearVelocity = new Vector3(200, 0, 0);
        grid.Physics.RecordForces = false;
        grid.Physics.Mass = 1000000;
        if (scenario == "own-thrust" || (scenario == "shared-thrust" && i % 10 == 9))
            grid.BlocksCounters[MyObjectBuilderType.ParseBackwardsCompatible("Thrust")] = 1;
        game.Entities.Add(grid);
    }
    if (scenario == "shared-thrust")
    {
        for (int i = 0; i < count; i += 10)
        {
            var group = new List<IMyCubeGrid>();
            for (int j = i; j < i + 10; j++) group.Add(grids[j]);
            foreach (var grid in group) MyAPIGateway.GridGroups.Groups[grid] = group;
        }
    }
    mod.BeforeStart();
    for (int frame = 0; frame < 600; frame++)
    {
        if (scenario == "max-speed-query") foreach (var grid in grids) mod.GetMaxSpeed(grid);
        else mod.Simulate();
    }
    foreach (var grid in grids) grid.Physics.ForceCount = 0;
    MyAPIGateway.GridGroups.Queries = 0;
    var timer = new Stopwatch();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    timer.Start();
    for (int frame = 0; frame < 600; frame++)
    {
        if (scenario == "max-speed-query") foreach (var grid in grids) mod.GetMaxSpeed(grid);
        else mod.Simulate();
    }
    timer.Stop();
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    int forces = 0;
    foreach (var grid in grids) forces += grid.Physics.ForceCount;
    Console.WriteLine($"{scenario},{count},600,{timer.Elapsed.TotalMilliseconds:F3},{allocated},{forces},{MyAPIGateway.GridGroups.Queries}");
    mod.SimulateUnload();
}
