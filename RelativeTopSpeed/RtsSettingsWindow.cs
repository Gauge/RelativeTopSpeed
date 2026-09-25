using System;
using System.Collections.Generic;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using VRageMath;

// Uses RHF HUD elements directly, so it is excluded from the stubbed test build.
namespace RelativeTopSpeed
{
    internal sealed partial class RtsSettingsMenu
    {
        partial void CreateView(ref SettingsView created) { created = new WindowSettingsView(this); }
    }

    internal sealed class WindowSettingsView : SettingsView
    {
        private readonly RtsSettingsWindow window;

        public WindowSettingsView(RtsSettingsMenu menu) : base(menu)
        {
            window = new RtsSettingsWindow(HudMain.HighDpiRoot, menu, Bind);
            // F2 keeps a single entry that opens the window.
            var open = new TerminalButton { Name = "Open settings window", ToolTip = "Closes F2 and opens the Relative Top Speed settings window." };
            open.ControlChanged += (sender, args) => menu.Open();
            var tile = new ControlTile();
            tile.Add(open);
            tile.Add(new TerminalLabel { Name = "or type /rts menu" });
            var category = new ControlCategory { HeaderText = "Relative Top Speed", SubheaderText = "Settings open in their own window" };
            category.Add(tile);
            var page = new ControlPage { Name = "Settings" };
            page.Add(category);
            RichHudTerminal.Root.Add(page);
        }
        public override void Open()
        {
            if (RichHudTerminal.Open) RichHudTerminal.CloseMenu();
            window.Show();
        }
        public override void Close() { window.Hide(); }
        public override void SetStatus(string text) { window.SetStatus(text); }
    }

    internal sealed class RtsSettingsWindow : WindowBase
    {
        private const float LabelWidth = 250f, NumberWidth = 150f, SpeedWidth = 110f, RowHeight = 30f, Gap = 10f;
        private const float ButtonWidth = 180f, FooterHeight = 74f;
        private static readonly Color Body = new Color(24, 30, 36, 240), Edge = new Color(72, 86, 98);
        private static readonly Color FieldColor = new Color(38, 48, 56), ButtonColor = new Color(38, 48, 56);
        private static readonly GlyphFormat SectionFormat = new GlyphFormat(new Color(238, 244, 248), TextAlignment.Left, 1.1f);
        private static readonly GlyphFormat NameFormat = new GlyphFormat(new Color(210, 224, 232), TextAlignment.Left, 1f);
        private static readonly GlyphFormat DimFormat = new GlyphFormat(new Color(140, 156, 168), TextAlignment.Left, 0.9f);
        private static readonly GlyphFormat InputFormat = new GlyphFormat(new Color(214, 228, 236), TextAlignment.Left, 1f);
        private static readonly GlyphFormat ButtonFormat = new GlyphFormat(new Color(226, 236, 242), TextAlignment.Center, 0.95f);

        private readonly RtsSettingsMenu menu;
        private readonly ScrollBox rows;
        private readonly HudChain buttons;
        private readonly Label status;
        private readonly List<TextField> fields = new List<TextField>();

        public RtsSettingsWindow(HudParentBase parent, RtsSettingsMenu menu, Action<Action<Settings>, Action<Settings>> bind) : base(parent)
        {
            this.menu = menu;
            HeaderText = "Relative Top Speed";
            HeaderBuilder.Format = new GlyphFormat(new Color(232, 240, 246), TextAlignment.Center, 1.1f);
            BodyColor = Body;
            BorderColor = Edge;
            Size = new Vector2(3 * ButtonWidth + 4 * Gap + 60f, 760f);
            MinimumSize = new Vector2(3 * ButtonWidth + 4 * Gap, 360f);

            var close = Button("Close", Hide, 80f, header);
            close.Height = 24f;
            close.ParentAlignment = ParentAlignments.InnerRight | ParentAlignments.InnerV;
            close.Offset = new Vector2(-8f, 0f);
            close.ZOffset = 2;

            rows = new ScrollBox(true, body)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.AlignMembersStart,
                Color = new Color(0, 0, 0, 0), Spacing = 4f,
            };
            string section = null;
            foreach (var setting in RtsSettingsMenu.Fields)
            {
                if (setting.Section != section) rows.Add(Section(section = setting.Section));
                if (setting.IsCurve) new CurveEditor(this, setting, bind);
                else rows.Add(Row(setting, bind));
            }
            rows.Add(Section("Your display"));
            var hudBox = Box(RtsSettingsMenu.HudTip);
            bind(s => hudBox.Value = menu.ShowHud, s => { });
            hudBox.ValueChanged += (sender, args) => menu.ShowHud = hudBox.Value;
            rows.Add(Row("Show speed HUD", hudBox));

            buttons = new HudChain(false, body)
            {
                ParentAlignment = ParentAlignments.InnerBottomLeft,
                SizingMode = HudChainSizingModes.AlignMembersStart,
                Spacing = Gap, Height = 30f,
            };
            buttons.Add(Button("Apply and save", menu.Apply), 0f);
            buttons.Add(Button("Reload", menu.Reload), 0f);
            buttons.Add(Button("Reset to defaults", menu.ResetDraft), 0f);
            status = new Label(body)
            {
                ParentAlignment = ParentAlignments.InnerBottomLeft,
                AutoResize = false, VertCenterText = true, Format = DimFormat, Height = 26f,
                BuilderMode = TextBuilderModes.Wrapped,
            };
            Visible = false;
        }

        protected override void Layout()
        {
            base.Layout();
            float width = Math.Max(body.Width - 2 * Gap, 1f);
            rows.Width = width;
            rows.Height = Math.Max(body.Height - FooterHeight - 2 * Gap, 1f);
            rows.Offset = new Vector2(Gap, -Gap);
            buttons.Offset = new Vector2(Gap, Gap + status.Height + 4f);
            status.Width = width;
            status.Offset = new Vector2(Gap, Gap);
        }

        protected override void HandleInput(Vector2 cursorPos)
        {
            base.HandleInput(cursorPos);
            if (SharedBinds.Escape.IsNewPressed && !fields.Exists(f => f.InputOpen)) Hide();
        }

        public void Show()
        {
            Visible = true;
            HudMain.EnableCursor = true;
            GetWindowFocus();
        }
        public void Hide()
        {
            foreach (var field in fields) { field.FocusHandler.ReleaseFocus(); field.CloseInput(); }
            if (!Visible) return;
            Visible = false;
            HudMain.EnableCursor = false;
        }
        public void SetStatus(string text) { status.Text = text ?? ""; }

        private HudElementBase Row(SettingField setting, Action<Action<Settings>, Action<Settings>> bind)
        {
            if (setting.IsFlag)
            {
                var box = Box(setting.Tip);
                bind(s => box.Value = setting.GetFlag(s), s => setting.SetFlag(s, box.Value));
                return Row(setting.Label, box);
            }
            var field = NumberField(NumberWidth, setting.Tip);
            bind(s => field.Text = setting.GetText(s), s => setting.SetText(s, field.TextBoard.ToString()));
            return Row(setting.Label, field);
        }
        private TextField NumberField(float width, string tip)
        {
            var field = new TextField { Width = width, Height = 26f, Color = FieldColor, BorderColor = Edge, Format = InputFormat };
            field.MouseInput.ToolTip = tip;
            field.CharFilterFunc = c => (c >= '0' && c <= '9') || c == '.';
            fields.Add(field);
            return field;
        }
        private static BorderedCheckBox Box(string tip)
        {
            var box = new BorderedCheckBox { Size = new Vector2(26f, 26f), BorderColor = Edge };
            box.MouseInput.ToolTip = tip;
            return box;
        }
        private static Label Text(string text, float width, GlyphFormat format)
        {
            return new Label { AutoResize = false, VertCenterText = true, Format = format, Text = text, Width = width, Height = RowHeight };
        }
        private static Label Section(string text)
        {
            var label = Text(text, LabelWidth, SectionFormat);
            label.Height = 36f;
            return label;
        }
        private static HudChain Row(string name, params HudElementBase[] controls)
        {
            var row = new HudChain(false) { Height = RowHeight, Spacing = Gap, SizingMode = HudChainSizingModes.FitMembersOffAxis };
            row.Add(Text(name, LabelWidth, NameFormat), 0f);
            foreach (var control in controls) row.Add(control, 0f);
            return row;
        }
        // RHF's default button padding leaves almost no room for text, so it is removed here.
        private static BorderedButton Button(string text, Action action, float width = ButtonWidth, HudParentBase parent = null)
        {
            var button = new BorderedButton(parent)
            {
                Text = text, Format = ButtonFormat, Padding = Vector2.Zero, TextPadding = new Vector2(8f, 0f),
                Size = new Vector2(width, 30f), Color = ButtonColor, BorderColor = Edge,
            };
            button.MouseInput.LeftClicked += (sender, args) => action();
            return button;
        }

        // Rows of mass/speed fields inserted into the window's list above an add/remove footer.
        private sealed class CurveEditor
        {
            private readonly RtsSettingsWindow window;
            private readonly string section;
            private readonly ScrollBoxEntry footer;
            private readonly BorderedButton add, remove;
            private readonly Label count;
            private readonly List<ScrollBoxEntry> entries = new List<ScrollBoxEntry>();
            private readonly List<TextField> masses = new List<TextField>(), speeds = new List<TextField>();

            public CurveEditor(RtsSettingsWindow window, SettingField setting, Action<Action<Settings>, Action<Settings>> bind)
            {
                this.window = window;
                section = setting.Section;
                var heading = Row(setting.Label, Text("Mass (kg)", NumberWidth, DimFormat), Text("Speed (m/s)", SpeedWidth, DimFormat));
                window.rows.Add(heading);
                // Same column widths as the point rows, so the footer never overflows the list.
                add = Button("+ Add point", AddPoint, NumberWidth);
                remove = Button("- Remove", RemovePoint, SpeedWidth);
                count = Text("", LabelWidth, DimFormat);
                var buttons = new HudChain(false) { Height = RowHeight, Spacing = Gap, SizingMode = HudChainSizingModes.FitMembersOffAxis };
                buttons.Add(count, 0f);
                buttons.Add(add, 0f);
                buttons.Add(remove, 0f);
                footer = new ScrollBoxEntry();
                footer.SetElement(buttons);
                window.rows.Add(footer);
                bind(Read(setting), Write(setting));
            }
            private Action<Settings> Read(SettingField setting)
            {
                return s =>
                {
                    var points = setting.Grid(s).CruiseCurve;
                    while (entries.Count > points.Count) RemovePoint();
                    while (entries.Count < points.Count) Insert();
                    for (int i = 0; i < points.Count; i++)
                    {
                        masses[i].Text = SettingsEditor.FormatNumber(points[i].Mass);
                        speeds[i].Text = SettingsEditor.FormatNumber(points[i].Speed);
                    }
                };
            }
            private Action<Settings> Write(SettingField setting)
            {
                return s =>
                {
                    var points = new List<CruisePoint>();
                    for (int i = 0; i < entries.Count; i++)
                        points.Add(new CruisePoint { Mass = Parse(masses[i], i, "mass"), Speed = Parse(speeds[i], i, "speed") });
                    setting.Grid(s).CruiseCurve = points;
                };
            }
            private float Parse(TextField field, int index, string what)
            {
                try { return SettingsEditor.ParseNumber(field.TextBoard.ToString()); }
                catch (ArgumentException error) { throw new ArgumentException(section + " point " + (index + 1) + " " + what + ": " + error.Message); }
            }
            private void AddPoint()
            {
                if (entries.Count >= SettingsEditor.MaxCurvePoints) return;
                var points = new List<CruisePoint>();
                for (int i = 0; i < entries.Count; i++)
                {
                    float mass, speed;
                    if (!float.TryParse(masses[i].TextBoard.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mass)) mass = 0;
                    if (!float.TryParse(speeds[i].TextBoard.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out speed)) speed = 100;
                    points.Add(new CruisePoint { Mass = mass, Speed = speed });
                }
                var next = SettingsEditor.NextPoint(points);
                Insert();
                masses[masses.Count - 1].Text = SettingsEditor.FormatNumber(next.Mass);
                speeds[speeds.Count - 1].Text = SettingsEditor.FormatNumber(next.Speed);
            }
            private void Insert()
            {
                var mass = window.NumberField(NumberWidth, "Mass in kg at which this cruise speed applies.");
                var speed = window.NumberField(SpeedWidth, "Cruise speed in m/s at this mass.");
                var entry = new ScrollBoxEntry();
                entry.SetElement(Row("   Point " + (entries.Count + 1), mass, speed));
                window.rows.Insert(IndexOf(footer), entry);
                entries.Add(entry); masses.Add(mass); speeds.Add(speed);
                Update();
            }
            private void RemovePoint()
            {
                if (entries.Count <= SettingsEditor.MinCurvePoints && !window.menu.Refreshing) return;
                if (entries.Count == 0) return;
                int last = entries.Count - 1;
                masses[last].CloseInput(); speeds[last].CloseInput();
                window.fields.Remove(masses[last]); window.fields.Remove(speeds[last]);
                window.rows.Remove(entries[last]);
                entries.RemoveAt(last); masses.RemoveAt(last); speeds.RemoveAt(last);
                Update();
            }
            private int IndexOf(ScrollBoxEntry target)
            {
                var collection = window.rows.Collection;
                for (int i = 0; i < collection.Count; i++) if (collection[i] == target) return i;
                return collection.Count;
            }
            private void Update()
            {
                add.Visible = entries.Count < SettingsEditor.MaxCurvePoints;
                remove.Visible = entries.Count > SettingsEditor.MinCurvePoints;
                count.Text = "   " + entries.Count + " / " + SettingsEditor.MaxCurvePoints + " points";
            }
        }
    }
}
