using System;
using Xunit;

namespace RelativeTopSpeed.Tests
{
    public class CurveTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DefaultCurveHasCorrectKnotsAndBounds(bool large)
        {
            var s = Settings.CreateDefault();
            var points = (large ? s.LargeGrid : s.SmallGrid).CruiseCurve;
            float min = points[0].Mass, mid = points[1].Mass, max = points[2].Mass;
            float high = points[0].Speed, middle = points[1].Speed, low = points[2].Speed;
            Assert.Equal(high, s.GetCruiseSpeed(min - 1, large));
            Assert.Equal(high, s.GetCruiseSpeed(min, large));
            Assert.Equal(middle, s.GetCruiseSpeed(mid, large));
            Assert.Equal(low, s.GetCruiseSpeed(max, large));
            Assert.Equal(low, s.GetCruiseSpeed(max + 1, large));
            for (int i = 0; i <= 10000; i++) Assert.InRange(s.GetCruiseSpeed(min + (max - min) * i / 10000, large), low, high);
        }
        [Theory]
        [InlineData(0, 100, 0)]
        [InlineData(50, 100, 0)]
        [InlineData(100, 100, 0)]
        [InlineData(200, 100, 0.5)]
        public void DragAndHardCapOnlyAffectExcessSpeed(float speed, float cruise, float fraction)
        {
            Assert.Equal(fraction, SpeedMath.ExcessSpeedFraction(speed, cruise));
            Assert.Equal(1000 * 1.5f * fraction, SpeedMath.Resistance(1000, speed, cruise, 1.5f));
        }
        [Fact]
        public void DirectionSummaryKeepsPublicArrayOrder()
        { Assert.Equal(new[] { 2f, 1f, 3.5f, 12f }, SpeedMath.SummarizeAcceleration(new[] { 1f, 2f, 3f, 4f, 5f, 6f })); }
        [Fact]
        public void SummaryRejectsInvalidDirectionCounts()
        {
            Assert.Throws<ArgumentException>(() => SpeedMath.SummarizeAcceleration(null));
            Assert.Throws<ArgumentException>(() => SpeedMath.SummarizeAcceleration(new float[5]));
        }
    }
}
