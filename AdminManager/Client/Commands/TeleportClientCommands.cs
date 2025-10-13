using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using AdminManager.Client.Helpers;
using static CitizenFX.Core.Native.API;
using AdminManager.Client.Services;

namespace AdminManager.Client.Commands
{
    internal sealed class TeleportClientCommands
    {
        private readonly EventHandlerDictionary _eventHandlers;
        private readonly VehicleLocator _vehicleLocator;

        public TeleportClientCommands(EventHandlerDictionary eventHandlers)
        {
            _eventHandlers = eventHandlers;
            _vehicleLocator = new VehicleLocator(_eventHandlers);

			// Register commands

			// BRING commands
			RegisterCommand("bringent", new Action<int, List<object>, string>(CmdTpEntityId), false);
            RegisterCommand("bringnet", new Action<int, List<object>, string>(CmdTpNetId), false);

			// GO commands
			RegisterCommand("gottopos", new Action<int, List<object>, string>(CmdTpPos), false);
            RegisterCommand("gottoent", new Action<int, List<object>, string>(CmdGoToEntityId), false);
            RegisterCommand("gottonet", new Action<int, List<object>, string>(CmdGoToNetId), false);



            RegisterCommand("getinfo", new Action<int, List<object>, string>(CmdGetInfo), false);

            _eventHandlers["AdminManager:Teleport:Result"] += new Action<bool, string>(OnTeleportResult);
        }

        private void CmdGetInfo(int src, List<object> args, string raw)
        {
            _vehicleLocator.RequestNearestVehicle();
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

        private void CmdGoToEntityId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 1, "gotoent <entityID>")) return;
            int entityId;
            if (!int.TryParse(args[0]?.ToString(), out entityId))
            {
                ChatHelper.PrintError("Invalid argument type for gotoent command.");
                return;
            }
            int ped = PlayerPedId();
            if (ped == 0)
            {
                ChatHelper.PrintError("Player ped not found.");
                return;
			}
            Vector3 pos = GetEntityCoords(entityId, false);

			SetEntityCoords(ped, pos.X, pos.Y, pos.Z, false, false, false, true);
		}

        private void CmdGoToNetId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 1, "gotonet <netID>")) return;
            int netId;
            if (!int.TryParse(args[0]?.ToString(), out netId))
            {
                ChatHelper.PrintError("Invalid argument type for gotonet command.");
                return;
            }
            int entityId = NetworkGetEntityFromNetworkId(netId);
            if (entityId == 0 || !DoesEntityExist(entityId))
            {
                ChatHelper.PrintError($"No entity found with Net ID: {netId}");
                return;
            }
            int ped = PlayerPedId();
            if (ped == 0)
            {
                ChatHelper.PrintError("Player ped not found.");
                return;
            }
            Vector3 pos = GetEntityCoords(entityId, false);
            SetEntityCoords(ped, pos.X, pos.Y, pos.Z, false, false, false, true);
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
