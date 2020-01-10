using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor;

[assembly: UdonProgramSourceNewMenu(typeof(UdonAssemblyProgramAsset), "Udon Assembly Program Asset")]

[CreateAssetMenu(menuName = "VRChat/Udon/Udon Assembly Program Asset", fileName = "New Udon Assembly Program Asset")]
public class UdonAssemblyProgramAsset : UdonProgramAsset
{
    [SerializeField]
    protected string udonAssembly = "";

    [SerializeField]
    private string assemblyError = null;

    public override void RunProgramSourceEditor(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty)
    {
        DrawAssemblyTextArea(!Application.isPlaying, ref dirty);
        DrawAssemblyErrorTextArea();

        base.RunProgramSourceEditor(publicVariables, ref dirty);
    }
    
    protected override void DoRefreshProgramActions()
    {
        AssembleProgram();
    }

    [PublicAPI]
    protected virtual void DrawAssemblyTextArea(bool allowEditing, ref bool dirty)
    {
        EditorGUILayout.LabelField("Assembly Code", EditorStyles.boldLabel);
        if (GUILayout.Button("Copy Assembly To Clipboard"))
        {
            EditorGUIUtility.systemCopyBuffer = udonAssembly;
        }
        EditorGUI.BeginChangeCheck();
        using(new EditorGUI.DisabledScope(!allowEditing))
        {
            string newAssembly = EditorGUILayout.TextArea(udonAssembly);
            if(EditorGUI.EndChangeCheck())
            {
                dirty = true;
                Undo.RecordObject(this, "Edit Assembly Program Code");
                udonAssembly = newAssembly;
                UdonEditorManager.Instance.QueueProgramSourceRefresh(this);
            }
        }
    }

    [PublicAPI]
    protected void DrawAssemblyErrorTextArea()
    {
        if(string.IsNullOrEmpty(assemblyError))
        {
            return;
        }

        EditorGUILayout.LabelField("Assembly Error", EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(assemblyError);
        }
    }

    [PublicAPI]
    protected void AssembleProgram()
    {
        try
        {
            program = UdonEditorManager.Instance.Assemble(udonAssembly);
            assemblyError = null;
        }
        catch(Exception e)
        {
            program = null;
            assemblyError = e.Message;
        }
    }
}
