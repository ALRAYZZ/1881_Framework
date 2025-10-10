using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using VehicleManager.Server.Commands;
using VehicleManager.Server.Interfaces;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server
{
    public class ServerMain : BaseScript
    {
        // Exports from DatabaseCore
        private dynamic _db;

        // Track net IDs of world vehicles (per player re-syncs)
        private readonly List<int> _worldVehicleNetIds = new List<int>();

        private VehicleCommands _vehicleCommands;

        public ServerMain()
        {
            IVehicleManager vehicleManager = new Services.VehicleManager();

            _db = Exports["Database"];

            // Pass db to commands
            _vehicleCommands = new VehicleCommands(vehicleManager, Players, _db);

            // Register server event
            EventHandlers["VehicleManager:Server:SaveParkedVehicle"] += new Action<Player, uint, string, float, float, float, float, float, float, float>(OnSaveParkedVehicle);

            Debug.WriteLine("[VehicleManager] Server initialized.");

            _ = SpawnWorldVehiclesAsync();
        }

        private void OnSaveParkedVehicle([FromSource] Player player, uint modelHash, string plate, float x, float y, float z, float heading, float rx, float ry, float rz)
        {
            _vehicleCommands.SaveVehicleToDatabase(player, modelHash, plate, x, y, z, heading, rx, ry, rz);
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
                                _worldVehicleNetIds.Add(netId);

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