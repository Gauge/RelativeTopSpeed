using VRage.Utils;

namespace RelativeTopSpeed
{

    public class Tools
    {

        public static void Log(MyLogSeverity level, string message)
        {
            MyLog.Default.Log(level, $"[RelativeTopSpeed] {message}");
            MyLog.Default.Flush();
        }
    }
}
