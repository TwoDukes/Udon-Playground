#if VRC_SDK_VRCSDK3

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(VRC.SDK3.Components.VRCAvatarDescriptor))]
public class AvatarDescriptorEditor3 : Editor
{
    VRC.SDK3.Components.VRCAvatarDescriptor avatarDescriptor;
    VRC.Core.PipelineManager pipelineManager;

    SkinnedMeshRenderer selectedMesh;
    List<string> blendShapeNames = null;

    bool shouldRefreshVisemes = false;

    public override void OnInspectorGUI()
    {
        if (avatarDescriptor == null)
            avatarDescriptor = (VRC.SDK3.Components.VRCAvatarDescriptor)target;

        if (pipelineManager == null)
        {
            pipelineManager = avatarDescriptor.GetComponent<VRC.Core.PipelineManager>();
            if (pipelineManager == null)
                avatarDescriptor.gameObject.AddComponent<VRC.Core.PipelineManager>();
        }

        // DrawDefaultInspector();

        if(VRCSdkControlPanel.window != null)
        { 
            if( GUILayout.Button( "Select this avatar in the SDK control panel" ) )
                VRCSdkControlPanel.SelectAvatar(avatarDescriptor);
        }

        avatarDescriptor.ViewPosition = EditorGUILayout.Vector3Field("View Position", avatarDescriptor.ViewPosition);
        //avatarDescriptor.Name = EditorGUILayout.TextField("Avatar Name", avatarDescriptor.Name);
        avatarDescriptor.Animations = (VRC.SDK3.Components.VRCAvatarDescriptor.AnimationSet)EditorGUILayout.EnumPopup("Default Animation Set", avatarDescriptor.Animations);
        avatarDescriptor.CustomStandingAnims = (AnimatorOverrideController)EditorGUILayout.ObjectField("Custom Standing Anims", avatarDescriptor.CustomStandingAnims, typeof(AnimatorOverrideController), true, null);
        avatarDescriptor.CustomSittingAnims = (AnimatorOverrideController)EditorGUILayout.ObjectField("Custom Sitting Anims", avatarDescriptor.CustomSittingAnims, typeof(AnimatorOverrideController), true, null);
        avatarDescriptor.ScaleIPD = EditorGUILayout.Toggle("Scale IPD", avatarDescriptor.ScaleIPD);

        avatarDescriptor.lipSync = (VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle)EditorGUILayout.EnumPopup("Lip Sync", avatarDescriptor.lipSync);
        switch (avatarDescriptor.lipSync)
        {
            case VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.Default:
                if (GUILayout.Button("Auto Detect!"))
                    AutoDetectLipSync();
                break;

            case VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.JawFlapBlendShape:
                avatarDescriptor.VisemeSkinnedMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Face Mesh", avatarDescriptor.VisemeSkinnedMesh, typeof(SkinnedMeshRenderer), true);
                if (avatarDescriptor.VisemeSkinnedMesh != null)
                {
                    DetermineBlendShapeNames();

                    int current = -1;
                    for (int b = 0; b < blendShapeNames.Count; ++b)
                        if (avatarDescriptor.MouthOpenBlendShapeName == blendShapeNames[b])
                            current = b;

                    string title = "Jaw Flap Blend Shape";
                    int next = EditorGUILayout.Popup(title, current, blendShapeNames.ToArray());
                    if (next >= 0)
                        avatarDescriptor.MouthOpenBlendShapeName = blendShapeNames[next];
                }
                break;

            case VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.JawFlapBone:
                avatarDescriptor.lipSyncJawBone = (Transform)EditorGUILayout.ObjectField("Jaw Bone", avatarDescriptor.lipSyncJawBone, typeof(Transform), true);
                break;

            case VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape:
                SkinnedMeshRenderer prev = avatarDescriptor.VisemeSkinnedMesh;
                avatarDescriptor.VisemeSkinnedMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Face Mesh", avatarDescriptor.VisemeSkinnedMesh, typeof(SkinnedMeshRenderer), true);
                if (avatarDescriptor.VisemeSkinnedMesh != prev)
                    shouldRefreshVisemes = true;
                if (avatarDescriptor.VisemeSkinnedMesh != null)
                {
                    DetermineBlendShapeNames();

                    if (avatarDescriptor.VisemeBlendShapes == null || avatarDescriptor.VisemeBlendShapes.Length != (int)VRC.SDK3.Components.VRCAvatarDescriptor.Viseme.Count)
                        avatarDescriptor.VisemeBlendShapes = new string[(int)VRC.SDK3.Components.VRCAvatarDescriptor.Viseme.Count];
                    for (int i = 0; i < (int)VRC.SDK3.Components.VRCAvatarDescriptor.Viseme.Count; ++i)
                    {
                        int current = -1;
                        for (int b = 0; b < blendShapeNames.Count; ++b)
                            if (avatarDescriptor.VisemeBlendShapes[i] == blendShapeNames[b])
                                current = b;

                        string title = "Viseme: " + ((VRC.SDK3.Components.VRCAvatarDescriptor.Viseme)i).ToString();
                        int next = EditorGUILayout.Popup(title, current, blendShapeNames.ToArray());
                        if (next >= 0)
                            avatarDescriptor.VisemeBlendShapes[i] = blendShapeNames[next];
                    }

                    if (shouldRefreshVisemes)
                        AutoDetectVisemes();
                }
                break;
        }
        EditorGUILayout.LabelField("Unity Version", avatarDescriptor.unityVersion);
    }

    void DetermineBlendShapeNames()
    {
        if (avatarDescriptor.VisemeSkinnedMesh != null &&
            avatarDescriptor.VisemeSkinnedMesh != selectedMesh)
        {
            blendShapeNames = new List<string>();
            blendShapeNames.Add("-none-");
            selectedMesh = avatarDescriptor.VisemeSkinnedMesh;
            if ((selectedMesh != null) && (selectedMesh.sharedMesh != null))
            {
                for (int i = 0; i < selectedMesh.sharedMesh.blendShapeCount; ++i)
                    blendShapeNames.Add(selectedMesh.sharedMesh.GetBlendShapeName(i));
            }
        }
    }

    void AutoDetectVisemes()
    {

        // prioritize strict - but fallback to looser - naming and don't touch user-overrides

        List<string> blendShapes = new List<string>(blendShapeNames);
        blendShapes.Remove("-none-");

        for (int v = 0; v < avatarDescriptor.VisemeBlendShapes.Length; v++)
        {
            if (string.IsNullOrEmpty(avatarDescriptor.VisemeBlendShapes[v]))
            {
                string viseme = ((VRC.SDK3.Components.VRCAvatarDescriptor.Viseme)v).ToString().ToLowerInvariant();

                foreach (string s in blendShapes)
                {
                    if (s.ToLowerInvariant() == "vrc.v_" + viseme)
                    {
                        avatarDescriptor.VisemeBlendShapes[v] = s;
                        goto next;
                    }
                }
                foreach (string s in blendShapes)
                {
                    if (s.ToLowerInvariant() == "v_" + viseme)
                    {
                        avatarDescriptor.VisemeBlendShapes[v] = s;
                        goto next;
                    }
                }
                foreach (string s in blendShapes)
                {
                    if (s.ToLowerInvariant().EndsWith(viseme))
                    {
                        avatarDescriptor.VisemeBlendShapes[v] = s;
                        goto next;
                    }
                }
                foreach (string s in blendShapes)
                {
                    if (s.ToLowerInvariant() == viseme)
                    {
                        avatarDescriptor.VisemeBlendShapes[v] = s;
                        goto next;
                    }
                }
                foreach (string s in blendShapes)
                {
                    if (s.ToLowerInvariant().Contains(viseme))
                    {
                        avatarDescriptor.VisemeBlendShapes[v] = s;
                        goto next;
                    }
                }
                next: { }
            }
        }

        shouldRefreshVisemes = false;

    }

    void AutoDetectLipSync()
    {
        var smrs = avatarDescriptor.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh.blendShapeCount > 0)
            {
                avatarDescriptor.lipSyncJawBone = null;

                if (smr.sharedMesh.blendShapeCount > 1)
                {
                    avatarDescriptor.lipSync = VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                    avatarDescriptor.VisemeSkinnedMesh = smr;
                    shouldRefreshVisemes = true;
                }
                else
                {
                    avatarDescriptor.lipSync = VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                    avatarDescriptor.VisemeSkinnedMesh = null;
                }

                return;
            }
        }

        Animator a = avatarDescriptor.GetComponent<Animator>();
        if (!a)
            EditorUtility.DisplayDialog("Ooops", "This avatar has no Animator and can have no lipsync.", "OK");
        else if (a.GetBoneTransform(HumanBodyBones.Jaw) != null)
        {
            avatarDescriptor.lipSync = VRC.SDK3.Components.VRCAvatarDescriptor.LipSyncStyle.JawFlapBone;
            avatarDescriptor.lipSyncJawBone = avatarDescriptor.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Jaw);
            avatarDescriptor.VisemeSkinnedMesh = null;
            return;
        }

    }
}

#endif