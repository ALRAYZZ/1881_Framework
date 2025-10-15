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
        private readonly PedLocator _pedLocator;

        public TeleportClientCommands(EventHandlerDictionary eventHandlers)
        {
            _eventHandlers = eventHandlers;
            _vehicleLocator = new VehicleLocator(_eventHandlers);
            _pedLocator = new PedLocator(_eventHandlers);

			// Register commands

			// BRING commands
            RegisterCommand("bring", new Action<int, List<object>, string>(BringNetId), false);

			// GO commands
			RegisterCommand("gotopos", new Action<int, List<object>, string>(GoToPos), false);
            RegisterCommand("goto", new Action<int, List<object>, string>(GoToNetId), false);



            RegisterCommand("vehinfo", new Action<int, List<object>, string>(GetVehInfo), false);
            RegisterCommand("pedinfo", new Action<int, List<object>, string>(GetPedInfo), false);

            _eventHandlers["AdminManager:Teleport:Result"] += new Action<bool, string>(OnTeleportResult);
        }

        private void GetPedInfo(int src, List<object> args, string raw)
        {
            _pedLocator.RequestNearestPed();
        }

        private void GetVehInfo(int src, List<object> args, string raw)
        {
            _vehicleLocator.RequestNearestVehicle();
		}

		private void BringNetId(int src, List<object> args, string raw)
        {
			if (!ValidateArgs(args, 1, "bringnet <netID>")) return;
			if (!int.TryParse(args[0]?.ToString(), out int netId))
			{
				ChatHelper.PrintError("Invalid netID.");
				return;
			}

			BaseScript.TriggerServerEvent("AdminManager:Teleport:BringEntity", netId);
		}

		private void GoToPos(int src, List<object> args, string raw)
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

        private void GoToNetId(int src, List<object> args, string raw)
        {
			if (!ValidateArgs(args, 1, "gotonet <netID>")) return;
			if (!int.TryParse(args[0]?.ToString(), out int netId))
			{
				ChatHelper.PrintError("Invalid netID.");
				return;
			}

			BaseScript.TriggerServerEvent("AdminManager:Teleport:GoToEntity", netId);
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
