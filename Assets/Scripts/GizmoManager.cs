using System;
using UnityEngine;

#if UNITY_EDITOR
public static class GizmoManager
{
    public static void AddGizmoAction(Action action)
    {
        Handler.DrawGizmos += action;
    }

    public static void ClearGizmos()
    {
        Handler.DrawGizmos = null;
    }

    private static GizmoHandler Handler => _handler != null ? _handler : (_handler = createHandler());
    private static GizmoHandler _handler;
 
    private static GizmoHandler createHandler()
    {
        var go = new GameObject("Gizmo Handler") { hideFlags = HideFlags.DontSave };
 
        return go.AddComponent<GizmoHandler>();
    }
 
}
#endif