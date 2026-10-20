using System;

namespace HeliSim.FrontEnd
{
    public enum MenuLaunchMode
    {
        Solo,
        Host,
        Join
    }

    [Serializable]
    public sealed class MenuSessionOptions
    {
        public MenuLaunchMode Mode = MenuLaunchMode.Solo;
        public ClientRole Role = ClientRole.Player;
        public string ScenePath = "Assets/Level/Scenes/Multiplayer.unity";
        public string Address = "127.0.0.1";
        public ushort Port = 7777;
        public bool RecordFlightData = false;
        public int AssistancePreset = -1;

        public MenuSessionOptions Clone()
        {
            return (MenuSessionOptions)MemberwiseClone();
        }

        public bool TryValidate(Func<string, bool> sceneAvailable, out string error)
        {
            if (Mode != MenuLaunchMode.Solo && Mode != MenuLaunchMode.Host && Mode != MenuLaunchMode.Join)
            {
                error = "Choose Solo, Host or Join.";
                return false;
            }
            if (Role != ClientRole.Player && Role != ClientRole.Spectator)
            {
                error = "Choose a pilot or spectator role.";
                return false;
            }
            if (!IsSupportedScene(ScenePath))
            {
                error = "Choose the supported Multiplayer flight scene.";
                return false;
            }
            if (sceneAvailable == null || !sceneAvailable(ScenePath))
            {
                error = "The Multiplayer flight scene is unavailable. Check the enabled build scenes.";
                return false;
            }
            if (Port == 0)
            {
                error = "Enter a port between 1 and 65535.";
                return false;
            }
            if (AssistancePreset < -1 || AssistancePreset > 5)
            {
                error = "Choose an existing assistance preset or keep the current default.";
                return false;
            }
            if (Mode == MenuLaunchMode.Solo)
            {
                Role = ClientRole.Player;
                Address = "127.0.0.1";
            }
            else
            {
                if (!RoleSelectionUI.TryNormalizeAddress(Address, out string normalized))
                {
                    error = "Enter an IPv4 address with four decimal octets, such as 192.168.1.10.";
                    return false;
                }
                Address = normalized;
            }
            error = null;
            return true;
        }

        internal static bool IsSupportedScene(string path)
        {
            return string.Equals(path, "Assets/Level/Scenes/Multiplayer.unity", StringComparison.Ordinal);
        }
    }
}
