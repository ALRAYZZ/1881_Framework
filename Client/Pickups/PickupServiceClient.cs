using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace armory.Client.Pickups
{
	public class PickupServiceClient : BaseScript
	{
		private readonly Dictionary<int, PickupClient> _weaponPickups = new Dictionary<int, PickupClient>();
		private readonly HashSet<int> _pendingCollect = new HashSet<int>();

		public PickupServiceClient()
		{
			EventHandlers["armory:CreateWeaponPickup"] += new Action<int, string, int, float, float, float>(OnCreateWeaponPickup);
			EventHandlers["armory:RemoveWeaponPickup"] += new Action<int>(OnRemoveWeaponPickup);
			EventHandlers["armory:RemoveAllWeaponPickups"] += new Action(OnRemoveAllWeaponPickups);
			EventHandlers["onResourceStop"] += new Action<string>(OnResourceStop);

			Tick += ProximityTick;
		}

		// Creates a weapon pickup and calls the spawn model function
		private void OnCreateWeaponPickup(int id, string weapon, int ammo, float x, float y, float z)
		{
			uint weaponHash = (uint)GetHashKey(weapon);
			if (!IsWeaponValid(weaponHash))
			{
				Debug.WriteLine($"[Armory|Client] Ignoring invalid weapon pickup '{weapon}' (id={id}).");
				return;
			}

			var pickup = new PickupClient { Id = id, WeaponName = weapon, Ammo = ammo, Pos = new Vector3(x, y, z) };
			_weaponPickups[id] = pickup;
			_ = SpawnWorldModel(pickup);
		}
		private void OnRemoveWeaponPickup(int id)
		{
			if (_weaponPickups.TryGetValue(id, out var pickup))
			{
				DeleteObjectForPickup(pickup);
				_weaponPickups.Remove(id);
			}
			_pendingCollect.Remove(id);
		}
		private void OnRemoveAllWeaponPickups()
		{
			foreach (var pickup in _weaponPickups.Values)
			{
				DeleteObjectForPickup(pickup);
			}
			_weaponPickups.Clear();
			_pendingCollect.Clear();
		}
		// Spawns the world model for the weapon pickup
		private async Task SpawnWorldModel(PickupClient pickup)
		{
			uint weaponHash = (uint)GetHashKey(pickup.WeaponName);
			if (!IsWeaponValid(weaponHash)) return;

			uint model = (uint)GetWeapontypeModel(weaponHash);
			if (!IsModelValid(model)) return;

			RequestModel(model);
			while (!HasModelLoaded(model)) await Delay(0);

			int obj = CreateObjectNoOffset(model, pickup.Pos.X, pickup.Pos.Y, pickup.Pos.Z + 0.05f, false, false, false);
			if (obj != 0)
			{
				SetEntityAsMissionEntity(obj, true, true);
				PlaceObjectOnGroundProperly(obj);
				FreezeEntityPosition(obj, true);
				pickup.ObjectHandle = obj;
			}
			SetModelAsNoLongerNeeded(model);
		}
		// Checks player proximity and draws markers
		private async Task ProximityTick()
		{
			if (_weaponPickups.Count == 0)
			{
				await Delay(500);
				return;
			}

			var playerPed = PlayerPedId();
			var playerPos = GetEntityCoords(playerPed, true);

			foreach (var pickup in _weaponPickups.Values.ToArray())
			{
				DrawMarker(1, pickup.Pos.X, pickup.Pos.Y, pickup.Pos.Z - 1.0f,
					0f, 0f, 0f, 0f, 0f, 0f,
					0.4f, 0.4f, 0.4f,
					0, 180, 255, 180,
					false, true, 2, false, null, null, false);

				float dist = Vector3.Distance(playerPos, pickup.Pos);
				const float radius = 1.5f;

				if (dist <= radius && !_pendingCollect.Contains(pickup.Id))
				{
					_pendingCollect.Add(pickup.Id);
					Debug.WriteLine($"[Armory|Client] Attempting to collect pickup id={pickup.Id}, distance={dist}");
					TriggerServerEvent("armory:TryCollectWeaponPickup", pickup.Id);
				}
			}

			await Delay(0);
		}
		// Cleans up on resource stop
		private void OnResourceStop(string name)
		{
			if (GetCurrentResourceName() != name) return;

			foreach (var pickup in _weaponPickups.Values)
				DeleteObjectForPickup(pickup);

			_weaponPickups.Clear();
			_pendingCollect.Clear();
		}
		// Deletes the object model associated with a pickup
		private void DeleteObjectForPickup(PickupClient pickup)
		{
			if (pickup.ObjectHandle != 0 && DoesEntityExist(pickup.ObjectHandle))
			{
				int handle = pickup.ObjectHandle;
				DeleteObject(ref handle);
				pickup.ObjectHandle = 0;
			}
		}
	}
}
