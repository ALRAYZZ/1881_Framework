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

        // NEW: Reverse lookup - map NETWORK ID to database ID for quick unpark lookups
        private readonly Dictionary<int, int> _netIdToDbId = new Dictionary<int, int>();

        private VehicleCommands _vehicleCommands;

        // Lock to prevent concurrent spawning
        private bool _isSpawning = false;

        // Track entities we intentionally deleted to ignore their removal events
        private readonly HashSet<int> _ignoredRemovedEntities = new HashSet<int>();

        public ServerMain()
        {
            IVehicleManager vehicleManager = new Services.VehicleManager();

            _db = Exports["Database"];

            // Pass db to commands
            _vehicleCommands = new VehicleCommands(vehicleManager, Players, _db);

            // Register server event
            EventHandlers["VehicleManager:Server:SaveParkedVehicle"] += new Action<Player, uint, string, string, float, float, float, float, float, float, float, int, int, int, string, string>(OnSaveParkedVehicle);

            EventHandlers["VehicleManager:Server:UnparkVehicle"] += new Action<Player, int>(OnUnparkVehicle);

            // NEW: Query if a vehicle entity is a world vehicle
            EventHandlers["VehicleManager:Server:IsWorldVehicle"] += new Action<Player, int, string>(OnIsWorldVehicle);

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

        private void OnEntityRemoved(int entity)
        {
            try
            {
                // Check if this is an entity we intentionally deleted
                if (_ignoredRemovedEntities.Remove(entity))
                {
                    Debug.WriteLine($"[VehicleManager] Ignoring removal of intentionally deleted entity {entity}");
                    return;
                }

                // Get netId from entity to find DB ID
                int netId = NetworkGetNetworkIdFromEntity(entity);
                if (netId != 0 && _netIdToDbId.TryGetValue(netId, out int dbId))
                {
                    Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {dbId}, Entity: {entity}, NetID: {netId}) destroyed. Scheduling respawn...");

                    // Remove from reverse lookup since entity is gone
                    _netIdToDbId.Remove(netId);

                    if (_worldVehicles.TryGetValue(dbId, out WorldVehicleData data))
                    {
                        _ = RespawnWorldVehicle(dbId, data);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error in OnEntityRemoved: {ex.Message}");
            }
        }

        private async Task DiscoverExistingVehicles()
        {
            Debug.WriteLine("[VehicleManager] Discovering existing vehicles in the world...");

            try
            {
                // Get all vehicles currently in the world
                var allVehiclesObj = GetAllVehicles();
                List<int> allVehicles = new List<int>();

                // Convert to List<int>
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

                                // Match by model and plate (position can drift)
                                bool modelMatch = vehModel == modelHash;
                                bool plateMatch = string.Equals(vehPlate?.Trim(), plate?.Trim(), StringComparison.OrdinalIgnoreCase);

                                if (modelMatch && plateMatch)
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

                                    // Add to reverse lookup using NETWORK ID
                                    _netIdToDbId[netId] = dbId;

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

        private void OnSyncWorldVehiclesForPlayer([FromSource] Player player)
        {
            if (player == null) return;

            Debug.WriteLine($"[VehicleManager] Syncing {_worldVehicles.Count} world vehicles for player {player.Name}");

            // Send all world vehicles with their DB IDs to client
            foreach (var kvp in _worldVehicles)
            {
                int dbId = kvp.Key;
                int netId = kvp.Value.NetId;

                if (netId != 0)
                {
                    // Send both netId AND dbId to client for proper tracking
                    player.TriggerEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);
                }
            }
        }

        private void OnSaveParkedVehicle([FromSource] Player player, uint modelHash, string vehicleType, string plate,
            float x, float y, float z, float heading, float rx, float ry, float rz, int entityId,
            int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
        {
            Debug.WriteLine($"[VehicleManager] OnSaveParkedVehicle called - Model: {modelHash}, Plate: {plate}");

            // Check if this vehicle matches an existing world vehicle by model and plate
            var existingVehicle = _worldVehicles.FirstOrDefault(kvp =>
                kvp.Value.ModelHash == modelHash &&
                string.Equals(kvp.Value.Plate?.Trim(), plate?.Trim(), StringComparison.OrdinalIgnoreCase)
            );

            if (existingVehicle.Key != 0)
            {
                // This is an EXISTING world vehicle being re-parked (position update)
                Debug.WriteLine($"[VehicleManager] Updating existing world vehicle (DB ID: {existingVehicle.Key})");

                UpdateVehiclePosition(existingVehicle.Key, x, y, z, heading, rx, ry, rz,
                    primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

                // Update in-memory data (but keep the existing entity ID)
                _worldVehicles[existingVehicle.Key].X = x;
                _worldVehicles[existingVehicle.Key].Y = y;
                _worldVehicles[existingVehicle.Key].Z = z;
                _worldVehicles[existingVehicle.Key].Heading = heading;
                _worldVehicles[existingVehicle.Key].PrimaryColor = primaryColor;
                _worldVehicles[existingVehicle.Key].SecondaryColor = secondaryColor;
                _worldVehicles[existingVehicle.Key].CustomPrimaryRGB = customPrimaryRGB;
                _worldVehicles[existingVehicle.Key].CustomSecondaryRGB = customSecondaryRGB;

                player.TriggerEvent("chat:addMessage", new { args = new[] { $"Updated parked {vehicleType} position & colors (ID: {existingVehicle.Key})" } });
            }
            else
            {
                // This is a NEW vehicle being parked for the first time
                Debug.WriteLine($"[VehicleManager] Parking NEW world vehicle - spawning server-authoritative version");

                // Save to database
                _vehicleCommands.SaveVehicleToDatabase(player, modelHash, vehicleType, plate, x, y, z, heading, rx, ry, rz, entityId,
                    primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

                // Spawn a NEW server-authoritative vehicle immediately
                _ = Task.Run(async () =>
                {
                    await Delay(500); // Wait for database insert to complete
                    await SpawnNewWorldVehicle(modelHash, vehicleType, plate, x, y, z, heading,
                        primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);
                });
            }
        }

        // Spawn a server-authoritative vehicle immediately after parking
        private async Task SpawnNewWorldVehicle(uint modelHash, string vehicleType, string plate,
            float x, float y, float z, float heading,
            int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
        {
            try
            {
                Debug.WriteLine($"[VehicleManager] SpawnNewWorldVehicle started for model {modelHash}, plate {plate}");

                // Query database to get the DB ID
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
                            Debug.WriteLine($"[VehicleManager] Found DB ID {dbId} for vehicle");

                            // Check if already spawned (race condition protection)
                            if (_worldVehicles.ContainsKey(dbId))
                            {
                                Debug.WriteLine($"[VehicleManager] World vehicle {dbId} already spawned, skipping.");
                                return;
                            }

                            Debug.WriteLine($"[VehicleManager] Creating NEW server-authoritative vehicle for DB ID {dbId}");

                            // Spawn NEW server-authoritative vehicle
                            float zSpawn = z + 1.0f;
                            int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, modelHash, vehicleType, x, y, zSpawn, heading);

                            Debug.WriteLine($"[VehicleManager] CREATE_VEHICLE_SERVER_SETTER returned entity ID: {veh}");

                            if (veh != 0)
                            {
                                Debug.WriteLine($"[VehicleManager] Successfully created server vehicle (Entity: {veh})");

                                // Set plate
                                if (!string.IsNullOrEmpty(plate))
                                {
                                    SetVehicleNumberPlateText(veh, plate);
                                    Debug.WriteLine($"[VehicleManager] Set plate to: {plate}");
                                }

                                // Apply colors
                                ApplyVehicleColors(veh, primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

                                // Get network ID
                                int netId = NetworkGetNetworkIdFromEntity(veh);
                                Debug.WriteLine($"[VehicleManager] Network ID for entity {veh}: {netId}");

                                if (netId != 0)
                                {
                                    // Register in tracking
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
                                        Heading = heading,
                                        PrimaryColor = primaryColor,
                                        SecondaryColor = secondaryColor,
                                        CustomPrimaryRGB = customPrimaryRGB,
                                        CustomSecondaryRGB = customSecondaryRGB
                                    };

                                    // Add to reverse lookup using NETWORK ID
                                    _netIdToDbId[netId] = dbId;
                                    Debug.WriteLine($"[VehicleManager] ✅ REGISTERED: _netIdToDbId[{netId}] = {dbId}");
                                    Debug.WriteLine($"[VehicleManager] Total tracked network IDs: {_netIdToDbId.Count}");

                                    // Tell clients to register this vehicle with its DB ID
                                    TriggerClientEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);

                                    Debug.WriteLine($"[VehicleManager] Successfully spawned NEW world vehicle (DB ID: {dbId}, Entity: {veh}, NetID: {netId})");
                                }
                                else
                                {
                                    Debug.WriteLine($"[VehicleManager] ERROR: Failed to get network ID for spawned vehicle (DB ID: {dbId})");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[VehicleManager] ERROR: Failed to spawn vehicle for DB ID {dbId}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[VehicleManager] ERROR: Could not find vehicle in database after insert");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[VehicleManager] ERROR: Database query returned null");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error in SpawnNewWorldVehicle: {ex.Message}");
                Debug.WriteLine($"[VehicleManager] Stack trace: {ex.StackTrace}");
            }
        }

        private void OnUnparkVehicle([FromSource] Player player, int vehicleNetId)
        {
            Debug.WriteLine($"[VehicleManager] OnUnparkVehicle called for NetID {vehicleNetId}");

            // Look up the DB ID from the NETWORK ID
            if (_netIdToDbId.TryGetValue(vehicleNetId, out int dbId))
            {
                if (_worldVehicles.TryGetValue(dbId, out WorldVehicleData vehicleData))
                {
                    // Remove from database
                    _vehicleCommands.RemoveVehicleFromDatabase(player, vehicleData.ModelHash, vehicleData.Plate);

                    // Remove from memory tracking
                    _worldVehicles.Remove(dbId);
                    _netIdToDbId.Remove(vehicleNetId);

                    // Get entity from network ID and delete it
                    int vehicleEntity = NetworkGetEntityFromNetworkId(vehicleNetId);
                    if (vehicleEntity != 0 && DoesEntityExist(vehicleEntity))
                    {
                        DeleteEntity(vehicleEntity);
                    }

                    player.TriggerEvent("chat:addMessage", new { args = new[] { $"Unparked vehicle (DB ID: {dbId}, Plate: {vehicleData.Plate})" } });
                    Debug.WriteLine($"[VehicleManager] Unparked vehicle (DB ID: {dbId}, NetID: {vehicleNetId}, Plate: {vehicleData.Plate})");
                }
                else
                {
                    player.TriggerEvent("chat:addMessage", new { args = new[] { "Error: Vehicle data not found in tracking." } });
                    Debug.WriteLine($"[VehicleManager] ERROR: Vehicle NetID {vehicleNetId} mapped to DB ID {dbId}, but no world vehicle data found");
                }
            }
            else
            {
                player.TriggerEvent("chat:addMessage", new { args = new[] { "Error: This is not a parked world vehicle." } });
                Debug.WriteLine($"[VehicleManager] ERROR: Vehicle NetID {vehicleNetId} is not a tracked world vehicle");
            }
        }

        // Handle client query for whether a vehicle is a world vehicle
        private void OnIsWorldVehicle([FromSource] Player player, int vehicleNetId, string callbackEvent)
        {
            try
            {
                Debug.WriteLine($"[VehicleManager] ====== OnIsWorldVehicle Query ======");
                Debug.WriteLine($"[VehicleManager] Client {player.Name} querying if NetID {vehicleNetId} is a world vehicle");
                Debug.WriteLine($"[VehicleManager] Current _netIdToDbId count: {_netIdToDbId.Count}");
                Debug.WriteLine($"[VehicleManager] All tracked network IDs:");
                
                foreach (var kvp in _netIdToDbId)
                {
                    Debug.WriteLine($"[VehicleManager]   NetID {kvp.Key} => DB ID {kvp.Value}");
                }

                // Check if this network ID is tracked as a world vehicle
                if (_netIdToDbId.TryGetValue(vehicleNetId, out int dbId))
                {
                    Debug.WriteLine($"[VehicleManager] ✅ NetID {vehicleNetId} IS a world vehicle (DB ID: {dbId})");
                    player.TriggerEvent(callbackEvent, true, dbId, vehicleNetId);
                }
                else
                {
                    Debug.WriteLine($"[VehicleManager] ❌ NetID {vehicleNetId} is NOT in _netIdToDbId dictionary");
                    player.TriggerEvent(callbackEvent, false, 0, vehicleNetId);
                }
                
                Debug.WriteLine($"[VehicleManager] ====================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VehicleManager] Error in OnIsWorldVehicle: {ex.Message}");
                Debug.WriteLine($"[VehicleManager] Stack trace: {ex.StackTrace}");
                player.TriggerEvent(callbackEvent, false, 0, vehicleNetId);
            }
        }

		private void UpdateVehiclePosition(int dbId, float x, float y, float z, float heading, float rx, float ry, float rz,
	    int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
		{
			string J(double v) => v.ToString(CultureInfo.InvariantCulture);
			string positionJson = $"{{\"x\":{J(x)},\"y\":{J(y)},\"z\":{J(z)},\"heading\":{J(heading)}}}";
			string rotationJson = $"{{\"x\":{J(rx)},\"y\":{J(ry)},\"z\":{J(rz)}}}";

			const string sql = @"
        UPDATE world_vehicles 
        SET position = @position, rotation = @rotation,
            primary_color = @primary_color, secondary_color = @secondary_color,
            custom_primary_rgb = @custom_primary_rgb, custom_secondary_rgb = @custom_secondary_rgb
        WHERE id = @id;";

			var parameters = new Dictionary<string, object>
			{
				["@id"] = dbId,
				["@position"] = positionJson,
				["@rotation"] = rotationJson,
				["@primary_color"] = primaryColor,
				["@secondary_color"] = secondaryColor,
				["@custom_primary_rgb"] = string.IsNullOrEmpty(customPrimaryRGB) ? null : customPrimaryRGB,
				["@custom_secondary_rgb"] = string.IsNullOrEmpty(customSecondaryRGB) ? null : customSecondaryRGB
			};

			Debug.WriteLine($"[VehicleManager] Updating vehicle position and colors for DB ID {dbId}");

			_db.Query(sql, parameters, new Action<dynamic>(_ =>
			{
				Debug.WriteLine($"[VehicleManager] Updated position and colors for vehicle DB ID {dbId}");
			}));
		}

		private async Task RespawnWorldVehicle(int dbId, WorldVehicleData data)
        {
            await Delay(5000);

            try
            {
                if (data.EntityId != 0 && DoesEntityExist(data.EntityId))
                {
                    Debug.WriteLine($"[VehicleManager] Vehicle {dbId} still exists, not respawning.");
                    return;
                }

                float zSpawn = data.Z + 1.0f;

                int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, data.ModelHash, data.VehicleType, data.X, data.Y, zSpawn, data.Heading);

                if (veh != 0)
                {
                    if (!string.IsNullOrEmpty(data.Plate))
                    {
                        SetVehicleNumberPlateText(veh, data.Plate);
                    }

                    // Apply colors
                    ApplyVehicleColors(veh, data.PrimaryColor, data.SecondaryColor, data.CustomPrimaryRGB, data.CustomSecondaryRGB);

                    int netId = NetworkGetNetworkIdFromEntity(veh);
                    if (netId != 0)
                    {
                        _worldVehicles[dbId].NetId = netId;
                        _worldVehicles[dbId].EntityId = veh;

                        // Update reverse lookup with NEW network ID
                        _netIdToDbId[netId] = dbId;

                        TriggerClientEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);
                        Debug.WriteLine($"[VehicleManager] Respawned world vehicle with colors (DB ID: {dbId}, Entity: {veh}, NetID: {netId})");
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

                Debug.WriteLine($"[VehicleManager] MonitorWorldVehicles: Checking {_worldVehicles.Count} vehicles...");

                foreach (var kvp in _worldVehicles.ToList())
                {
                    try
                    {
                        int dbId = kvp.Key;
                        var data = kvp.Value;
                        int entity = data.EntityId;

                        // Only respawn if entity doesn't exist (destroyed)
                        if (entity == 0)
                        {
                            Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {dbId}) has entity ID 0. Respawning...");
                            await RespawnWorldVehicle(dbId, data);
                        }
                        else if (!DoesEntityExist(entity))
                        {
                            Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {dbId}, Entity: {entity}) no longer exists. Respawning...");

                            // Clean up reverse lookup
                            _netIdToDbId.Remove(entity);

                            await RespawnWorldVehicle(dbId, data);
                        }
                        else
                        {
                            // Vehicle exists
                            Debug.WriteLine($"[VehicleManager] World vehicle (DB ID: {dbId}, Entity: {entity}) is healthy.");
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
                primary_color,
                secondary_color,
                custom_primary_rgb,
                custom_secondary_rgb,
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

                            // Skip if already tracked
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

                            int primaryColor = row.primary_color != null ? Convert.ToInt32(row.primary_color) : 0;
                            int secondaryColor = row.secondary_color != null ? Convert.ToInt32(row.secondary_color) : 0;
                            string customPrimaryRGB = row.custom_primary_rgb;
                            string customSecondaryRGB = row.custom_secondary_rgb;

                            float zSpawn = z + 1.0f;

                            int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER, modelHash, vehicleType, x, y, zSpawn, heading);

                            if (veh != 0)
                            {
                                if (!string.IsNullOrEmpty(plate))
                                {
                                    SetVehicleNumberPlateText(veh, plate);
                                }

                                // Apply colors
                                ApplyVehicleColors(veh, primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

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
                                        Heading = heading,
                                        PrimaryColor = primaryColor,
                                        SecondaryColor = secondaryColor,
                                        CustomPrimaryRGB = customPrimaryRGB,
                                        CustomSecondaryRGB = customSecondaryRGB
                                    };

                                    // Add to reverse lookup using NETWORK ID
                                    _netIdToDbId[netId] = dbId;

                                    TriggerClientEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);
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

        private void ApplyVehicleColors(int veh, int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
        {
            // Apply standard colors
            SetVehicleColours(veh, primaryColor, secondaryColor);

            // Apply custom RGB colors if provided
            if (!string.IsNullOrEmpty(customPrimaryRGB))
            {
                var rgb = customPrimaryRGB.Split(',');
                if (rgb.Length == 3)
                {
                    int r = int.Parse(rgb[0]);
                    int g = int.Parse(rgb[1]);
                    int b = int.Parse(rgb[2]);
                    SetVehicleCustomPrimaryColour(veh, r, g, b);
                }
            }

            if (!string.IsNullOrEmpty(customSecondaryRGB))
            {
                var rgb = customSecondaryRGB.Split(',');
                if (rgb.Length == 3)
                {
                    int r = int.Parse(rgb[0]);
                    int g = int.Parse(rgb[1]);
                    int b = int.Parse(rgb[2]);
                    SetVehicleCustomSecondaryColour(veh, r, g, b);
                }
            }

            Debug.WriteLine($"[VehicleManager] Applied colors to vehicle entity {veh} (Primary: {primaryColor}, Secondary: {secondaryColor})");
        }
    }
}