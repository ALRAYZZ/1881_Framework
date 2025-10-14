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
			RegisterCommand("bringent", new Action<int, List<object>, string>(BringEntityId), false);
            RegisterCommand("bringnet", new Action<int, List<object>, string>(BringNetId), false);

			// GO commands
			RegisterCommand("gottopos", new Action<int, List<object>, string>(GoToPos), false);
            RegisterCommand("gottoent", new Action<int, List<object>, string>(GoToEntityId), false);
            RegisterCommand("gottonet", new Action<int, List<object>, string>(GoToNetId), false);



            RegisterCommand("getinfo", new Action<int, List<object>, string>(GetInfo), false);

            _eventHandlers["AdminManager:Teleport:Result"] += new Action<bool, string>(OnTeleportResult);
        }

        private void GetInfo(int src, List<object> args, string raw)
        {
            _vehicleLocator.RequestNearestVehicle();
		}

		private void BringEntityId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 1, "bringent <entityID>")) return;

            if (!int.TryParse(args[0]?.ToString(), out int entityId))
            {
                ChatHelper.PrintError("Invalid argument types for bringent command.");
                return;
            }
            int ped = PlayerPedId();
            if (ped == 0)
            {
                ChatHelper.PrintError("Player ped not found.");
                return;
            }

            if (entityId == 0 || !DoesEntityExist(entityId))
            {
                ChatHelper.PrintError($"No entity found with Entity ID: {entityId}");
                return;
            }

            if (!TryRequestControlOfEntity(entityId, 1000))
            {
                ChatHelper.PrintError($"Failed to gain control of entity with Entity ID: {entityId}");
                return;
			}

            Vector3 pos = GetEntityCoords(ped, false);
            SetEntityCoords(entityId, pos.X, pos.Y, pos.Z, false, false, false, true);
		}

		private void BringNetId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 1, "bringnet <netID>")) return;

            if (!int.TryParse(args[0]?.ToString(), out int netId))
            {
                ChatHelper.PrintError("Invalid argument types for bringnet command.");
                return;
            }
            int ped = PlayerPedId();
            if (ped == 0)
            {
                ChatHelper.PrintError("Player ped not found.");
                return;
			}

            if (NetworkDoesNetworkIdExist(netId))
            {
                int veh = ResolveVehicleFromNetId(netId);
                if (veh != 0 && TryRequestControlOfEntity(veh, 1000))
                {
                    Vector3 pos = GetEntityCoords(ped, false);
                    SetEntityCoords(veh, pos.X, pos.Y, pos.Z, false, false, false, true);
                    ChatHelper.PrintSuccess($"Brought entity with Net ID: {netId} to your position.");
                    return;
                }
            }
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

        private void GoToEntityId(int src, List<object> args, string raw)
        {
            if (!ValidateArgs(args, 1, "gotoent <entityID>")) return;
            if (!int.TryParse(args[0]?.ToString(), out int entityId))
            {
                ChatHelper.PrintError("Invalid argument type for gotoent command.");
                return;
            }

            if (!DoesEntityExist(entityId))
            {
                ChatHelper.PrintError($"No entity found with Entity ID: {entityId}");
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

        private void GoToNetId(int src, List<object> args, string raw)
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

        private static bool TryRequestControlOfEntity(int entity, int timeoutMs = 500)
        {
            if (entity == 0 || !DoesEntityExist(entity)) return false;

            int end = GetGameTimer() + timeoutMs;

            NetworkRequestControlOfEntity(entity);

            while (!NetworkHasControlOfEntity(entity) && GetGameTimer() < end)
            {
                NetworkRequestControlOfEntity(entity);
                Wait(0);
			}

            return NetworkHasControlOfEntity(entity);
		}

        private static int ResolveVehicleFromNetId(int netId)
        {
            if (!NetworkDoesEntityExistWithNetworkId(netId)) return 0;

            int ent = NetworkGetEntityFromNetworkId(netId);
            if (ent == 0 || !DoesEntityExist(ent)) return 0;

            if (IsEntityAVehicle(ent)) return ent;

            if (IsEntityAPed(ent))
            {
                int veh = GetVehiclePedIsIn(ent, false);
                if (veh != 0 && DoesEntityExist(veh))
                {
                    return veh;
                }
            }
            return 0;
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
