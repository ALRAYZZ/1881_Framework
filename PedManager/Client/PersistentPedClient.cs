using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
	internal sealed class PersistentPedClient
	{
		private readonly EventHandlerDictionary _eventHandler;
		public PersistentPedClient(EventHandlerDictionary eventHandler)
		{
			_eventHandler = eventHandler;

			RegisterCommand("unpersistped", new Action<int, List<object>, string>(OnUnpersistPedCommand), false);

			_eventHandler["PedManager:Client:LoadPersistentPeds"] += new Action<dynamic>(SpawnPersistentPeds);
			_eventHandler["PedManager:Client:RequestPersistentPedSpawn"] += new Action<string>(OnRequestPersistentPedSpawn);
			_eventHandler["PedManager:Client:SpawnSinglePersistentPed"] += new Action<string, float, float, float, float, int>(SpawnSinglePersistentPed);
			_eventHandler["PedManager:Client:UnpersistPedNearestResponse"] += new Action<int, float>(OnUnpersistPedNearestResponse);
			_eventHandler["PedManager:Client:DeletePersistentPedById"] += new Action<int>(OnDeletePersistentPedById);
		}

		private void OnUnpersistPedCommand(int source, List<object> args, string raw)
		{
			if (args == null || args.Count < 1)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "[PedManager] Usage: /unpersistped <Net ID>" } });
				return;
			}
			if (!int.TryParse(args[0].ToString(), out int netId))
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "[PedManager] Invalid Net ID." } });
				return;
			}

			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity != 0 && DoesEntityExist(entity))
			{
				int dbId = DecorGetInt(entity, "PersistentPedId");
				if (dbId > 0)
				{
					BaseScript.TriggerServerEvent("PedManager:Server:UnpersistPedById", dbId);
				}
				else
				{
					BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "[PedManager] This ped is not marked as persistent." } });
				}

			}
			else
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "[PedManager] No ped found with that Net ID." } });
			}
		}

		private void OnDeletePersistentPedById(int dbId)
		{
			// Loop through all peds
			int handle = 0;
			int iter = FindFirstPed(ref handle);
			bool found = false;

			if (iter != 0)
			{
				do
				{
					if (DoesEntityExist(handle) && DecorExistOn(handle, "PersistentPedId"))
					{
						int pedDbId = DecorGetInt(handle, "PersistentPedId");
						if (pedDbId == dbId)
						{
							// Found the ped to delete
							SetEntityAsMissionEntity(handle, true, true);
							DeleteEntity(ref handle);
							found = true;
							break;
						}
					}
				} while (FindNextPed(iter, ref handle));
				EndFindPed(iter);
			}

			if (found)
			{
				Debug.WriteLine($"[PedManager] Deleted persistent ped with DB ID {dbId} from world.");
			}
			else
			{
				Debug.WriteLine($"[PedManager] No persistent ped with DB ID {dbId} found in world to delete.");
			}
		}

		private void OnUnpersistPedNearestResponse(int pedId, float distance)
		{
			if (pedId == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "[PedManager] No peds found nearby to unpersist." } });
				return;
			}
			int netId = NetworkGetNetworkIdFromEntity(pedId);
			if (netId == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "[PedManager] Failed to get network ID for the nearest ped." } });
				return;
			}
			// Send the Net ID to the server to unpersist
			BaseScript.TriggerServerEvent("PedManager:Server:UnpersistPed", netId, distance);
			Debug.WriteLine($"[PedManager] Requested unpersist for ped Net ID: {netId}, distance: {distance:F2}m");
		}

		private async void SpawnPersistentPeds(dynamic peds)
		{
			Debug.WriteLine($"[PedManager] Spawning persistent peds from database...");

			foreach (var pedData in peds)
			{
				string model = pedData.Model ?? pedData.Model;
				float x = (float)pedData.X;
				float y = (float)pedData.Y;
				float z = (float)pedData.Z;
				float heading = (float)pedData.Heading;

				uint hash = (uint)GetHashKey(model);
				RequestModel(hash);
				while (!HasModelLoaded(hash))
				{
					await BaseScript.Delay(1);
				}

				int ped = CreatePed(4, hash, x, y, z, heading, true, true);
				SetEntityAsMissionEntity(ped, true, true);
				FreezeEntityPosition(ped, true);
				SetEntityInvincible(ped, true);

				// Set decorator
				DecorSetInt(ped, "PersistentPedId", (int)pedData.Id);
			}
			Debug.WriteLine($"[PedManager] Spawned {peds.Count} persistent peds from database.");
		}

		private void OnRequestPersistentPedSpawn(string pedModel)
		{
			int playerPed = PlayerPedId();

			Vector3 playerPos = GetEntityCoords(playerPed, true);
			float playerHeading = GetEntityHeading(playerPed);

			// Calculate spawn position 2 units in front of player
			float forwardX = playerPos.X + (2.0f * (float)Math.Sin(-playerHeading * (Math.PI / 180.0)));
			float forwardY = playerPos.Y + (2.0f * (float)Math.Cos(-playerHeading * (Math.PI / 180.0)));
			float forwardZ = playerPos.Z + 2.0f;

			// Get ground Z at that position
			float groundZ = forwardZ;
			if (!GetGroundZFor_3dCoord(forwardX, forwardY, forwardZ, ref groundZ, false))
			{
				groundZ = playerPos.Z; // Fallback if ground Z not found
			}

			// Calculate heading to face player
			float pedHeading = (playerHeading + 180.0f) % 360;

			Debug.WriteLine($"[PedManager] Requesting spawn of persistent ped '{pedModel}' at ({forwardX}, {forwardY}, {groundZ}) facing heading {pedHeading}");

			// Send back to server with calculated position
			BaseScript.TriggerServerEvent("PedManager:Server:SpawnPersistentPed", pedModel, forwardX, forwardY, groundZ, pedHeading);
		}

		private async void SpawnSinglePersistentPed(string model, float x, float y, float z, float heading, int id)
		{
			int hash = (int)GetHashKey(model);
			RequestModel((uint)hash);

			while (!HasModelLoaded((uint)hash))
			{
				await BaseScript.Delay(1);
			}

			int ped = CreatePed(4, (uint)hash, x, y, z, heading, true, true);
			SetEntityAsMissionEntity(ped, true, true);
			SetEntityInvincible(ped, true);
			SetBlockingOfNonTemporaryEvents(ped, true);

			DecorSetInt(ped, "PersistentPedId", id);

			await BaseScript.Delay(100); // slight delay to ensure ped is created before placing on ground

			PlaceObjectOnGroundOrObjectProperly(ped);
			FreezeEntityPosition(ped, true);

			Debug.WriteLine($"[PedManager] Spawned persistent ped '{model}' at ({x}, {y}, {z}) with heading {heading}");
		}

	}
}
