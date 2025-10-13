using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using AdminManager.Client.Helpers;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Client.Commands
{
    internal sealed class TeleportClientCommands
    {
        private readonly EventHandlerDictionary _eventHandlers;
        private readonly VehicleLocator _vehicleLocator;

        public TeleportClientCommands(EventHandlerDictionary eventHandlers)
        {
            _eventHandlers = eventHandlers;
            _vehicleLocator = new VehicleLocator(); // USE VEHICLE MANAGER

            // Register commands
            RegisterCommand("tpent", new Action<int, List<object>, string>(CmdTpEntityId), true);
            RegisterCommand("tpnet", new Action<int, List<object>, string>(CmdTpNetId), true);
            RegisterCommand("tppos", new Action<int, List<object>, string>(CmdTpPos), true);

            _eventHandlers["AdminManager:Teleport:Result"] += new Action<bool, string>(OnTeleportResult);
        }

        private void CmdTpEntityId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 4, "tpent <entityID> <x> <y> <z>")) return;

            int entityId;
            float x, y, z;

            if (!int.TryParse(args[0]?.ToString(), out entityId) ||
                !TryParseFloat(args[1], out x) ||
                !TryParseFloat(args[2], out y) ||
                !TryParseFloat(args[3], out z))
            {
                ChatHelper.PrintError("Invalid argument types for tpent command.");
                return;
            }

            BaseScript.TriggerServerEvent("AdminManager:Teleport:EntityID", entityId, x, y, z);
        }

        private void CmdTpNetId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 4, "tpnet <netID> <x> <y> <z>")) return;

            int netId;
            float x, y, z;

            if (!int.TryParse(args[0]?.ToString(), out netId) ||
                !TryParseFloat(args[1], out x) ||
                !TryParseFloat(args[2], out y) ||
                !TryParseFloat(args[3], out z))
            {
                ChatHelper.PrintError("Invalid argument types for tpnet command.");
                return;
            }

            BaseScript.TriggerServerEvent("AdminManager:Teleport:NetID", netId, x, y, z);
        }

        private void CmdTpPos(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 3, "tppos <x> <y> <z>")) return;

            float x, y, z;

            if (!TryParseFloat(args[0], out x) ||
                !TryParseFloat(args[1], out y) ||
                !TryParseFloat(args[2], out z))
            {
                ChatHelper.PrintError("Invalid argument types for tppos command.");
                return;
            }

            int ped = PlayerPedId();
            if (ped == 0)
            {
                ChatHelper.PrintError("Player ped not found.");
                return;
            }

            SetEntityCoords(ped, x, y, z, false, false, false, true);
            ChatHelper.PrintInfo($"Teleported to position: {x}, {y}, {z}");
        }

        private void OnTeleportResult(bool success, string message)
        {
            if (success)
                ChatHelper.PrintSuccess(message);
            else
                ChatHelper.PrintError(message);
        }

        private static bool TryParseFloat(object o, out float f) =>
            float.TryParse(o?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static bool ValidateArgs(List<object> args, int expectedCount, string usage)
        {
            if (args == null || args.Count < expectedCount)
            {
                ChatHelper.PrintError($"Invalid arguments. Usage: {usage}");
                return false;
            }
            return true;
        }
    }
}
