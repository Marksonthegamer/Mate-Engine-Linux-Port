using Lachee.Discord.Attributes;
using UnityEngine;

namespace Lachee.Discord
{
	[System.Obsolete("The word Discord has been removed from types", true)]
	public sealed class DiscordPresence { }

	[System.Serializable]
	public sealed class Presence
	{
		[Header("Basic Details")]

		/// <summary>
		/// The details about the game. Appears underneath the game name
		/// </summary>
		[CharacterLimit(128)]
		[Tooltip("The details about the game")]
		public string details = "Playing a game";

		/// <summary>
		/// The current state of the game (In Game, In Menu etc). Appears next to the party size
		/// </summary>
		[CharacterLimit(128)]
		[Tooltip("The current state of the game (In Game, In Menu). It appears next to the party size.")]
		public string state = "In Game";

		[Header("Time Details")]

		/// <summary>
		/// The time the game started. 0 if the game hasn't started
		/// </summary>
		[Tooltip("The time the game started. Leave as 0 if the game has not yet started.")]
		public Timestamp startTime = 0;

		/// <summary>
		/// The time the game will end in. 0 to ignore endtime.
		/// </summary>
		[Tooltip("Time the game will end. Leave as 0 to ignore it.")]
		public Timestamp endTime = 0;

		[Header("Presentation Details")]

		/// <summary>
		/// The images used for the presence.
		/// </summary>
		[Tooltip("The images used for the presence")]
		public Asset smallAsset;
		public Asset largeAsset;

		[Header("Button Details")]

		/// <summary>
		/// The buttons used for the presence.
		/// </summary>
		[Tooltip("The buttons used for the presence")]
		public Button[] buttons;

		[Header("Party Details")]

		/// <summary>
		/// The current party
		/// </summary>
		[Tooltip("The current party. Identifier must not be empty")]
		public Party party = new Party("", 0, 0);

		/// <summary>
		/// The current secrets for the join / spectate feature.
		/// </summary>
		[Tooltip("The current secrets for the join / spectate feature.")]
		public Secrets secrets = new Secrets();

		/// <summary>
		/// Creates a new Presence object
		/// </summary>
		public Presence() { }

		/// <summary>
		/// Creats a new Presence object, copying values of the Rich Presence
		/// </summary>
		/// <param name="presence">The rich presence, often received by discord.</param>
		public Presence(DiscordRPC.BaseRichPresence presence)
		{
			if (presence != null)
			{
				this.state = presence.State;
				this.details = presence.Details;

				this.party = presence.HasParty() ? new Party(presence.Party) : new Party();
				this.secrets = presence.HasSecrets() ? new Secrets(presence.Secrets) : new Secrets();

				if (presence.HasAssets())
				{
					this.smallAsset = new Asset()
					{
						image = presence.Assets.SmallImageKey,
						tooltip = presence.Assets.SmallImageText,
						snowflake = presence.Assets.SmallImageID.GetValueOrDefault(0)
					};


					this.largeAsset = new Asset()
					{
						image = presence.Assets.LargeImageKey,
						tooltip = presence.Assets.LargeImageText,
						snowflake = presence.Assets.LargeImageID.GetValueOrDefault(0)
					};
				}
				else
				{
					this.smallAsset = new Asset();
					this.largeAsset = new Asset();
				}

				if (presence.HasTimestamps())
				{
					//This could probably be made simpler
					this.startTime = presence.Timestamps.Start.HasValue ? new Timestamp((long)presence.Timestamps.StartUnixMilliseconds.Value) : Timestamp.Invalid;
					this.endTime = presence.Timestamps.End.HasValue ? new Timestamp((long)presence.Timestamps.EndUnixMilliseconds.Value) : Timestamp.Invalid;
				}
			}
			else
			{
				this.state = "";
				this.details = "";
				this.party = new Party();
				this.secrets = new Secrets();
				this.smallAsset = new Asset();
				this.largeAsset = new Asset();
				this.startTime = Timestamp.Invalid;
				this.endTime = Timestamp.Invalid;
			}

			this.buttons = new Button[0];
		}

		public Presence(DiscordRPC.RichPresence presence)
            : this((DiscordRPC.BaseRichPresence)presence)
        {
			if (presence != null)
			{
				if (presence.HasButtons())
				{
					this.buttons = new Button[presence.Buttons.Length];

					for (int i = 0; i < presence.Buttons.Length; i++)
					{
						this.buttons[i] = new Button()
						{
							label = presence.Buttons[i].Label,
							url = presence.Buttons[i].Url
						};
					}
				}
				else
				{
					this.buttons = new Button[0];
				}
			}
         }

        /// <summary>
        /// Converts this object into a new instance of a rich presence, ready to be sent to the discord client.
        /// </summary>
        /// <returns>A new instance of a rich presence, ready to be sent to the discord client.</returns>
        public DiscordRPC.RichPresence ToRichPresence()
		{
			var presence = new DiscordRPC.RichPresence();
			presence.State = this.state;
			presence.Details = this.details;

			presence.Party = !this.party.IsEmpty() ? this.party.ToRichParty() : null;
			presence.Secrets = !this.secrets.IsEmpty() ? this.secrets.ToRichSecrets() : null;

			if ((smallAsset != null && !smallAsset.IsEmpty()) || (largeAsset != null && !largeAsset.IsEmpty()))
			{
				presence.Assets = new DiscordRPC.Assets()
				{
					SmallImageKey = smallAsset.image,
					SmallImageText = smallAsset.tooltip,

					LargeImageKey = largeAsset.image,
					LargeImageText = largeAsset.tooltip
				};
			}

			if (startTime.IsValid() || endTime.IsValid())
			{
				presence.Timestamps = new DiscordRPC.Timestamps();
				if (startTime.IsValid()) presence.Timestamps.Start = startTime.GetDateTime();
				if (endTime.IsValid()) presence.Timestamps.End = endTime.GetDateTime();
			}

			if (buttons.Length > 0)
			{
				presence.Buttons = new DiscordRPC.Button[buttons.Length];

				for (int i = 0; i < buttons.Length; i++)
				{
					presence.Buttons[i] = new DiscordRPC.Button
					{
						Label = buttons[i].label,
						Url = buttons[i].url
					};
				}
			}

			return presence;
		}

		public static explicit operator DiscordRPC.RichPresence(Presence presence)
		{
			return presence.ToRichPresence();
		}

		public static explicit operator Presence(DiscordRPC.RichPresence presence)
		{
			return new Presence(presence);
		}
	}
}