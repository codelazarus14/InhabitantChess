namespace InhabitantChess
{
    public static class Controls
    {
        public static IInputCommands Interact => InputLibrary.interact;
        public static IInputCommands Overhead => InputLibrary.landingCamera;
        public static IInputCommands ExitOverhead => InputLibrary.cancel;
        public static IInputCommands PanCamera => InputLibrary.moveXZ;
        public static IInputCommands SpawnGame => InputLibrary.enter;
    }
}
