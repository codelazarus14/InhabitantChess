using OWML.Common;

namespace InhabitantChess.Util
{
    // borrowed from https://github.com/xen-42/outer-wilds-achievement-tracker/blob/main/AchievementTracker/Util/Logger.cs
    public static class Logger
    {
        private static string _prefix = $"[{(nameof(InhabitantChess))}] -- ";

        public static void Log(string msg)
        {
            if (InhabitantChess.Instance == null) return;

            InhabitantChess.Instance.ModHelper.Console.WriteLine($"{_prefix}{msg}", MessageType.Info);
        }

        public static void LogError(string msg)
        {
            if (InhabitantChess.Instance == null) return;

            InhabitantChess.Instance.ModHelper.Console.WriteLine($"{_prefix}{msg}", MessageType.Error);
        }

        public static void LogSuccess(string msg)
        {
            if (InhabitantChess.Instance == null) return;

            InhabitantChess.Instance.ModHelper.Console.WriteLine($"{_prefix}{msg}", MessageType.Success);
        }
    }

}
