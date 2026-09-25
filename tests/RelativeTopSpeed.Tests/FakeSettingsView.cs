using System.Collections.Generic;
using System.Linq;

// Stands in for RtsSettingsWindow, which only compiles against the real RHF library.
namespace RelativeTopSpeed
{
    internal sealed partial class RtsSettingsMenu
    {
        partial void CreateView(ref SettingsView created) { created = new FakeSettingsView(this); }
    }

    internal sealed class FakeSettingsView : SettingsView
    {
        public static FakeSettingsView Last;
        public readonly Dictionary<string, string> Text = new Dictionary<string, string>();
        public readonly Dictionary<string, bool> Flags = new Dictionary<string, bool>();
        public readonly Dictionary<string, List<CruisePoint>> Curves = new Dictionary<string, List<CruisePoint>>();
        public bool Opened, Hud;
        public string Status;

        public FakeSettingsView(RtsSettingsMenu menu) : base(menu)
        {
            Last = this;
            foreach (var field in RtsSettingsMenu.Fields)
            {
                string key = field.Section + "/" + field.Label;
                if (field.IsFlag) Bind(s => Flags[key] = field.GetFlag(s), s => field.SetFlag(s, Flags[key]));
                else if (field.IsCurve) Bind(s => Curves[key] = Copy(field.Grid(s).CruiseCurve), s => field.Grid(s).CruiseCurve = Copy(Curves[key]));
                else Bind(s => Text[key] = field.GetText(s), s => field.SetText(s, Text[key]));
            }
            Bind(s => Hud = menu.ShowHud, s => { });
        }
        public void SetHud(bool value) { Hud = value; Menu.ShowHud = value; }
        public void Apply() { Menu.Apply(); }
        public void Reload() { Menu.Reload(); }
        public void ResetDraft() { Menu.ResetDraft(); }
        public override void Open() { Opened = true; }
        public override void Close() { Opened = false; }
        public override void SetStatus(string text) { Status = text; }

        private static List<CruisePoint> Copy(List<CruisePoint> points)
        {
            return points.Select(p => new CruisePoint { Mass = p.Mass, Speed = p.Speed }).ToList();
        }
    }
}
