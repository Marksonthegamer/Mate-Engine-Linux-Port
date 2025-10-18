using UnityEngine;

namespace Lachee.Discord
{
	/// <summary>
	/// Events to receive from Discord
	/// </summary>
	[System.Flags]
	public enum Event
	{
		/// <summary>
		/// No Events
		/// </summary>
		None = 0,

		/// <summary>
		/// Listen to Spectate Events
		/// </summary>
		Spectate = 1,

		/// <summary>
		/// Listen to Join Events
		/// </summary>
		Join = 2,

		/// <summary>
		/// Listen for Join Requests
		/// </summary>
		JoinRequest = 4
	}

	public static class EventExtension
	{
		public static DiscordRPC.EventType ToDiscordRPC(this Event ev)
		{
			return (DiscordRPC.EventType)((int)ev);
		}

		public static Event ToUnity(this DiscordRPC.EventType type)
		{
			return (Event)((int)type);
		}
	}
}