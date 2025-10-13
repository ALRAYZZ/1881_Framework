using System;
using static CitizenFX.Core.Native.API;
using CitizenFX.Core;

namespace AdminManager.Client.Helpers
{
	internal static class ChatHelper
	{
		public static void Print(string message)
		{
			try
			{
				TriggerEvent("chat:addMessage", new
				{
					color = new[] { 255, 200, 0 },
					args = new[] { "[AdminManager]", message }
				});
			}
			catch { Debug.WriteLine("[AdminManager] Failed to print chat message."); }
		}

		public static void PrintInfo(string message) => Print($"~b~{message}~s~");
		public static void PrintSuccess(string message) => Print($"~g~{message}~s~");
		public static void PrintError(string message) => Print($"~r~{message}~s~");
	}
}
