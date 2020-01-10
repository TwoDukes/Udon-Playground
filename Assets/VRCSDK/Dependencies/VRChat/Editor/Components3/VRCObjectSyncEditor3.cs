#if VRC_SDK_VRCSDK3

using UnityEngine;
using System.Collections;
using UnityEditor;
using System;

[CustomEditor(typeof(VRC.SDK3.Components.VRCObjectSync))]
public class VRCObjectSyncEditor3 : Editor
{
    VRC.SDK3.Components.VRCObjectSync sync;

    void OnEnable()
    {
        if (sync == null)
            sync = (VRC.SDK3.Components.VRCObjectSync)target;
    }

    public override void OnInspectorGUI()
    {
        sync.SynchronizePhysics = EditorGUILayout.Toggle("Synchronize Physics",sync.SynchronizePhysics);
        sync.AllowCollisionTransfer = EditorGUILayout.Toggle("Allow Collision Transfer", sync.AllowCollisionTransfer);
    }
}

#endif