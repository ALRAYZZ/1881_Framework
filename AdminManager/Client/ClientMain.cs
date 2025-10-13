using System;
using System.Threading.Tasks;
using AdminManager.Client.Commands;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Client
{
    public class ClientMain : BaseScript
    {
        private readonly TeleportClientCommands _teleportCommands;

        public ClientMain()
        {
            _teleportCommands = new TeleportClientCommands(EventHandlers);
        }
    }
}