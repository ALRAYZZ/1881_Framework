using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using System.Globalization;
using System.Linq;
using PedManager.Server.Services;
using PedManager.Server.Interfaces;

namespace PedManager.Server
{
    public class ServerMain : BaseScript
    {
        private readonly IPedService _pedService;
        private readonly PersistentPedService _persistentPedService;

        public ServerMain()
        {
            var db = Exports["Database"];
            _persistentPedService = new PersistentPedService(db, EventHandlers);
            _pedService = new PedService(db);

            Debug.WriteLine("[PedManager] Server initialized.");

            RegisterCommand("setped", new Action<int, List<object>, string>(OnSetPedCommand), true);
            RegisterCommand("persistped", new Action<int, List<object>, string>(OnPersistPedCommand), true);

            EventHandlers["PedManager:Server:ApplyPed"] += new Action<string, string>((serverId, model) =>
            {
                var player = Players.FirstOrDefault(p => p.Handle == serverId);
                if (player == null)
                {
                    Debug.WriteLine($"[PedManager] ApplyPed: player '{serverId}' not found.");
                    return;
                }

                _pedService.SetPedFor(player, model, false);
                Debug.WriteLine($"[PedManager] Applied ped '{model}' for {serverId} (from PlayerCore).");
            });

            EventHandlers["PedManager:Server:ApplyInitialPed"] += new Action<string>((serverId) =>
            {
                var player = Players.FirstOrDefault(p => p.Handle == serverId);
                if (player == null)
                {
                    Debug.WriteLine($"[PedManager] ApplyInitialPed: player '{serverId}' not found.");
                    return;
                }

                _pedService.ApplyInitialPedFor(player);
            });

            EventHandlers["PedManager:Server:SetPed"] += new Action<string, string>((serverId, model) =>
            {
                var player = Players.FirstOrDefault(p => p.Handle == serverId);
                if (player == null)
                {
                    Debug.WriteLine($"[PedManager] SetPed: player '{serverId}' not found.");
                    return;
                }

                _pedService.SetPedFor(player, model);
                Debug.WriteLine($"[PedManager] Set ped '{model}' for {serverId} (from event).");

                player.TriggerEvent("PedManager:Client:ApplyPedNow", model);
            });

            EventHandlers["PedManager:Server:OpenPedMenu"] += new Action<int>((src) =>
            {
                Debug.WriteLine($"[PedManager] Opening ped menu for {src}");
                var peds = _pedService.GetAllAvailablePeds();
                Debug.WriteLine($"[PedManager] Retrieved {peds?.Count ?? 0} peds from service");
                var player = Players.FirstOrDefault(p => p.Handle == src.ToString());
                if (player != null)
                {
                    Debug.WriteLine($"[PedManager] Sending ped list to player {src}");
                    player.TriggerEvent("UI:OpenPedMenu", peds.Cast<object>().ToList());
                }
                else
                {
                    Debug.WriteLine($"[PedManager] Player {src} not found when trying to open ped menu.");
                }
            });

            // Handle client callback with spawn position
            EventHandlers["PedManager:Server:SpawnPersistentPed"] += new Action<Player, string, float, float, float, float>(OnSpawnPersistentPedCallback);

            EventHandlers["PedManager:Server:UnpersistPedById"] += new Action<Player, int>(OnUnpersistPedRequest);

            // If PlayerCore already spawned with the correct model (from state), skip re-applying to avoid double swap
            EventHandlers["PlayerCore:Server:OnSpawned"] += new Action<Player>(OnPlayerCoreSpawned);
        }

        private void OnPlayerCoreSpawned([FromSource] Player player)
        {
            if (player == null)
            {
                Debug.WriteLine("[PedManager] OnPlayerCoreSpawned: player is null.");
                return;
            }

            try
            {
                var existing = player.State.Get("pedModel") as string;
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    Debug.WriteLine($"[PedManager] OnSpawned: ped model already known ('{existing}'), skipping re-apply.");
                    return;
                }
            }
            catch { /* ignore */ }

            Debug.WriteLine($"[PedManager] OnPlayerCoreSpawned: resolving/applying initial ped for {player.Name} ({player.Handle}).");
            _pedService.ApplyInitialPedFor(player);
        }

        private void OnSetPedCommand(int src, List<object> args, string raw)
        {
            // Accepts:
            // - /setped with no args to open ped menu for self
            // - /setped <serverId> <ped_name>
            // - /setped <ped_name> for self
            if (args == null || args.Count == 0)
            {
                TriggerEvent("PedManager:Server:OpenPedMenu", src);
                return;
            }

            var firstArg = Convert.ToString(args[0], CultureInfo.InvariantCulture);
            int targetId;
            string pedName;

            if (int.TryParse(firstArg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                // First arg is a number, assume targetId
                targetId = parsedId;

                if (args.Count < 2)
                {
                    Reply(src, "Missing ped name.");
                    return;
                }

                pedName = string.Join(" ", args.Skip(1).Select(a => a?.ToString() ?? string.Empty)).Trim().Trim('"');
            }
            else
            {
                // First arg is not a number, assume self and ped name is all args
                if (src == 0)
                {
                    Reply(src, "Console must specify <serverId> and <ped_name>.");
                    return;
                }

                targetId = src;
                pedName = string.Join(" ", args.Select(a => a?.ToString() ?? string.Empty)).Trim().Trim('"');
            }

            if (string.IsNullOrWhiteSpace(pedName))
            {
                Reply(src, "Invalid ped name.");
                return;
            }

            var target = Players.FirstOrDefault(p => p.Handle.ToString(CultureInfo.InvariantCulture) == targetId.ToString(CultureInfo.InvariantCulture));
            if (target == null)
            {
                Reply(src, $"Player with id {targetId} not found.");
                return;
            }

            _pedService.SetPedFor(target, pedName); // persists
            Reply(src, $"Set ped for player {targetId} to {pedName}.");
        }

        private void OnPersistPedCommand(int src, List<object> args, string raw)
        {
            // Usage: /persistped <pedmodel>
            if (src == 0)
            {
                Debug.WriteLine("[PedManager] persistped command cannot be used from console.");
                return;
            }

            if (args == null || args.Count == 0)
            {
                Reply(src, "Usage: /persistped <pedmodel>");
                return;
            }

            var pedModel = string.Join(" ", args.Select(a => a?.ToString() ?? string.Empty)).Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(pedModel))
            {
                Reply(src, "Invalid ped model.");
                return;
            }

            var player = Players.FirstOrDefault(p => p.Handle == src.ToString());
            if (player == null)
            {
                Debug.WriteLine($"[PedManager] Player {src} not found for persistped command.");
                return;
            }

            // Request spawn position from client (player's position + forward offset)
            player.TriggerEvent("PedManager:Client:RequestPersistentPedSpawn", pedModel);
        }

        private void OnUnpersistPedRequest([FromSource] Player player, int dbId)
        {
            _persistentPedService.RemovePersistentPedByNetId(dbId, (success) =>
            {
                if (success)
                {
                    Reply(int.Parse(player.Handle), "Persistent ped deleted successfully.");
                    TriggerClientEvent("PedManager:Client:DeletePersistentPedById", dbId);
                }
                else
                {
                    Reply(int.Parse(player.Handle), "Failed to delete persistent ped. Make sure you are close enough.");
                }
            });
        }

        private void OnSpawnPersistentPedCallback([FromSource] Player player, string pedModel, float x, float y, float z, float heading)
        {
            if (player == null)
            {
                Debug.WriteLine("[PedManager] OnSpawnPersistentPedCallback: player is null.");
                return;
            }

            int src = int.Parse(player.Handle);
            Debug.WriteLine($"[PedManager] Spawning persistent ped '{pedModel}' at ({x}, {y}, {z}, {heading}) by player {src}");

            _persistentPedService.AddPersistentPed(pedModel, x, y, z, heading, (success, insertedId) =>
            {
                if (success)
                {
                    Reply(src, $"Persistent ped '{pedModel}' spawned successfully.");
                    
                    // Broadcast to all clients to spawn the new ped
                    TriggerClientEvent("PedManager:Client:SpawnSinglePersistentPed", pedModel, x, y, z, heading, insertedId);
                }
                else
                {
                    Reply(src, $"Failed to spawn persistent ped '{pedModel}'.");
                }
            });
        }

        private void Reply(int src, string message)
        {
            if (src == 0)
            {
                Debug.WriteLine($"[PedManager] {message}");
                return;
            }

            var invoker = Players.FirstOrDefault(p => p.Handle.ToString(CultureInfo.InvariantCulture) == src.ToString(CultureInfo.InvariantCulture));
            if (invoker != null)
            {
                invoker.TriggerEvent("chat:addMessage", new
                {
                    color = new[] { 0, 200, 255 },
                    args = new[] { "[PedManager]", message }
                });
            }
        }
    }
}