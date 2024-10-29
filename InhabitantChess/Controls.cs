namespace InhabitantChess
{
    public static class Controls
    {
        // TODO: fix central screenprompt displaying wrong interact text
        // currently uses interactPrompt in patches, which has a fixed binding
        // (stuck using E/X button, incompatible with X also being for overhead on controller)
        public static IInputCommands BoardMove => InputLibrary.lockOn;
        public static IInputCommands Overhead => InputLibrary.landingCamera;
        public static IInputCommands ExitOverhead => InputLibrary.cancel;
        public static IInputCommands MoveCamera => InputLibrary.moveXZ;
        public static IInputCommands SpawnGame => InputLibrary.autopilot;
    }
}
