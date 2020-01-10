#if VRC_SDK_VRCSDK3

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VRC.SDK3.Components.VRCObjectSync))]
public class VRC_ObjectSyncEditor3 : Editor {
    public override void OnInspectorGUI()
    {
        VRC.SDK3.Components.VRCObjectSync c = ((VRC.SDK3.Components.VRCObjectSync)target);
        if ((c.gameObject.GetComponent<Animator>() != null || c.gameObject.GetComponent<Animation>() != null) && c.SynchronizePhysics)
            EditorGUILayout.HelpBox("If the Animator or Animation moves the root position of this object then it will conflict with physics synchronization.", MessageType.Warning);
        DrawDefaultInspector();
    }
}

#endif