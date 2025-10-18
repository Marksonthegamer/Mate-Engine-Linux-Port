using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Thry.ThryEditor
{
    public class PropertyValueAction
    {
        public string value;
        public DefineableAction[] actions;

        public bool Execute(MaterialProperty p, Material[] targets)
        {
            if (
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Float && p.floatValue.ToString() == value) ||
#if UNITY_2022_1_OR_NEWER
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Int && p.intValue.ToString() == value) ||
#endif
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Range && p.floatValue.ToString() == value) ||
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Color && p.colorValue.ToString() == value) ||
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Vector && p.vectorValue.ToString() == value) ||
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture && ((p.textureValue == null) == (value == "0"))) ||
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture && ((p.textureValue != null) == (value == "1"))) ||
                (p.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture && (p.textureValue != null && p.textureValue.name == value))
            )
            {
                ;
                foreach (DefineableAction a in actions)
                    a.Perform(targets);
                return true;
            }
            return false;
        }

        private static PropertyValueAction ParseForThryParser(string s)
        {
            return Parse(s);
        }

        // value,property1=value1,property2=value2
        public static PropertyValueAction Parse(string s)
        {
            s = s.Trim();
            string[] valueAndActions = s.Split(new string[] { "=>" }, System.StringSplitOptions.RemoveEmptyEntries);
            if (valueAndActions.Length > 1)
            {
                PropertyValueAction propaction = new PropertyValueAction();
                propaction.value = valueAndActions[0];
                List<DefineableAction> actions = new List<DefineableAction>();
                string[] actionStrings = valueAndActions[1].Split(';');
                for (int i = 0; i < actionStrings.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(actionStrings[i]))
                        continue;
                    actions.Add(DefineableAction.Parse(actionStrings[i]));
                }
                propaction.actions = actions.ToArray();
                return propaction;
            }
            return null;
        }

        private static PropertyValueAction[] ParseToArrayForThryParser(string s)
        {
            return ParseToArray(s);
        }

        public static PropertyValueAction[] ParseToArray(string s)
        {
            //s := 0=>p1=v1;p2=v2;1=>p1=v3...
            List<PropertyValueAction> propactions = new List<PropertyValueAction>();
            string[] valueAndActionMatches = Regex.Matches(s, @"[^;]+=>.+?(?=(;[^;]+=>)|$)", RegexOptions.Multiline).Cast<Match>().Select(m => m.Value).ToArray();
            foreach (string p in valueAndActionMatches)
            {
                PropertyValueAction propertyValueAction = PropertyValueAction.Parse(p);
                if (propertyValueAction != null)
                    propactions.Add(propertyValueAction);
            }
            return propactions.ToArray();
        }
    }

}