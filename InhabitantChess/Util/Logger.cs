using OWML.Common;

namespace InhabitantChess.Util
{
    // borrowed from https://github.com/xen-42/outer-wilds-achievement-tracker/blob/main/AchievementTracker/Util/Logger.cs
    public static class Logger
    {
        private const string _prefix = $"[{(nameof(InhabitantChess))}] -- ";

        public static void Log(object obj, MessageType type = MessageType.Info)
        {
            if (InhabitantChess.Instance == null) return;

            InhabitantChess.Instance.ModHelper.Console.WriteLine($"{_prefix}{obj}", type);
        }

        public static void LogError(object obj)
        {
            Log(obj, MessageType.Error);
        }

        public static void LogSuccess(object obj)
        {
            Log(obj, MessageType.Success);
        }
    }

}
