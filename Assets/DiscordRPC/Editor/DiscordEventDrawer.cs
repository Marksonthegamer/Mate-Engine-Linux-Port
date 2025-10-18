using UnityEditor;
using UnityEngine;

namespace Lachee.Discord.Editor
{
	[CustomPropertyDrawer(typeof(Event))]
	internal sealed class DiscordEventDrawer : PropertyDrawer
	{
		private bool INCLUDE_NONE = false;

		public const float keySize = 150;
		public const int lines = 3;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			Event propval = (Event)property.intValue;
			Event newval = Event.None;

			Rect buttonPos;
			int offset = INCLUDE_NONE ? 0 : 1;
			float buttonWidth = (position.width - EditorGUIUtility.labelWidth) / (4 - offset);

			EditorGUI.LabelField(new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height), label);
			EditorGUI.BeginProperty(position, label, property);
			{

				if (INCLUDE_NONE)
				{
					buttonPos = new Rect(position.x + EditorGUIUtility.labelWidth + buttonWidth * 0, position.y, buttonWidth, position.height);
					if (GUI.Toggle(buttonPos, propval == Event.None, "None", EditorStyles.miniButtonLeft))
						newval = Event.None;
				}

				buttonPos = new Rect(position.x + EditorGUIUtility.labelWidth + buttonWidth * (1 - offset), position.y, buttonWidth, position.height);
				if (GUI.Toggle(buttonPos, (propval & Event.Join) == Event.Join, "Join", INCLUDE_NONE ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft))
					newval |= Event.Join;

				buttonPos = new Rect(position.x + EditorGUIUtility.labelWidth + buttonWidth * (2 - offset), position.y, buttonWidth, position.height);
				if (GUI.Toggle(buttonPos, (propval & Event.Spectate) == Event.Spectate, "Spectate", EditorStyles.miniButtonMid))
					newval |= Event.Spectate;

				buttonPos = new Rect(position.x + EditorGUIUtility.labelWidth + buttonWidth * (3 - offset), position.y, buttonWidth, position.height);
				if (GUI.Toggle(buttonPos, (propval & Event.JoinRequest) == Event.JoinRequest, "Invites", EditorStyles.miniButtonRight))
					newval |= Event.JoinRequest;

				property.intValue = (int)newval;
			}
			EditorGUI.EndProperty();

		}
	}
}
