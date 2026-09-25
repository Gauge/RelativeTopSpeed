// Test-only game boundaries. Physics records/applies ideal impulses; it does not
// emulate Havok. Real interface compatibility is checked by the mod project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace VRageMath
{
    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vector3 Zero => new Vector3();
        public float LengthSquared() => X * X + Y * Y + Z * Z;
        public float Length() => (float)Math.Sqrt(LengthSquared());
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.X * f, a.Y * f, a.Z * f);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
    public static class Base6Directions
    {
        public enum Direction { Forward, Backward, Left, Right, Up, Down }
        public static Direction GetDirection(Vector3 vector) => vector.Z < 0 ? Direction.Forward : vector.Z > 0 ? Direction.Backward : vector.X < 0 ? Direction.Left : vector.X > 0 ? Direction.Right : vector.Y > 0 ? Direction.Up : Direction.Down;
    }
}
namespace VRage.ObjectBuilders
{
    public struct MyObjectBuilderType : IEquatable<MyObjectBuilderType>
    {
        private string name;
        public override string ToString() => name;
        public bool Equals(MyObjectBuilderType other) => name == other.name;
        public override bool Equals(object other) => other is MyObjectBuilderType type && Equals(type);
        public override int GetHashCode() => name == null ? 0 : StringComparer.Ordinal.GetHashCode(name);
        public static MyObjectBuilderType ParseBackwardsCompatible(string name) => new MyObjectBuilderType { name = name };
    }
}
namespace VRage.Game
{
    public enum MyCubeSize { Large, Small }
    public class MyObjectBuilder_CubeBlock { }
    public class MyObjectBuilder_CubeGrid { public List<MyObjectBuilder_CubeBlock> CubeBlocks = new List<MyObjectBuilder_CubeBlock>(); }
}
namespace Sandbox.Common.ObjectBuilders
{
    public class MyObjectBuilder_RemoteControl { }
    public class MyObjectBuilder_Parachute : VRage.Game.MyObjectBuilder_CubeBlock { public float DeployHeight; }
}
namespace VRage.Collections
{
    public class DictionaryReader<K, V> : Dictionary<K, V> { }
}
namespace Sandbox.Definitions
{
    public class EnvironmentDefinition { public float LargeShipMaxSpeed, SmallShipMaxSpeed; }
    public class Prefab { public VRage.Game.MyObjectBuilder_CubeGrid[] CubeGrids; }
    public class MyDropContainerDefinition { public Prefab Prefab; }
    public class MyDefinitionManager
    {
        public static MyDefinitionManager Static = new MyDefinitionManager();
        public EnvironmentDefinition EnvironmentDefinition = new EnvironmentDefinition();
        public VRage.Collections.DictionaryReader<string, MyDropContainerDefinition> Drops = new VRage.Collections.DictionaryReader<string, MyDropContainerDefinition>();
        public VRage.Collections.DictionaryReader<string, MyDropContainerDefinition> GetDropContainerDefinitions() => Drops;
    }
}
namespace VRage.Game.Components
{
    public enum MyPhysicsForceType { APPLY_WORLD_FORCE, APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE }
    public class FakePhysics
    {
        public float Mass = 10000;
        public Vector3 LinearVelocity, LinearAcceleration;
        public Vector3D CenterOfMassWorld;
        public float Speed => LinearVelocity.Length();
        public List<(MyPhysicsForceType Type, Vector3 Force, float? Limit)> Forces = new List<(MyPhysicsForceType, Vector3, float?)>();
        public bool RecordForces = true;
        public int ForceCount;
        public void AddForce(MyPhysicsForceType type, Vector3 force, Vector3D position, Vector3? torque, float? maxSpeed = null)
        {
            ForceCount++;
            if (RecordForces) Forces.Add((type, force, maxSpeed));
            if (type == MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE)
                LinearVelocity += force * (1 / Mass);
        }
    }
}
namespace VRage.Game.ModAPI
{
    public enum GridLinkTypeEnum { Mechanical, Physical }
    public enum MyPromoteLevel { None, Scripter, Moderator, SpaceMaster, Admin, Owner }
    public interface IMyCubeGrid : IMyEntity
    {
        VRage.Game.Components.FakePhysics Physics { get; }
        VRage.Game.MyCubeSize GridSizeEnum { get; }
        bool IsStatic { get; }
        bool Closed { get; }
    }
    public interface IMyCubeBlock { IMyCubeGrid CubeGrid { get; } }
    public interface IMySlimBlock { object FatBlock { get; } }
    public partial interface IMyPlayer { SEStubs.FakeController Controller { get; } }
    public partial interface IMySession { MyPromoteLevel GetUserPromoteLevel(ulong id); }
    public partial interface IMyUtilities
    {
        string SerializeToXML<T>(T value);
        T SerializeFromXML<T>(string value);
        bool FileExistsInWorldStorage(string file, Type type);
        bool FileExistsInLocalStorage(string file, Type type);
        TextReader ReadFileInWorldStorage(string file, Type type);
        TextReader ReadFileInLocalStorage(string file, Type type);
        TextWriter WriteFileInWorldStorage(string file, Type type);
        TextWriter WriteFileInLocalStorage(string file, Type type);
        void RegisterMessageHandler(long channel, Action<object> callback);
        void UnregisterMessageHandler(long channel, Action<object> callback);
        void SendModMessage(long channel, object value);
        void ShowNotification(string text, int time);
        void ShowMissionScreen(string title, string subtitle, string prefix, string text);
    }
}
namespace VRage.ModAPI
{
    public partial interface IMyEntities
    {
        event Action<IMyEntity> OnEntityAdd;
        event Action<IMyEntity> OnEntityRemove;
        void GetEntities(HashSet<IMyEntity> result);
    }
}
namespace Sandbox.ModAPI
{
    public interface IMyThrust { float MaxThrust { get; } Vector3 GridThrustDirection { get; } }
    public interface IMyRemoteControl : IMyEntity { float SpeedLimit { get; set; } }
    public static partial class MyAPIGateway
    {
        public static SEStubs.FakeGridGroups GridGroups;
        public static SEStubs.FakeTerminalControls TerminalControls;
    }
}
namespace Sandbox.ModAPI.Interfaces.Terminal
{
    public interface IMyTerminalControl { string Id { get; } }
    public interface IMyTerminalControlSlider : IMyTerminalControl { void SetLimits(float min, float max); }
}
namespace Sandbox.Game.Entities
{
    public partial class MyCubeGrid
    {
        public VRage.Game.Components.FakePhysics Physics { get; set; } = new VRage.Game.Components.FakePhysics();
        public VRage.Game.MyCubeSize GridSizeEnum { get; set; }
        public bool IsStatic { get; set; }
        public event Action<MyCubeGrid, bool> OnStaticChanged;
        public int StaticSubscribers => OnStaticChanged?.GetInvocationList().Length ?? 0;
        public Dictionary<VRage.ObjectBuilders.MyObjectBuilderType, int> BlocksCounters = new Dictionary<VRage.ObjectBuilders.MyObjectBuilderType, int>();
        public List<IMySlimBlock> CubeBlocks = new List<IMySlimBlock>();
        public void SetStatic(bool value) { IsStatic = value; OnStaticChanged?.Invoke(this, value); }
    }
}
namespace SEStubs
{
    public class FakeController { public object ControlledEntity { get; set; } }
    public partial class FakePlayer { public FakeController Controller { get; set; } = new FakeController(); }
    public partial class FakeSession
    {
        public Dictionary<ulong, MyPromoteLevel> Promotions = new Dictionary<ulong, MyPromoteLevel>();
        public MyPromoteLevel GetUserPromoteLevel(ulong id) => Promotions.TryGetValue(id, out var level) ? level : MyPromoteLevel.None;
    }
    public partial class FakeEntities
    {
        public event Action<IMyEntity> OnEntityAdd;
        public event Action<IMyEntity> OnEntityRemove;
        public void GetEntities(HashSet<IMyEntity> result) { foreach (var entity in Registered.Values) result.Add(entity); }
    }
    public partial class FakeMultiplayer
    {
        public Dictionary<ushort, Action<ushort, byte[], ulong, bool>> Secure = new Dictionary<ushort, Action<ushort, byte[], ulong, bool>>();
        public void DeliverSecure(ushort channel, byte[] data, ulong sender, bool fromServer)
        { if (Secure.TryGetValue(channel, out var handler)) handler(channel, data, sender, fromServer); }
    }
    public class FakeGridGroups
    {
        public Dictionary<IMyCubeGrid, List<IMyCubeGrid>> Groups = new Dictionary<IMyCubeGrid, List<IMyCubeGrid>>();
        public int Queries;
        public void GetGroup(IMyCubeGrid grid, GridLinkTypeEnum type, List<IMyCubeGrid> result)
        { Queries++; if (Groups.TryGetValue(grid, out var group)) result.AddRange(group); else result.Add(grid); }
    }
    public class FakeTerminalControls
    {
        public List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl> Controls = new List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl>();
        public void GetControls<T>(out List<Sandbox.ModAPI.Interfaces.Terminal.IMyTerminalControl> controls) { controls = Controls; }
    }
    public partial class FakeUtilities
    {
        public Dictionary<string, string> World = new Dictionary<string, string>(), Local = new Dictionary<string, string>();
        public bool FailWrite;
        public string SerializeToXML<T>(T value) { using var writer = new StringWriter(); new XmlSerializer(typeof(T)).Serialize(writer, value); return writer.ToString(); }
        public T SerializeFromXML<T>(string value) { using var reader = new StringReader(value); return (T)new XmlSerializer(typeof(T)).Deserialize(reader); }
        public bool FileExistsInWorldStorage(string file, Type type) => World.ContainsKey(file);
        public bool FileExistsInLocalStorage(string file, Type type) => Local.ContainsKey(file);
        public TextReader ReadFileInWorldStorage(string file, Type type) => new StringReader(World[file]);
        public TextReader ReadFileInLocalStorage(string file, Type type) => new StringReader(Local[file]);
        public TextWriter WriteFileInWorldStorage(string file, Type type) => Writer(World, file);
        public TextWriter WriteFileInLocalStorage(string file, Type type) => Writer(Local, file);
        private TextWriter Writer(Dictionary<string, string> files, string name)
        { if (FailWrite) throw new IOException("Injected storage failure"); return new SaveWriter(text => files[name] = text); }
        private class SaveWriter : StringWriter
        {
            private Action<string> save;
            public SaveWriter(Action<string> save) { this.save = save; }
            protected override void Dispose(bool disposing) { if (disposing) save(ToString()); base.Dispose(disposing); }
        }
        public Dictionary<long, List<Action<object>>> ModHandlers = new Dictionary<long, List<Action<object>>>();
        public void RegisterMessageHandler(long channel, Action<object> callback)
        { if (!ModHandlers.ContainsKey(channel)) ModHandlers[channel] = new List<Action<object>>(); ModHandlers[channel].Add(callback); }
        public void UnregisterMessageHandler(long channel, Action<object> callback)
        { if (ModHandlers.TryGetValue(channel, out var handlers)) handlers.Remove(callback); }
        public void SendModMessage(long channel, object value)
        { if (ModHandlers.TryGetValue(channel, out var handlers)) foreach (var callback in handlers.ToArray()) callback(value); }
        public List<string> Notifications = new List<string>();
        public void ShowNotification(string text, int time) => Notifications.Add(text);
        public string Mission;
        public void ShowMissionScreen(string title, string subtitle, string prefix, string text) { Mission = text; }
    }
}
