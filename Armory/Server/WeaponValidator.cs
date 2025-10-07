using System;
using System.Collections.Generic;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace armory.Server
{
	/// <summary>
	/// Validates and normalizes weapon names against a server-side allowlist.
	/// </summary>
	public class WeaponValidator
	{
		// Server-authoritative ALLOWLIST of valid weapon names
		// Update list as needed for permitted weapons
		private static readonly HashSet<string> ValidWeapons = new HashSet<string>
		{

			// Melee
			"WEAPON_KNIFE","WEAPON_NIGHTSTICK","WEAPON_HAMMER","WEAPON_BAT","WEAPON_GOLFCLUB","WEAPON_CROWBAR",
			"WEAPON_BOTTLE","WEAPON_DAGGER","WEAPON_HATCHET","WEAPON_MACHETE","WEAPON_SWITCHBLADE",
			"WEAPON_FLASHLIGHT","WEAPON_BATTLEAXE","WEAPON_POOLCUE","WEAPON_WRENCH","WEAPON_STONE_HATCHET",

            // Handguns
            "WEAPON_PISTOL","WEAPON_PISTOL_MK2","WEAPON_COMBATPISTOL","WEAPON_APPISTOL","WEAPON_STUNGUN",
			"WEAPON_PISTOL50","WEAPON_SNSPISTOL","WEAPON_SNSPISTOL_MK2","WEAPON_HEAVYPISTOL","WEAPON_VINTAGEPISTOL",
			"WEAPON_MARKSMANPISTOL","WEAPON_REVOLVER","WEAPON_REVOLVER_MK2","WEAPON_DOUBLEACTION",
			"WEAPON_NAVYREVOLVER","WEAPON_CERAMICPISTOL","WEAPON_FLAREGUN", "WEAPON_PISTOLXM3",

            // SMGs
            "WEAPON_MICROSMG","WEAPON_SMG","WEAPON_SMG_MK2","WEAPON_ASSAULTSMG","WEAPON_COMBATPDW",
			"WEAPON_MACHINEPISTOL","WEAPON_MINISMG","WEAPON_GUSENBERG",

            // Shotguns
            "WEAPON_PUMPSHOTGUN","WEAPON_PUMPSHOTGUN_MK2","WEAPON_SAWNOFFSHOTGUN","WEAPON_ASSAULTSHOTGUN",
			"WEAPON_BULLPUPSHOTGUN","WEAPON_HEAVYSHOTGUN","WEAPON_MUSKET","WEAPON_DBSHOTGUN","WEAPON_AUTOSHOTGUN",
			"WEAPON_COMBATSHOTGUN",

            // Rifles
            "WEAPON_ASSAULTRIFLE","WEAPON_ASSAULTRIFLE_MK2","WEAPON_CARBINERIFLE","WEAPON_CARBINERIFLE_MK2",
			"WEAPON_ADVANCEDRIFLE","WEAPON_SPECIALCARBINE","WEAPON_SPECIALCARBINE_MK2",
			"WEAPON_BULLPUPRIFLE","WEAPON_BULLPUPRIFLE_MK2","WEAPON_COMPACTRIFLE","WEAPON_MILITARYRIFLE",
			"WEAPON_TACTICALRIFLE",

            // LMGs
            "WEAPON_MG","WEAPON_COMBATMG","WEAPON_COMBATMG_MK2",

            // Snipers
            "WEAPON_SNIPERRIFLE","WEAPON_HEAVYSNIPER","WEAPON_HEAVYSNIPER_MK2","WEAPON_MARKSMANRIFLE","WEAPON_MARKSMANRIFLE_MK2",

            // Heavy / Launchers
            "WEAPON_GRENADELAUNCHER","WEAPON_GRENADELAUNCHER_SMOKE","WEAPON_RPG","WEAPON_MINIGUN",
			"WEAPON_FIREWORK","WEAPON_RAILGUN","WEAPON_HOMINGLAUNCHER","WEAPON_COMPACTLAUNCHER",

            // Throwables
            "WEAPON_GRENADE","WEAPON_STICKYBOMB","WEAPON_PROXMINE","WEAPON_BZDGAS","WEAPON_MOLOTOV",
			"WEAPON_SMOKEGRENADE","WEAPON_TEARGAS","WEAPON_FLARE","WEAPON_SNOWBALL"
		};

		/// <summary>
		/// Validates and normalizes an input weapon name to the "WEAPON_*" form and returns its hash.
		/// </summary>
		/// <param name="input">Input weapon name (with or without WEAPON_ prefix).</param>
		/// <param name="normalized">Normalized name if valid.</param>
		/// <param name="hash">Weapon hash if valid.</param>
		/// <returns>True if the name is valid and normalized; otherwise false.</returns>
		public static bool TryNormalizeWeaponName(string input, out string normalized, out uint hash)
		{
			normalized = null;
			hash = 0;
			if (string.IsNullOrWhiteSpace(input)) return false;

			var candidate = input.ToUpperInvariant();
			if (!candidate.StartsWith("WEAPON_"))
				candidate = "WEAPON_" + candidate;

			if (!ValidWeapons.Contains(candidate))
				return false;

			hash = (uint)GetHashKey(candidate);
			if (hash == 0) return false;

			normalized = candidate;
			return true;
		}

		// Basic getter for all valid weapons
		public static List<string> GetAllWeapons()
		{
			return new List<string>(ValidWeapons);
		}
	}
}
