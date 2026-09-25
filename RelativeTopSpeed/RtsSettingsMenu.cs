using System;
using System.Collections.Generic;
using System.Globalization;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using VRageMath;

namespace RelativeTopSpeed
{
    // One editable world setting: a flag, a number edited as text, or a grid's cruise curve.
    internal sealed class SettingField
    {
        public string Section, Label, Tip;
        public Func<Settings, bool> GetFlag;
        public Action<Settings, bool> SetFlag;
        public Func<Settings, string> GetText;
        public Action<Settings, string> SetText;
        public Func<Settings, GridSpeedSettings> Grid;
        public bool IsFlag { get { return GetFlag != null; } }
        public bool IsCurve { get { return Grid != null; } }
    }

    // Presents the settings. The view owns its controls; the menu owns the draft.
    internal abstract class SettingsView
    {
        protected readonly RtsSettingsMenu Menu;
        private readonly List<Action<Settings>> read = new List<Action<Settings>>();
        private readonly List<Action<Settings>> write = new List<Action<Settings>>();

        protected SettingsView(RtsSettingsMenu menu) { Menu = menu; }
        public abstract void Open();
        public abstract void Close();
        public abstract void SetStatus(string text);
        public void Read(Settings settings) { foreach (var load in read) load(settings); }
        public void Write(Settings settings) { foreach (var save in write) save(settings); }
        protected void Bind(Action<Settings> load, Action<Settings> save) { read.Add(load); write.Add(save); }
    }

    internal sealed partial class RtsSettingsMenu
    {
        public const string HudTip = "Your own HUD only; takes effect immediately.";
        public static readonly List<SettingField> Fields = CreateFields();

        private readonly RelativeTopSpeed mod;
        private SettingsView view;
        private string baseline;
        private string pendingXml;
        private bool closed;
        private Label hud;

        public RtsSettingsMenu(RelativeTopSpeed mod) { this.mod = mod; }
        internal bool Refreshing { get; private set; }
        internal bool ShowHud { get { return mod.ShowHud; } set { if (!Refreshing) mod.ShowHud = value; } }

        public void Load()
        {
            RelativeTopSpeed.SettingsChanged += OnSettingsChanged;
            RichHudClient.Init("Relative Top Speed", Build, Reset);
        }
        private void OnSettingsChanged(Settings settings)
        {
            if (view != null && pendingXml != null && settings.ToString() == pendingXml)
            {
                pendingXml = null;
                Refresh();
                view.SetStatus("Settings saved and applied by the server.");
            }
        }
        private void Reset()
        {
            if (hud != null) hud.Visible = false;
            hud = null; view = null;
        }
        public void Close()
        {
            closed = true;
            RelativeTopSpeed.SettingsChanged -= OnSettingsChanged;
            view?.Close();
            if (RichHudClient.Registered) RichHudTerminal.Root.Enabled = false;
            Reset();
            // RHF's own session component owns unregistering its client on unload.
        }
        public void Open()
        {
            if (!closed && view != null) { Refresh(); view.Open(); }
            else MyAPIGateway.Utilities.ShowMessage("Relative Top Speed", "The settings window needs the optional Rich HUD Master mod. /rts config and /rts load remain available.");
        }
        public bool SetHudText(string text)
        {
            if (closed || !RichHudClient.Registered || hud == null) return false;
            hud.Visible = text != null;
            if (text != null) hud.Text = text;
            return true;
        }
        private void Build()
        {
            if (closed || view != null) return;
            hud = new Label(HudMain.HighDpiRoot)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                Offset = new Vector2(0, 160),
                Format = new GlyphFormat(Color.White, TextAlignment.Center, 0.85f),
                Visible = false
            };
            RichHudTerminal.Root.Enabled = true;
            CreateView(ref view);
            Refresh();
        }
        // Implemented by RtsSettingsWindow.cs against the real RHF library, and by a test double.
        partial void CreateView(ref SettingsView created);

        internal void Reload() { Refresh(); }
        internal void ResetDraft()
        {
            if (view == null) return;
            SetDraft(Settings.CreateDefault());
            view.SetStatus("Draft reset to defaults. Apply and save to use them.");
        }
        private void SetDraft(Settings settings)
        {
            Refreshing = true;
            try { view.Read(settings); }
            finally { Refreshing = false; }
        }
        private void Refresh()
        {
            if (view == null) return;
            baseline = mod.cfg.Value.ToString();
            SetDraft(mod.cfg.Value);
            view.SetStatus(mod.CanEditSettings ? "Ready. Apply and save updates the running world." : "Read only: world edits require administrator access.");
        }
        internal void Apply()
        {
            if (view == null) return;
            try
            {
                var draft = MyAPIGateway.Utilities.SerializeFromXML<Settings>(baseline);
                view.Write(draft);
                Settings.Validate(ref draft);
                pendingXml = draft.ToString();
                string result = mod.SubmitSettings(baseline, draft);
                // Adopt the authoritative result on a host, retaining draft edits on rejection.
                if (MyAPIGateway.Multiplayer.IsServer && result.StartsWith("Settings saved")) Refresh();
                view.SetStatus(result);
            }
            catch (Exception error) { view.SetStatus("Not applied: " + error.Message); }
        }

        private static List<SettingField> CreateFields()
        {
            var fields = new List<SettingField>
            {
                Flag("World", "Enable grid groups", "Grids joined by connectors, rotors, pistons or landing gear share one cruise speed, taken from their total mass.",
                    s => s.EnableGridGroups, (s, v) => s.EnableGridGroups = v),
                Flag("World", "Allow boosting", "On: thrust can push past cruise speed up to the boost ceiling, and resistance pulls the ship back. Off: cruise speed is a hard cap.",
                    s => s.EnableBoosting, (s, v) => s.EnableBoosting = v),
                Flag("World", "Only grids with thrust", "Grids with no thrusters anywhere in their group are left alone.",
                    s => s.IgnoreGridsWithoutThrust, (s, v) => s.IgnoreGridsWithoutThrust = v),
                Flag("World", "Only grids with cockpit", "Grids with no cockpit anywhere in their group are left alone.",
                    s => s.IgnoreGridsWithoutCockpit, (s, v) => s.IgnoreGridsWithoutCockpit = v),
                Number("Limits", "World speed limit (m/s)", "No ship goes faster than this, whatever its mass or boost.",
                    s => s.SpeedLimit, (s, v) => s.SpeedLimit = v),
                Number("Limits", "Remote control limit (m/s)", "Speed limit for remote-controlled and autopilot ships.",
                    s => s.RemoteControlSpeedLimit, (s, v) => s.RemoteControlSpeedLimit = v),
                Number("Limits", "Parachute height (m)", "Height at which drop-container parachutes open.",
                    s => s.ParachuteDeployHeight, (s, v) => s.ParachuteDeployHeight = v),
            };
            AddGrid(fields, "Large grids", s => s.LargeGrid);
            AddGrid(fields, "Small grids", s => s.SmallGrid);
            return fields;
        }
        private static void AddGrid(List<SettingField> fields, string section, Func<Settings, GridSpeedSettings> grid)
        {
            fields.Add(Number(section, "Boost ceiling (m/s)", "The fastest a boosting ship may travel. Never below cruise, never above the world limit.",
                s => grid(s).MaxBoostSpeed, (s, v) => grid(s).MaxBoostSpeed = v));
            fields.Add(Number(section, "Resistance", "How firmly a ship is pulled back to cruise speed. Higher is firmer.",
                s => grid(s).ResistanceMultiplier, (s, v) => grid(s).ResistanceMultiplier = v));
            fields.Add(new SettingField
            {
                Section = section, Label = "Cruise curve", Grid = grid,
                Tip = "Cruise speed by mass. Speeds are interpolated in straight lines between points and held flat beyond the ends. "
                    + SettingsEditor.MinCurvePoints + " to " + SettingsEditor.MaxCurvePoints + " points."
            });
        }
        private static SettingField Flag(string section, string label, string tip, Func<Settings, bool> get, Action<Settings, bool> set)
        {
            return new SettingField { Section = section, Label = label, Tip = tip, GetFlag = get, SetFlag = set };
        }
        private static SettingField Number(string section, string label, string tip, Func<Settings, float> get, Action<Settings, float> set)
        {
            return new SettingField
            {
                Section = section, Label = label, Tip = tip,
                GetText = s => SettingsEditor.FormatNumber(get(s)),
                SetText = (s, v) => set(s, SettingsEditor.ParseNumber(v))
            };
        }
    }
}
