using System;
using UnityEditor;
using UnityEngine;

#if (UNITY_EDITOR)
public class GizmoHandler : MonoBehaviour
{
    public Action DrawGizmos;
    public Action DrawGizmosSelected;

    private void OnDrawGizmos()
    {
        if (EditorApplication.isPlaying)
        {
            DrawGizmos?.Invoke();
        }
    }
 
    private void OnDrawGizmosSelected()
    {
        if (EditorApplication.isPlaying)
        {
            DrawGizmosSelected?.Invoke();
            // DrawGizmosSelected = null;
        }
    }
}
#endif