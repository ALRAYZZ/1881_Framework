using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using VehicleManager.Server.Commands;
using VehicleManager.Server.Interfaces;
using static CitizenFX.Core.Native.API;
using VehicleManager.Server.Models;
using System.Linq;

namespace VehicleManager.Server
{
    public class ServerMain : BaseScript
    {
        // Exports from DatabaseCore
        private dynamic _db;

        // Track net IDs of world vehicles (per player re-syncs)
        private readonly Dictionary<int, WorldVehicleData> _worldVehicles = new Dictionary<int, WorldVehicleData>();

        private VehicleCommands _vehicleCommands;

        public ServerMain()
        {
            IVehicleManager vehicleManager = new Services.VehicleManager();

            _db = Exports["Database"];

            // Pass db to commands
            _vehicleCommands = new VehicleCommands(vehicleManager, Players, _db);

            // Register server event
            EventHandlers["VehicleManager:Server:SaveParkedVehicle"] += new Action<Player, uint, string, float, float, float, float, float, float, float>(OnSaveParkedVehicle);

            // Listen for PlayerCore notification that a player has spawned
            EventHandlers["VehicleManager:Server:SyncWorldVehiclesForPlayer"] += new Action<Player>(OnSyncWorldVehiclesForPlayer);

            // Listen for entity removal to respawn destroyed world vehicles
            EventHandlers["entityRemoved"] += new Action<int>(OnEntityRemoved);

			Debug.WriteLine("[VehicleManager] Server initialized.");

            _ = SpawnWorldVehiclesAsync();
            _ = MonitorWorldVehicles();
        }

        private void OnSyncWorldVehiclesForPlayer([FromSource] Player player)
        {
            if (player == null) return;

            Debug.WriteLine($"[VehicleManager] Syncing {_worldVehicles.Count} world vehicles for player {player.Name}");

			// Notify client about all existing world vehicles
            foreach(var kvp in _worldVehicles)
            {
                int netId = kvp.Value.NetId;
                int entity = NetworkGetEntityFromNetworkId(netId);

                if (entity != 0 && DoesEntityExist(entity))
                {
                    player.TriggerEvent("VehicleManager:Client:SetVehicleOnGround", netId);
				}
			}
		}

		private void OnSaveParkedVehicle([FromSource] Player player, uint modelHash, string plate, float x, float y, float z, float heading, float rx, float ry, float rz)
        {
            _vehicleCommands.SaveVehicleToDatabase(player, modelHash, plate, x, y, z, heading, rx, ry, rz);

            // Reload world vehicles to include the newly parked one
            _ = Task.Run(async () =>
            {
                await Delay(1000);
                await SpawnWorldVehiclesAsync();
            });
        }

        private void OnEntityRemoved(int entity)
        {
			// Check if this was a world vehicle
            int netId = NetworkGetNetworkIdFromEntity(entity);
            if (netId == 0) return;

            var vehicleToRespawn = _worldVehicles.FirstOrDefault(kvp => kvp.Value.NetId == netId);
            if (vehicleToRespawn.Key != 0)
            {
                Debug.WriteLine($"[VehicleManager] World vehicle with netId {netId} removed. Respawning...");
                _ = RespawnWorldVehicle(vehicleToRespawn.Key, vehicleToRespawn.Value);
            }
		}

        private async Task RespawnWorldVehicle(int dbId, WorldVehicleData data)
        {
            await Delay(5000); // Wait 5 seconds before respawn

            float zSpawn = data.Z + 1.0f;
            int veh = CreateVehicle(data.ModelHash, data.X, data.Y, zSpawn, data.Heading, true, true);

            if (veh != 0)
            {
                if (!string.IsNullOrEmpty(data.Plate))
                {
                    SetVehicleNumberPlateText(veh, data.Plate);
				}

                int netId = NetworkGetNetworkIdFromEntity(veh);
                if (netId != 0)
                {
                    _worldVehicles[dbId].NetId = netId;
                    TriggerClientEvent("VehicleManager:Client:SetVehicleOnGround", netId);
                    Debug.WriteLine($"[VehicleManager] Respawned world vehicle with netId {netId}.");
                }
			}
		}

        private async Task MonitorWorldVehicles()
        {
            while (true)
            {
                await Delay(30000); // Check every 30 seconds

                foreach (var kvp in _worldVehicles.ToList())
                {
                    int dbId = kvp.Key;
                    var data = kvp.Value;
                    int entity = NetworkGetEntityFromNetworkId(data.NetId);

                    if (entity == 0 || !DoesEntityExist(entity))
                    {
                        Debug.WriteLine($"[VehicleManager] World vehicle with netId {data.NetId} missing. Respawning...");
                        await RespawnWorldVehicle(dbId, data);
                    }
                }
			}
        }

		private async Task SpawnWorldVehiclesAsync()
        {
            // Make sure oxmysql and Database are loaded
            await Delay(0);

            const string sql = @"
                SELECT
                id,
                model,
                plate,
                CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.x')) AS DOUBLE)   AS x,
                CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.y')) AS DOUBLE)   AS y,
                CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.z')) AS DOUBLE)   AS z,
                CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.heading')) AS DOUBLE) AS heading
                FROM world_vehicles;";

            _db.Query(sql, new Dictionary<string, object>(), new Action<dynamic>(rows =>
            {
                int count = 0;
                foreach (var row in rows)
                {
                    try
                    {
                        int dbId = Convert.ToInt32(row.id);
                        string modelStr = row.model;
                        uint modelHash;
                        if (!uint.TryParse(modelStr, out modelHash))
                        {
                            modelHash = (uint)GetHashKey(modelStr);
                        }

                        float x = Convert.ToSingle(row.x, CultureInfo.InvariantCulture);
                        float y = Convert.ToSingle(row.y, CultureInfo.InvariantCulture);
                        float z = Convert.ToSingle(row.z, CultureInfo.InvariantCulture);
                        float heading = Convert.ToSingle(row.heading, CultureInfo.InvariantCulture);

                        // Spawn above ground to avoid clipping; clients will ground it
                        float zSpawn = z + 1.0f;

                        // Server-side CreateVehicle automatically creates a networked entity
                        int veh = CreateVehicle(modelHash, x, y, zSpawn, heading, true, true);
                        if (veh != 0)
                        {
                            string plate = row.plate;
                            if (!string.IsNullOrEmpty(plate))
                            {
                                SetVehicleNumberPlateText(veh, plate);
                            }

                            // Get network ID for client communication
                            int netId = NetworkGetNetworkIdFromEntity(veh);
                            if (netId != 0)
                            {
                                _worldVehicles[dbId] = new WorldVehicleData
                                {
                                    NetId = netId,
                                    ModelHash = modelHash,
                                    Plate = plate,
                                    X = x,
                                    Y = y,
                                    Z = z,
                                    Heading = heading
                                };  

                                // Ask clients to place it on the ground properly
                                TriggerClientEvent("VehicleManager:Client:SetVehicleOnGround", netId);
                            }

                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[VehicleManager] Error spawning world vehicle: {ex.Message}");
                    }
                }
                Debug.WriteLine($"[VehicleManager] Spawned {count} world vehicles.");
            }));
        }
    }
}