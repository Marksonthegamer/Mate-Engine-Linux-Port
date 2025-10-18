using UnityEngine;

namespace Lachee.Discord
{
	[System.Obsolete("The word Discord has been removed from types", true)]
    public sealed class DiscordButton { }

	[System.Serializable]
	public sealed class Button
	{
		/// <summary>
		/// The text on the button to be displayed
		/// </summary>
		[Tooltip("The label on the button to be displayed")]
		public string label;

		/// <summary>
		/// The tooltip of the image.
		/// </summary>]
		[Tooltip("The URL on the button to be displayed")]
		public string url;

		/// <summary>
		/// Is the asset object empty?
		/// </summary>
		/// <returns></returns>
		public bool IsEmpty()
		{
			return string.IsNullOrEmpty(label) && string.IsNullOrEmpty(url);
		}
	}
}