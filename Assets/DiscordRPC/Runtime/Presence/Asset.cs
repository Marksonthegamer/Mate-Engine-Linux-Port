using Lachee.Discord.Attributes;
using UnityEngine;

namespace Lachee.Discord
{
	[System.Obsolete("The word Discord has been removed from types", true)]
	public sealed class DiscordAsset { }

	[System.Serializable]
	public sealed class Asset
	{
		/// <summary>
		/// The key of the image to be displayed.
		/// <para>Max 32 Bytes.</para>
		/// </summary>
		[CharacterLimit(256, enforce = true)]
		[Tooltip("The key or URL of the image to be displayed in the large square.")]
		public string image;

		/// <summary>
		/// The tooltip of the image.
		/// <para>Max 128 Bytes.</para>
		/// </summary>
		[CharacterLimit(128, enforce = true)]
		[Tooltip("The tooltip of the image.")]
		public string tooltip;

		[Tooltip("Snowflake ID of the image.")]
		public ulong snowflake;

		/// <summary>
		/// Is the asset object empty?
		/// </summary>
		/// <returns></returns>
		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(image) && string.IsNullOrEmpty(tooltip);
		}
	}
}