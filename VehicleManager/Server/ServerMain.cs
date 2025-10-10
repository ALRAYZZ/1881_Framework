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

        // Lock to prevent concurrent spawning
        private bool _isSpawning = false;

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
            foreach (var kvp in _worldVehicles)
            {
                int netId = kvp.Value.NetId;
                
                // Don't try to get entity on server - just send the netId to client
                if (netId != 0)
                {
                    player.TriggerEvent("VehicleManager:Client:SetVehicleOnGround", netId);
                }
            }
        }

        private void OnSaveParkedVehicle([FromSource] Player player, uint modelHash, string plate, float x, float y, float z, float heading, float rx, float ry, float rz)
        {
            _vehicleCommands.SaveVehicleToDatabase(player, modelHash, plate, x, y, z, heading, rx, ry, rz);

            // Instead of reloading everything, just spawn the new vehicle
            _ = Task.Run(async () =>
            {
                await Delay(1000);
                await SpawnNewlyParkedVehicle(modelHash, plate, x, y, z, heading);
            });
        }

        private async Task SpawnNewlyParkedVehicle(uint modelHash, string plate, float x, float y, float z, float heading)
        {
            try
            {
                // Query the database for the vehicle we just inserted
                const string sql = @"
                    SELECT id FROM world_vehicles 
                    WHERE model = @model AND plate = @plate 
                    ORDER BY id DESC LIMIT 1;";

                var parameters = new Dictionary<string, object>
                {
                    ["@model"] = modelHash.ToString(),
                    ["@plate"] = plate
                };

                _db.Query(sql, parameters, new Action<dynamic>(rows =>
                {
                    if (rows != null)
                    {
                        dynamic firstRow = null;
                        if (rows is System.Collections.IEnumerable enumerable)
                        {
                            var enumerator = enumerable.GetEnumerator();
                            if (enumerator.MoveNext())
                            {
                                firstRow = enumerator.Current;
                            }
                        }
                        else
                        {
                            firstRow = rows;
                        }

                        if (firstRow != null)
                        {
                            int dbId = Convert.ToInt32(firstRow.id);

                            // Check if already spawned
                            if (_worldVehicles.ContainsKey(dbId))
                            {
                                Debug.WriteLine($"[VehicleManager] Vehicle {dbId} already spawned, skipping.");
                                return;
                            }

                            float zSpawn = z + 1.0f;
                            
                            // Most vehicles are automobiles, you can extend this logic later
                            string vehicleType = "automobile";
                            
                            // Use CREATE_VEHICLE_SERVER_SETTER native (hash: 0x6AE51D4B)
                            int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, modelHash, vehicleType, x, y, zSpawn, heading);

                            if (veh != 0)
                            {
                                if (!string.IsNullOrEmpty(plate))
                                {
                                    SetVehicleNumberPlateText(veh, plate);
                                }

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

                                    TriggerClientEvent("VehicleManager:Client:SetVehicleOnGround", netId);
                                    Debug.WriteLine($"[VehicleManager] Spawned newly parked vehicle (DB ID: {dbId}, NetID: {netId})");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[VehicleManager] Failed to spawn vehicle DB ID {dbId} with hash {modelHash}");
                            }
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error spawning newly parked vehicle: {ex.Message}");
            }
        }

        private void OnEntityRemoved(int entity)
        {
            try
            {
                // Search through our tracked vehicles to find which one was removed
                var vehicleToRespawn = _worldVehicles.FirstOrDefault(kvp =>
                {
                    try
                    {
                        int trackedEntity = NetworkGetEntityFromNetworkId(kvp.Value.NetId);
                        return trackedEntity == entity;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (vehicleToRespawn.Key != 0)
                {
                    Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {vehicleToRespawn.Key}) removed. Scheduling respawn...");
                    _ = RespawnWorldVehicle(vehicleToRespawn.Key, vehicleToRespawn.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error in OnEntityRemoved: {ex.Message}");
            }
        }

        private async Task RespawnWorldVehicle(int dbId, WorldVehicleData data)
        {
            await Delay(5000); // Wait 5 seconds before respawn

            try
            {
                float zSpawn = data.Z + 1.0f;
                string vehicleType = "automobile";
                
                // Use CREATE_VEHICLE_SERVER_SETTER
                int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, data.ModelHash, vehicleType, data.X, data.Y, zSpawn, data.Heading);

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
                        Debug.WriteLine($"[VehicleManager] Respawned world vehicle (DB ID: {dbId}, NetID: {netId})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error respawning vehicle {dbId}: {ex.Message}");
            }
        }

        private async Task MonitorWorldVehicles()
        {
            while (true)
            {
                await Delay(30000); // Check every 30 seconds

                foreach (var kvp in _worldVehicles.ToList())
                {
                    try
                    {
                        int dbId = kvp.Key;
                        var data = kvp.Value;
                        int entity = NetworkGetEntityFromNetworkId(data.NetId);

                        if (entity == 0 || !DoesEntityExist(entity))
                        {
                            Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {dbId}) missing. Respawning...");
                            await RespawnWorldVehicle(dbId, data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[VehicleManager] Error monitoring vehicle: {ex.Message}");
                    }
                }
            }
        }

        private async Task SpawnWorldVehiclesAsync()
        {
            // Prevent concurrent spawning
            if (_isSpawning)
            {
                Debug.WriteLine("[VehicleManager] Already spawning vehicles, skipping.");
                return;
            }

            _isSpawning = true;

            try
            {
                // Wait for database to be ready
                await Delay(1000);

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

                            // Skip if already spawned
                            if (_worldVehicles.ContainsKey(dbId))
                            {
                                Debug.WriteLine($"[VehicleManager] Vehicle {dbId} already exists, skipping.");
                                count++;
                                continue;
                            }

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
                            string plate = row.plate;

                            // Spawn above ground to avoid clipping; clients will ground it
                            float zSpawn = z + 1.0f;

                            // Default to automobile for now
                            string vehicleType = "automobile";

                            // Use CREATE_VEHICLE_SERVER_SETTER
                            int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, modelHash, vehicleType, x, y, zSpawn, heading);
                            
                            if (veh != 0)
                            {
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
                            else
                            {
                                Debug.WriteLine($"[VehicleManager] Failed to create vehicle entity for DB ID {dbId}, hash {modelHash}, type {vehicleType}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error in SpawnWorldVehiclesAsync: {ex.Message}");
            }
            finally
            {
                _isSpawning = false;
            }
        }
    }
}