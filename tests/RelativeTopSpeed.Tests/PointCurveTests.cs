using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SENetworkAPI;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class PointCurveTests : TestSession
    {
        private static GridSpeedSettings Points(params float[] values)
        {
            var curve = new GridSpeedSettings { CruiseCurve = new List<CruisePoint>() };
            for (int i = 0; i < values.Length; i += 2)
                curve.CruiseCurve.Add(new CruisePoint { Mass = values[i], Speed = values[i + 1] });
            return curve;
        }

        [Fact]
        public void DocumentedConfigurationLoadsAsWritten()
        {
            string xml = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Example.cfg"));
            var settings = Game.Utilities.SerializeFromXML<Settings>(xml);
            Settings.Validate(ref settings);
            Assert.Equal(5, settings.LargeGrid.CruiseCurve.Count);
            Assert.Equal(3, settings.SmallGrid.CruiseCurve.Count);
            Assert.Equal(225, settings.GetCruiseSpeed(1500000, true));
            Assert.Equal(180, settings.GetCruiseSpeed(300000, false));
            Assert.Equal(400, settings.LargeGrid.MaxBoostSpeed);
            Assert.Equal(500, settings.SmallGrid.MaxBoostSpeed);
            Assert.Equal(600, settings.SpeedLimit);
            Assert.True(settings.EnableGridGroups);
        }

        [Theory]
        [InlineData(true)] [InlineData(false)]
        public void GroupedBoostAndResistanceRoundTrip(bool large)
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            var group = large ? settings.LargeGrid : settings.SmallGrid;
            group.MaxBoostSpeed = 450; group.ResistanceMultiplier = 2.75f;
            Settings.Validate(ref settings);
            string xml = settings.ToString(); var root = XDocument.Parse(xml).Root;
            string name = large ? "LargeGrid" : "SmallGrid";
            Assert.Equal("450", root.Element(name).Element("MaxBoostSpeed").Value);
            Assert.Equal("2.75", root.Element(name).Element("ResistanceMultiplier").Value);
            Assert.Null(root.Element(name + "_MaxBoostSpeed"));
            Assert.Null(root.Element(large ? "LargeGrid_ResistanceMultiplier" : "SmallGrid_ResistanceMultiplyer"));
            foreach (var copyValue in new[] { Game.Utilities.SerializeFromXML<Settings>(xml), Game.Utilities.SerializeFromBinary<Settings>(Game.Utilities.SerializeToBinary(settings)) })
            {
                var copy = copyValue; Settings.Validate(ref copy);
                Assert.Equal(450, large ? copy.LargeGrid.MaxBoostSpeed : copy.SmallGrid.MaxBoostSpeed);
                Assert.Equal(2.75f, large ? copy.LargeGrid.ResistanceMultiplier : copy.SmallGrid.ResistanceMultiplier);
                Assert.Equal(xml, copy.ToString());
            }
        }

        [Theory]
        [InlineData(50, 0, 110, 1)]
        [InlineData(900, -2, 600, 1)]
        [InlineData(float.NaN, float.PositiveInfinity, 110, 1)]
        [InlineData(450, 2.5f, 450, 2.5f)]
        public void GroupedSettingsNormalizeAndRemainStable(float boost, float resistance, float expectedBoost, float expectedResistance)
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            settings.LargeGrid.MaxBoostSpeed = boost; settings.SmallGrid.MaxBoostSpeed = boost;
            settings.LargeGrid.ResistanceMultiplier = resistance; settings.SmallGrid.ResistanceMultiplier = resistance;
            Settings.Validate(ref settings);
            string normalized = settings.ToString();
            Settings.Validate(ref settings);
            Assert.Equal(normalized, settings.ToString());
            foreach (var group in new[] { settings.LargeGrid, settings.SmallGrid })
            {
                Assert.Equal(expectedBoost, group.MaxBoostSpeed);
                Assert.Equal(expectedResistance, group.ResistanceMultiplier);
            }
            Assert.Equal(expectedBoost, settings.LargeGrid.MaxBoostSpeed);
            Assert.Equal(expectedBoost, settings.SmallGrid.MaxBoostSpeed);
            Assert.Equal(expectedResistance, settings.LargeGrid.ResistanceMultiplier);
            Assert.Equal(expectedResistance, settings.SmallGrid.ResistanceMultiplier);
        }

        [Theory]
        [InlineData(-1, 250)] [InlineData(200000, 250)] [InlineData(600000, 250)]
        [InlineData(1500000, 225)] [InlineData(2000000, 200)]
        [InlineData(3500000, 180)] [InlineData(6500000, 150)]
        [InlineData(8000000, 140)] [InlineData(9000000, 140)]
        public void FivePointsInterpolatePlateausAndClampEndpoints(float mass, float expected)
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            settings.LargeGrid = Points(8000000, 140, 1000000, 250, 5000000, 160, 200000, 250, 2000000, 200);
            Settings.Validate(ref settings);
            Assert.Equal(expected, settings.GetCruiseSpeed(mass, true));
            Assert.Equal(new float[] { 200000, 1000000, 2000000, 5000000, 8000000 }, settings.LargeGrid.CruiseCurve.Select(p => p.Mass));
        }

        [Fact]
        public void BothGridTypesCanHaveIndependentPointCountsAndShapes()
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            settings.LargeGrid = Points(0, 40, 10, 100, 20, 30, 30, 30);
            settings.SmallGrid = Points(0, 300, 100, 100);
            Settings.Validate(ref settings);
            Assert.Equal(70, settings.GetCruiseSpeed(5, true));
            Assert.Equal(65, settings.GetCruiseSpeed(15, true));
            Assert.Equal(30, settings.GetCruiseSpeed(25, true));
            Assert.Equal(200, settings.GetCruiseSpeed(50, false));
            Assert.Equal(30, settings.LargeGrid.MinimumCruiseSpeed);
            Assert.Equal(100, settings.LargeGrid.CruiseCurve.Max(p => p.Speed));
        }

        [Theory]
        [InlineData(-1, 100)] [InlineData(float.NaN, 100)] [InlineData(float.PositiveInfinity, 100)]
        [InlineData(1, 0)] [InlineData(1, -1)] [InlineData(1, float.NaN)]
        [InlineData(1, float.NegativeInfinity)] [InlineData(1, float.PositiveInfinity)]
        public void InvalidNumbersAreRejected(float mass, float speed)
        {
            var settings = Settings.CreateDefault(); settings.LargeGrid = Points(mass, speed, 10, 100);
            Assert.Throws<ArgumentException>(() => Settings.Validate(ref settings));
        }

        [Fact]
        public void MissingShortNullAndDuplicatePointsAreRejected()
        {
            foreach (var curve in new[] { new GridSpeedSettings(), Points(), Points(1, 20), Points(1, 20, 1, 30), Points(1, 20, 1, 20) })
            {
                var settings = Settings.CreateDefault(); settings.SmallGrid = curve;
                Assert.Throws<ArgumentException>(() => Settings.Validate(ref settings));
            }
            var invalid = Settings.CreateDefault(); invalid.LargeGrid = Points(0, 10, 100, 20);
            invalid.LargeGrid.CruiseCurve[1] = null;
            Assert.Throws<ArgumentException>(() => Settings.Validate(ref invalid));
        }

        [Fact]
        public void WorldLimitAndBoostCapUseAllPointsIncludingInteriorPeaks()
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 300;
            settings.LargeGrid = Points(0, 10, 100, 400, 200, 20);
            Settings.Validate(ref settings);
            Assert.Equal(300, settings.GetCruiseSpeed(100, true));
            Assert.Equal(300, settings.LargeGrid.MaxBoostSpeed);
            Assert.Equal(10, settings.LargeGrid.MinimumCruiseSpeed);
        }

        [Fact]
        public void ChangingOneGridLeavesTheOtherCurveUnchanged()
        {
            var settings = Settings.CreateDefault(); var expected = new float[101];
            for (int i = 0; i < expected.Length; i++) expected[i] = settings.GetCruiseSpeed(i * 100000, true);
            Settings.Validate(ref settings);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], settings.GetCruiseSpeed(i * 100000, true));
            settings.SmallGrid = Points(0, 100, 100, 50); Settings.Validate(ref settings);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], settings.GetCruiseSpeed(i * 100000, true));
        }

        [Fact]
        public void NewXmlIsCompactAndRoundTripsThroughXmlAndProtobuf()
        {
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            settings.LargeGrid = Points(0, 250, 100, 200, 200, 140, 300, 140);
            Settings.Validate(ref settings);
            string xml = settings.ToString(); var doc = XDocument.Parse(xml);
            Assert.Equal(4, doc.Root.Element("LargeGrid").Element("CruiseCurve").Elements("Point").Count());
            Assert.Null(doc.Root.Element("LargeGrid_MinMass"));
            Assert.Null(doc.Root.Element("LargeGrid_MinCruise"));
            Assert.Null(doc.Root.Element("SmallGrid_MinMass"));
            foreach (var roundTrip in new[] { Game.Utilities.SerializeFromXML<Settings>(xml), Game.Utilities.SerializeFromBinary<Settings>(Game.Utilities.SerializeToBinary(settings)) })
            {
                var copy = roundTrip; Settings.Validate(ref copy);
                Assert.Equal(xml, copy.ToString());
                Assert.Equal(170, copy.GetCruiseSpeed(150, true));
            }
        }

        [Fact]
        public void NewFilesUsePointsAndInvalidReloadPreservesFileAndActiveSettings()
        {
            var fresh = Settings.Load();
            Assert.NotNull(fresh.LargeGrid); Assert.NotNull(fresh.SmallGrid);
            Assert.DoesNotContain("LargeGrid_MinMass", Game.Utilities.World[Settings.Filename]);
            var a = Settings.CreateDefault(); var b = Settings.CreateDefault();
            a.LargeGrid.CruiseCurve[0].Speed = 42;
            Assert.Equal(110, b.LargeGrid.CruiseCurve[0].Speed);
            Start(); var previous = Mod.cfg.Value;
            var invalid = Settings.CreateDefault(); invalid.LargeGrid = Points(1, 100, 1, 50);
            string broken = invalid.ToString(); Game.Utilities.World[Settings.Filename] = broken;
            Game.Utilities.SimulateChat("/rts load");
            Assert.Same(previous, Mod.cfg.Value);
            Assert.Equal(broken, Game.Utilities.World[Settings.Filename]);
            Assert.True(Game.Log.Contains(VRage.Utils.LogSeverity.Warning, "duplicate mass"));
        }

        [Theory] [InlineData(true)] [InlineData(false)]
        public void PointCurvesDriveActualForcesBelowDefaultMinimum(bool large)
        {
            var settings = Unfiltered(false); settings.SpeedLimit = 600;
            if (large) settings.LargeGrid = Points(0, 40, 100000, 20);
            else settings.SmallGrid = Points(0, 40, 100000, 20);
            Start(settings); var grid = Grid(50, 50000, large); Frames(1);
            Assert.Equal(30, grid.Physics.Speed, 3);
            Assert.Equal(30, Mod.GetMaxSpeed(grid));
        }

        [Fact]
        public void ClientReceivesVersionNoticeAndWaitsForLocalPlayer()
        {
            Game.BecomeServer(false); Start();
            var settings = Settings.CreateDefault(); settings.Version = 0;
            Settings.Validate(ref settings);
            var packet = new Command { IsProperty = true, Property = new SyncData
            {
                Id = Mod.cfg.Id, SyncType = SyncType.Post,
                Data = Game.Utilities.SerializeToBinary(settings)
            }};
            var player = Game.Session.LocalHumanPlayer;
            Game.Session.LocalHumanPlayer = null;
            Game.Multiplayer.DeliverSecure(16341, Game.Utilities.SerializeToBinary(packet), 999, true);
            Frames(2);
            Assert.DoesNotContain(Game.ShownMessages, m => m.Text.Contains("Using defaults until"));
            Game.Session.LocalHumanPlayer = player; Frames(2);
            Assert.Single(Game.ShownMessages, m => m.Text.Contains("Using defaults until"));
        }

        [Fact]
        public void MultiplayerReloadUpdatesPointsAndRejectsInvalidReplacement()
        {
            Game.BecomeServer(false); Start();
            var settings = Settings.CreateDefault(); settings.SpeedLimit = 600;
            settings.LargeGrid = Points(0, 250, 100, 200, 200, 140, 300, 140);
            void Receive(Settings value)
            {
                var packet = new Command { IsProperty = true, Property = new SyncData
                {
                    Id = Mod.cfg.Id, SyncType = SyncType.Post,
                    Data = Game.Utilities.SerializeToBinary(value)
                }};
                Game.Multiplayer.DeliverSecure(16341, Game.Utilities.SerializeToBinary(packet), 999, true);
            }
            Receive(settings);
            Assert.Equal(170, Mod.GetCruiseSpeed(150, true));
            var previous = Mod.cfg.Value;
            settings.LargeGrid = Points(1, 20, 1, 30); Receive(settings);
            Assert.Same(previous, Mod.cfg.Value); Assert.Same(previous, Settings.Instance);
            Assert.Equal(170, Mod.GetCruiseSpeed(150, true));
        }

        [Fact]
        public void ThousandsOfPointsMatchLinearReferenceWithoutQueryAllocations()
        {
            var curve = new GridSpeedSettings { CruiseCurve = new List<CruisePoint>() };
            for (int i = 0; i < 4096; i++) curve.CruiseCurve.Add(new CruisePoint { Mass = i * 100, Speed = 1 + (i % 137) });
            curve.Validate("LargeGrid", 600);
            for (int i = 0; i < 4095; i++)
                Assert.Equal((curve.CruiseCurve[i].Speed + curve.CruiseCurve[i + 1].Speed) / 2, curve.Evaluate(i * 100 + 50));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) curve.Evaluate(i * 10 + 50);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
