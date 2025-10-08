using CitizenFX.Core;

namespace armory.Server
{
	/// Abstraction to send chat replies to a source (player or console) with consistent colors and formatting.
	public interface IChatMessenger
	{
		/// Sends a chat message to the invoker (or logs to console if src == 0).
		void Reply(int src, string message, int r = 0, int g = 200, int b = 255, string prefix = "[Armory]");

		/// Sends an informational message with the default color.
		void Info(int src, string message);

		/// Sends a warning message with a warning color.
		void Warn(int src, string message);

		/// Sends an error message with an error color.
		void Error(int src, string message);
	}

	/// Default chat messenger implementation using chat:addMessage and Debug.WriteLine for console.
	public sealed class ChatMessenger : IChatMessenger
	{
		private readonly PlayerList _players;
		private readonly string _defaultPrefix;
		private static readonly int[] InfoColor = { 0, 200, 255 };
		private static readonly int[] WarnColor = { 255, 210, 0 };
		private static readonly int[] ErrorColor = { 255, 80, 80 };

		/// Creates a chat messenger using the provided PlayerList and default prefix.
		public ChatMessenger(PlayerList players, string defaultPrefix = "[Armory]")
		{
			_players = players ?? throw new System.ArgumentNullException(nameof(players));
			_defaultPrefix = string.IsNullOrWhiteSpace(defaultPrefix) ? "[Armory]" : defaultPrefix;
		}

		public void Reply(int src, string message, int r = 0, int g = 200, int b = 255, string prefix = "[Armory]")
		{
			if (src == 0)
			{
				Debug.WriteLine($"{prefix} {message}");
				return;
			}

			Player invoker = null;
			try { invoker = _players[src]; } catch { invoker = null; }

			if (invoker == null)
			{
				Debug.WriteLine($"{prefix} (to {src}) {message}");
				return;
			}

			invoker.TriggerEvent("chat:addMessage", new
			{
				color = new[] { r, g, b },
				args = new[] { prefix, message }
			});
		}

		public void Info(int src, string message) => Reply(src, message, InfoColor[0], InfoColor[1], InfoColor[2], _defaultPrefix);

		public void Warn(int src, string message) => Reply(src, message, WarnColor[0], WarnColor[1], WarnColor[2], _defaultPrefix);

		public void Error(int src, string message) => Reply(src, message, ErrorColor[0], ErrorColor[1], ErrorColor[2], _defaultPrefix);
	}
}
