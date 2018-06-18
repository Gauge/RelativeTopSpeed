using VRage.Utils;

namespace RelativeTopSpeed
{

    public class Logger
    {

        public static void Log(MyLogSeverity level, string message)
        {
            MyLog.Default.Log(level, $"[RelativeTopSpeed] {message}");
        }
    }
}
