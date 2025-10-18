using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lachee.Discord.Editor
{
    public static class SerializedPropertyExtensions
    {
        /// <summary>
        /// Gets the type of the underlying field the SerializedProperty is of.
        /// </summary>
        /// <param name="property">The property to get the type from</param>
        /// <returns></returns>
        public static System.Type GetSerializedType(this SerializedProperty property)
        {
            return property.GetSerializedFieldInfo()?.FieldType;
        }

        /// <summary>
        /// Gets the FieldInfo of the underlying field
        /// </summary>
        /// <param name="property">The property to get the FieldInfo off</param>
        /// <returns></returns>
        public static FieldInfo GetSerializedFieldInfo(this SerializedProperty property)
        {
            System.Type parentType = property.serializedObject.targetObject.GetType();
            return parentType.GetFieldInfoFromPath(property.propertyPath);
        }

        /// <summary>
        /// Gets the underlying value this property represents
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public static object GetSerializedValue(this SerializedProperty property)
        {
#if !DISABLE_FAST_SERIALIZED_VALUE_LOOKUP
            switch (property.propertyType)
            {
                default:                                                    // If we cant find anything, we should just use the raw .ToString of the value
                case SerializedPropertyType.Enum:                           // Its easier to just lookup the enum properties than recreating it
                    break;

                // Manually get a bunch because its more efficient than looking up serialized values
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue ? property.objectReferenceValue : null;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Integer:
                    return property.intValue;
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return property.colorValue;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value;
                case SerializedPropertyType.Vector3:
                    return property.vector3Value;
                case SerializedPropertyType.Vector4:
                    return property.vector4Value;
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue;
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue;
                case SerializedPropertyType.Bounds:
                    return property.boundsValue;
                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue;
                case SerializedPropertyType.Rect:
                    return property.rectValue;
                case SerializedPropertyType.RectInt:
                    return property.rectIntValue;
            }
#endif
            // Lookup the property path and pull teh value directly
            System.Type parentType = property.serializedObject.targetObject.GetType();
            return parentType.GetValueFromPath(property.serializedObject.targetObject, property.propertyPath);
        }

        /// <summary>
        /// Finds the field info for the given type at the given path.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="path"></param>
        /// <param name="flag"></param>
        /// <remarks>Does not work with arrays yet as they would return a PropertyInfo instead</remarks>
        /// <returns></returns>
        public static FieldInfo GetFieldInfoFromPath(this System.Type type, string path, BindingFlags flag = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField)
        {
            System.Type parentType = type;
            FieldInfo fi = type.GetField(path, flag);
            if (fi != null) return fi;

            string[] perDot = path.Split('.');
            foreach (string fieldName in perDot)
            {
                fi = parentType.GetField(fieldName, flag);
                if (fi != null)
                    parentType = fi.FieldType;
                else
                    return null;
            }
            if (fi != null)
                return fi;
            else return null;
        }

        /// <summary>
        /// Gets the field values from the given path
        /// </summary>
        /// <param name="type">The type of the root object</param>
        /// <param name="context">The root object to get the value from</param>
        /// <param name="path">The SerializedProperty formatted path</param>
        /// <param name="flag">The flag used to search fields.</param>
        /// <returns></returns>
        public static object GetValueFromPath(this System.Type type, object context, string path, BindingFlags flag = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField)
        {
            object result = context;
            System.Type resultType = type;

            // We need to delve deeper until we hit the final result.
            string[] segments = path.Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                // If the field name is an array we need to break apart the next segment to extract its index.
                //  Once we have the index we can then use the `this` property arrays have to get the appropriate item and 
                //  continue our search through the list of paths.
                string fieldName = segments[i];
                if (fieldName == "Array")
                {
                    // parse the index
                    string arrIndexPath = segments[++i];
                    string arrIndexStr = arrIndexPath.Substring(5, arrIndexPath.Length - 1 - 5);
                    int arrIndex = int.Parse(arrIndexStr);

                    // get the property
                    var thisProperty = resultType.GetProperty("Item", new System.Type[] { arrIndex.GetType() });
                    var thisGetter = thisProperty.GetMethod;

                    // Update the current state
                    result = thisGetter.Invoke(result, new object[] { arrIndex });
                    resultType = result.GetType();
                }
                else
                {
                    var fi = resultType.GetField(fieldName, flag);
                    if (fi == null) return null;

                    resultType = fi.FieldType;
                    result = fi.GetValue(result);
                }
            }

            return result;
        }
    }
}