// Test boundary for RHF. Production compiles its real client library against game DLLs.
using System;
using System.Collections.Generic;

namespace VRageMath
{
    public struct Vector2 { public Vector2(float x, float y) { } }
    public struct Color { public static Color White => new Color(); }
}
namespace RichHudFramework.UI.Rendering
{
    public enum TextAlignment { Center }
    public struct GlyphFormat { public GlyphFormat(VRageMath.Color color, TextAlignment alignment, float scale) { } }
}
namespace RichHudFramework.UI
{
    [Flags] public enum ParentAlignments { Bottom = 1, InnerV = 2 }
    public class Label
    {
        public Label(object parent) { }
        public ParentAlignments ParentAlignment;
        public VRageMath.Vector2 Offset;
        public Rendering.GlyphFormat Format;
        public bool Visible;
        public string Text;
    }
}
namespace RichHudFramework.Client
{
    public static class RichHudClient
    {
        public static bool Available, Registered;
        private static Action ready, reset;
        public static void Init(string name, Action onReady, Action onReset)
        { ready = onReady; reset = onReset; if (Available) Register(); }
        public static void Register() { Available = Registered = true; ready?.Invoke(); }
        public static void Clear()
        {
            reset?.Invoke(); ready = reset = null; Available = Registered = false;
            UI.Client.RichHudTerminal.Root = new UI.Client.MenuRoot();
        }
    }
}
namespace RichHudFramework.UI.Client
{
    public static class HudMain { public static object HighDpiRoot = new object(); }
    public class MenuRoot { public bool Enabled; }
    public static class RichHudTerminal { public static MenuRoot Root = new MenuRoot(); }
}
