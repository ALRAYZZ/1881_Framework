using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using System.Globalization;
using System.Linq;

namespace PedManager.Server
{
    public class ServerMain : BaseScript
    {
        private readonly IPedService _pedService;

        public ServerMain()
        {
            var db = Exports["Database"];
            _pedService = new PedService(db);

            Debug.WriteLine("[PedManager] Server initialized.");

			// USAGE: /setped [playerId] [modelName] POTENTIAL REFACTOR TO A COMMAND HANDLER WHEN WE GROW
			RegisterCommand("setped", new Action<int, List<object>, string>(OnSetPedCommand), true);

            // Back-compat: PlayerCore can tell PedManager to apply a specific ped (no persist)
            EventHandlers["PedManager:Server:ApplyPed"] += new Action<string, string>((serverId, model) =>
            {
                var player = Players.FirstOrDefault(p => p.Handle == serverId);
                if (player == null)
                {
                    Debug.WriteLine($"[PedManager] ApplyPed: player '{serverId}' not found.");
                    return;
                }

                _pedService.SetPedFor(player, model, false); // apply only
                Debug.WriteLine($"[PedManager] Applied ped '{model}' for {serverId} (from PlayerCore).");
            });

            // Preferred: PlayerCore requests PedManager to resolve+apply from DB
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
        }

        private void OnSetPedCommand(int src, List<object> args, string raw)
        {
			// Accepts:
            // - /setped <serverId> <ped_name>
            // - /setped <ped_name> for self
			if (args == null || args.Count == 0)
            {
                Reply(src, "Usage: /setped <serverId> <ped_name> OR /setped <ped_name>");
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
                    Reply(src, "Console must specify");
                    return;
                }

                targetId = src;
				pedName = string.Join(" ", args.Skip(1).Select(a => a?.ToString() ?? string.Empty)).Trim().Trim('"');
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