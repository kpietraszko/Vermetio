using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vermetio.AI;

[CustomPropertyDrawer(typeof(AnimationCurve))]
public class AnimationCurveDrawer : PropertyDrawer
{

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 150f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // base.OnGUI(position, property, label);
        EditorGUI.BeginProperty(position, label, property);
        var rect = position;
        rect.height = GetPropertyHeight(property, label);
        EditorGUI.CurveField(rect, property, Color.cyan, new Rect(0, 0, 1, 1), label);
        EditorGUI.EndProperty();
    }
}
