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

        // Track world vehicles by DB ID (runtime only)
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
            EventHandlers["VehicleManager:Server:SaveParkedVehicle"] += new Action<Player, uint, string, string, float, float, float, float, float, float, float, int>(OnSaveParkedVehicle);

            // Listen for PlayerCore notification that a player has spawned
            EventHandlers["VehicleManager:Server:SyncWorldVehiclesForPlayer"] += new Action<Player>(OnSyncWorldVehiclesForPlayer);

            // Listen for entity removal to respawn destroyed world vehicles
            EventHandlers["entityRemoved"] += new Action<int>(OnEntityRemoved);

            Debug.WriteLine("[VehicleManager] Server initialized.");

            // Delay initial spawn to allow existing vehicles to be discovered
            _ = Task.Run(async () =>
            {
                await Delay(2000);
                await DiscoverExistingVehicles();
                await SpawnWorldVehiclesAsync();
                _ = MonitorWorldVehicles();
            });
        }

        private async Task DiscoverExistingVehicles()
        {
            Debug.WriteLine("[VehicleManager] Discovering existing vehicles in the world...");

            try
            {
                // Get all vehicles currently in the world (returns List<object>, need to convert)
                var allVehiclesObj = GetAllVehicles();
                List<int> allVehicles = new List<int>();
                
                // Convert List<object> to List<int>
                foreach (var vehObj in allVehiclesObj)
                {
                    try
                    {
                        int veh = Convert.ToInt32(vehObj);
                        if (DoesEntityExist(veh))
                        {
                            allVehicles.Add(veh);
                        }
                    }
                    catch { }
                }

                Debug.WriteLine($"[VehicleManager] Found {allVehicles.Count} vehicles in the world.");

                const string sql = @"
                    SELECT
                    id,
                    model,
                    vehicle_type,
                    plate,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.x')) AS DOUBLE)   AS x,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.y')) AS DOUBLE)   AS y,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.z')) AS DOUBLE)   AS z,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.heading')) AS DOUBLE) AS heading
                    FROM world_vehicles;";

                _db.Query(sql, new Dictionary<string, object>(), new Action<dynamic>(rows =>
                {
                    int discovered = 0;

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

                            string plate = row.plate;
                            float dbX = Convert.ToSingle(row.x, CultureInfo.InvariantCulture);
                            float dbY = Convert.ToSingle(row.y, CultureInfo.InvariantCulture);
                            float dbZ = Convert.ToSingle(row.z, CultureInfo.InvariantCulture);
                            float dbHeading = Convert.ToSingle(row.heading, CultureInfo.InvariantCulture);
                            string vehicleType = row.vehicle_type ?? "automobile";

                            // Check if a vehicle matching this data already exists in the world
                            foreach (int veh in allVehicles)
                            {
                                if (!DoesEntityExist(veh)) continue;

                                uint vehModel = (uint)GetEntityModel(veh);
                                string vehPlate = GetVehicleNumberPlateText(veh);
                                var vehPos = GetEntityCoords(veh);

                                // Match by model, plate, and approximate position (within 5 units)
                                bool modelMatch = vehModel == modelHash;
                                bool plateMatch = string.Equals(vehPlate?.Trim(), plate?.Trim(), StringComparison.OrdinalIgnoreCase);
                                float distance = GetDistance(vehPos.X, vehPos.Y, vehPos.Z, dbX, dbY, dbZ);
                                bool positionMatch = distance < 10.0f; // Increased tolerance

                                if (modelMatch && plateMatch && positionMatch)
                                {
                                    // This vehicle already exists in the world!
                                    int netId = NetworkGetNetworkIdFromEntity(veh);
                                    
                                    _worldVehicles[dbId] = new WorldVehicleData
                                    {
                                        NetId = netId,
                                        EntityId = veh,
                                        ModelHash = modelHash,
                                        VehicleType = vehicleType,
                                        Plate = plate,
                                        X = dbX,
                                        Y = dbY,
                                        Z = dbZ,
                                        Heading = dbHeading
                                    };

                                    discovered++;
                                    Debug.WriteLine($"[VehicleManager] Discovered existing vehicle (DB ID: {dbId}, Entity: {veh}, NetID: {netId}, Plate: {plate})");
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[VehicleManager] Error discovering vehicle: {ex.Message}");
                        }
                    }

                    Debug.WriteLine($"[VehicleManager] Discovered {discovered} existing world vehicles.");
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error in DiscoverExistingVehicles: {ex.Message}");
            }
        }

        private float GetDistance(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float dz = z2 - z1;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private void OnSyncWorldVehiclesForPlayer([FromSource] Player player)
        {
            if (player == null) return;

            Debug.WriteLine($"[VehicleManager] Syncing {_worldVehicles.Count} world vehicles for player {player.Name}");

            // Notify client about all existing world vehicles
            foreach (var kvp in _worldVehicles)
            {
                int netId = kvp.Value.NetId;
                
                if (netId != 0)
                {
                    player.TriggerEvent("VehicleManager:Client:SetVehicleOnGround", netId);
                }
            }
        }

        private void OnSaveParkedVehicle([FromSource] Player player, uint modelHash, string vehicleType, string plate, float x, float y, float z, float heading, float rx, float ry, float rz, int entityId)
        {
            // Get the network ID from the entity
            int netId = NetworkGetNetworkIdFromEntity(entityId);

            // Check if this vehicle is already tracked (player is re-parking an existing world vehicle)
            var existingVehicle = _worldVehicles.FirstOrDefault(kvp => kvp.Value.EntityId == entityId);
            
            if (existingVehicle.Key != 0)
            {
                // Update existing vehicle position in database
                UpdateVehiclePosition(existingVehicle.Key, x, y, z, heading, rx, ry, rz);
                player.TriggerEvent("chat:addMessage", new { args = new[] { $"Updated parked {vehicleType} position (ID: {existingVehicle.Key})" } });
            }
            else
            {
                // Save new parked vehicle (NO entity ID in database)
                _vehicleCommands.SaveVehicleToDatabase(player, modelHash, vehicleType, plate, x, y, z, heading, rx, ry, rz, entityId);

                // Add to tracking immediately (will be updated with DB ID after insert)
                _ = Task.Run(async () =>
                {
                    await Delay(500);
                    await RegisterExistingVehicle(modelHash, vehicleType, plate, x, y, z, heading, entityId, netId);
                });
            }
        }

        private void UpdateVehiclePosition(int dbId, float x, float y, float z, float heading, float rx, float ry, float rz)
        {
            string J(double v) => v.ToString(CultureInfo.InvariantCulture);
            string positionJson = $"{{\"x\":{J(x)},\"y\":{J(y)},\"z\":{J(z)},\"heading\":{J(heading)}}}";
            string rotationJson = $"{{\"x\":{J(rx)},\"y\":{J(ry)},\"z\":{J(rz)}}}";

            const string sql = @"
                UPDATE world_vehicles 
                SET position = @position, rotation = @rotation
                WHERE id = @id;";

            var parameters = new Dictionary<string, object>
            {
                ["@id"] = dbId,
                ["@position"] = positionJson,
                ["@rotation"] = rotationJson
            };

            _db.Query(sql, parameters, new Action<dynamic>(_ =>
            {
                Debug.WriteLine($"[VehicleManager] Updated position for vehicle DB ID {dbId}");
            }));

            // Update in-memory tracking
            if (_worldVehicles.ContainsKey(dbId))
            {
                _worldVehicles[dbId].X = x;
                _worldVehicles[dbId].Y = y;
                _worldVehicles[dbId].Z = z;
                _worldVehicles[dbId].Heading = heading;
            }
        }

        private async Task RegisterExistingVehicle(uint modelHash, string vehicleType, string plate, float x, float y, float z, float heading, int entityId, int netId)
        {
            try
            {
                // Query by model, plate, and position (NOT entity ID)
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

                            _worldVehicles[dbId] = new WorldVehicleData
                            {
                                NetId = netId,
                                EntityId = entityId,
                                ModelHash = modelHash,
                                VehicleType = vehicleType,
                                Plate = plate,
                                X = x,
                                Y = y,
                                Z = z,
                                Heading = heading
                            };

                            Debug.WriteLine($"[VehicleManager] Registered existing parked vehicle (DB ID: {dbId}, Entity: {entityId})");
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error registering existing vehicle: {ex.Message}");
            }
        }

        private void OnEntityRemoved(int entity)
        {
            try
            {
                // Find if this was a tracked world vehicle by entity ID
                var vehicleToRespawn = _worldVehicles.FirstOrDefault(kvp => kvp.Value.EntityId == entity);

                if (vehicleToRespawn.Key != 0)
                {
                    Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {vehicleToRespawn.Key}, Entity: {entity}) destroyed. Scheduling respawn...");
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
                // Check if vehicle still exists (might have been driven away, not destroyed)
                int existingEntity = NetworkGetEntityFromNetworkId(data.NetId);
                if (existingEntity != 0 && DoesEntityExist(existingEntity))
                {
                    Debug.WriteLine($"[VehicleManager] Vehicle {dbId} still exists, not respawning.");
                    return;
                }

                float zSpawn = data.Z + 1.0f;
                
                // Use the stored vehicle type
                int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, data.ModelHash, data.VehicleType, data.X, data.Y, zSpawn, data.Heading);

                if (veh != 0)
                {
                    if (!string.IsNullOrEmpty(data.Plate))
                    {
                        SetVehicleNumberPlateText(veh, data.Plate);
                    }

                    int netId = NetworkGetNetworkIdFromEntity(veh);
                    if (netId != 0)
                    {
                        // Update tracking with new entity/network IDs (in memory only)
                        _worldVehicles[dbId].NetId = netId;
                        _worldVehicles[dbId].EntityId = veh;

                        TriggerClientEvent("VehicleManager:Client:SetVehicleOnGround", netId);
                        Debug.WriteLine($"[VehicleManager] Respawned world vehicle (DB ID: {dbId}, NetID: {netId}, Entity: {veh})");
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
                        int entity = data.EntityId;

                        // Only respawn if entity doesn't exist (destroyed), not if it's just being driven
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
            if (_isSpawning)
            {
                Debug.WriteLine("[VehicleManager] Already spawning vehicles, skipping.");
                return;
            }

            _isSpawning = true;

            try
            {
                await Delay(1000);

                const string sql = @"
                    SELECT
                    id,
                    model,
                    vehicle_type,
                    plate,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.x')) AS DOUBLE)   AS x,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.y')) AS DOUBLE)   AS y,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.z')) AS DOUBLE)   AS z,
                    CAST(JSON_UNQUOTE(JSON_EXTRACT(position, '$.heading')) AS DOUBLE) AS heading
                    FROM world_vehicles;";

                _db.Query(sql, new Dictionary<string, object>(), new Action<dynamic>(rows =>
                {
                    int count = 0;
                    int skipped = 0;

                    foreach (var row in rows)
                    {
                        try
                        {
                            int dbId = Convert.ToInt32(row.id);

                            // CRITICAL: Skip if already tracked (vehicle exists in world)
                            if (_worldVehicles.ContainsKey(dbId))
                            {
                                skipped++;
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
                            string vehicleType = row.vehicle_type ?? "automobile";

                            float zSpawn = z + 1.0f;

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
                                        EntityId = veh,
                                        ModelHash = modelHash,
                                        VehicleType = vehicleType,
                                        Plate = plate,
                                        X = x,
                                        Y = y,
                                        Z = z,
                                        Heading = heading
                                    };

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
                    
                    if (skipped > 0)
                    {
                        Debug.WriteLine($"[VehicleManager] Skipped {skipped} vehicles that already exist in the world.");
                    }
                    Debug.WriteLine($"[VehicleManager] Spawned {count} new world vehicles.");
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