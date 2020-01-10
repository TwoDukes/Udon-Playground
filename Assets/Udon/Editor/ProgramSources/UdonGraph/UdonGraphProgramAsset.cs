using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;

[assembly: UdonProgramSourceNewMenu(typeof(UdonGraphProgramAsset), "Udon Graph Program Asset")]

[CreateAssetMenu(menuName = "VRChat/Udon/Udon Graph Program Asset", fileName = "New Udon Graph Program Asset")]
public class UdonGraphProgramAsset : UdonAssemblyProgramAsset, IUdonGraphDataProvider
{
    [SerializeField]
    public UdonGraphData graphData = new UdonGraphData();

    [SerializeField]
    private bool showAssembly = false;

    [NonSerialized, OdinSerialize] 
    private Dictionary<string, (object value, Type type)> heapDefaultValues = new Dictionary<string, (object value, Type type)>();

    public override void RunProgramSourceEditor(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty)
    {
        if(program == null)
        {
            RefreshProgram();
        }

        if(GUILayout.Button("Open Udon Graph", "LargeButton"))
        {
            EditorWindow.GetWindow<UdonGraphWindow>("Udon Graph", true, typeof(SceneView));
        }

        DrawPublicVariables(publicVariables, ref dirty);
        DrawAssemblyErrorTextArea();
        DrawAssemblyTextArea(false, ref dirty);
        DrawSerializationDebug();

        if(dirty)
        {
            EditorUtility.SetDirty(this);
        }
    }

    protected override void DoRefreshProgramActions()
    {
        if(graphData == null)
        {
            return;
        }

        CompileGraph();
        base.DoRefreshProgramActions();
        ApplyDefaultValuesToHeap();
    }

    protected override void DrawAssemblyTextArea(bool allowEditing, ref bool dirty)
    {
        EditorGUI.BeginChangeCheck();
        bool newShowAssembly = EditorGUILayout.Foldout(showAssembly, "Compiled Graph Assembly");
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Toggle Assembly Foldout");
            showAssembly = newShowAssembly;
        }

        if(!showAssembly)
        {
            return;
        }

        EditorGUI.indentLevel++;
        base.DrawAssemblyTextArea(allowEditing, ref dirty);
        EditorGUI.indentLevel--;
    }

    [PublicAPI]
    protected void CompileGraph()
    {
        udonAssembly = UdonEditorManager.Instance.CompileGraph(graphData, null, out Dictionary<string, (string uid, string fullName, int index)> _, out heapDefaultValues);
    }

    [PublicAPI]
    protected void ApplyDefaultValuesToHeap()
    {
        IUdonSymbolTable symbolTable = program?.SymbolTable;
        IUdonHeap heap = program?.Heap;
        if(symbolTable == null || heap == null)
        {
            return;
        }
        
        foreach(KeyValuePair<string, (object value, Type type)> defaultValue in heapDefaultValues)
        {
            if(!symbolTable.HasAddressForSymbol(defaultValue.Key))
            {
                continue;
            }

            uint symbolAddress = symbolTable.GetAddressFromSymbol(defaultValue.Key);
            (object value, Type declaredType) = defaultValue.Value;
            if (typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
            {
                if (value != null && !declaredType.IsInstanceOfType(value))
                {
                    heap.SetHeapVariable(symbolAddress, null, declaredType);
                    continue;
                }
                if ((UnityEngine.Object) value == null)
                {
                    heap.SetHeapVariable(symbolAddress, null, declaredType);
                    continue;
                }
            } 

            if (value != null)
            {
                if (!declaredType.IsInstanceOfType(value))
                {
                    value = declaredType.IsValueType ? Activator.CreateInstance(declaredType) : null;
                }
            }

            heap.SetHeapVariable(symbolAddress, value, declaredType);
        }
    }
    
    protected override (object value, Type declaredType) InitializePublicVariable(Type type, string symbol)
    {
        IUdonSymbolTable symbolTable = program?.SymbolTable;
        IUdonHeap heap = program?.Heap;
        if(symbolTable == null || heap == null)
        { 
            return (null, type);
        }
        if (!heapDefaultValues.ContainsKey(symbol))
        {
            return (null, type);
        }
        (object value, Type declaredType) = heapDefaultValues[symbol];
        if (!typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
        {
            return (value, declaredType);
        }
        return (UnityEngine.Object) value == null ? ( null, declaredType) : (value, declaredType);
    }

    protected override void DrawFieldForTypeString(string symbol, ref (object value, Type declaredType) publicVariable, ref bool dirty,
        bool enabled)
    {
        EditorGUILayout.BeginHorizontal();
        base.DrawFieldForTypeString(symbol, ref publicVariable, ref dirty, enabled);
        object defaultValue = null;
        if (heapDefaultValues.ContainsKey(symbol))
        {
            defaultValue = heapDefaultValues[symbol].value;   
        }
        if (publicVariable.value == null || !publicVariable.value.Equals(defaultValue))
        {
            if (defaultValue != null || publicVariable.value != null)
            {
                if (GUILayout.Button("Reset to Default Value"))
                {
                    publicVariable.value = defaultValue;
                    dirty = true;
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    #region Serialization Methods

    protected override void OnAfterDeserialize()
    {
        foreach(UdonNodeData node in graphData.nodes)
        {
            node.SetGraph(graphData);
        }
    }

    #endregion

    public UdonGraphData GetGraphData()
    {
        return graphData;
    }
}
