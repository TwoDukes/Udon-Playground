using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Serialization.OdinSerializer;

public class UdonProgramAsset : AbstractUdonProgramSource, ISerializationCallbackReceiver, ISupportsPrefabSerialization, IOverridesSerializationFormat
{
    [NonSerialized, OdinSerialize]
    protected IUdonProgram program;

    [SerializeField]
    private bool showSerializedProgramJSON = false;

    public override IUdonProgram GetProgram()
    {
        return (IUdonProgram)SerializationUtility.CreateCopy(program);
    }

    public override void RunProgramSourceEditor(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty)
    {
        if(program != null)
        {
            DrawPublicVariables(publicVariables, ref dirty);
            DrawProgramDisassembly();
        }

        DrawSerializationDebug();

        if(dirty)
        {
            EditorUtility.SetDirty(this);
        }
    }

    public sealed override void RefreshProgram()
    {
        if(Application.isPlaying)
        {
            return;
        }

        DoRefreshProgramActions();

        UdonEditorManager.Instance.TriggerUdonBehaviourProgramRefresh(this);

        if(!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
    }

    protected virtual void DoRefreshProgramActions()
    {
    }

    [Conditional("UDON_DEBUG")]
    protected void DrawSerializationDebug()
    {
        EditorGUI.BeginChangeCheck();
        bool newShowSerializedProgramJSON = EditorGUILayout.Foldout(showSerializedProgramJSON, "Serialized Program JSON");
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Toggle Serialized Program JSON Foldout");
            showSerializedProgramJSON = newShowSerializedProgramJSON;
        }

        if(!showSerializedProgramJSON)
        {
            return;
        }

        EditorGUI.indentLevel++;
        if(string.IsNullOrEmpty(serializationData.SerializedBytesString))
        {
            return;
        }

        using(new EditorGUI.DisabledScope(true))
        {
            string serializedJSONString = serializationData.SerializedBytesString;
            EditorGUILayout.TextArea(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(serializedJSONString)));
        }

        EditorGUI.indentLevel--;
    }

    [PublicAPI]
    protected void DrawPublicVariables(Dictionary<string, (object value, Type declaredType)> publicVariables, ref bool dirty)
    {
        EditorGUILayout.LabelField("Public Variables", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if(program?.SymbolTable == null)
        {
            EditorGUILayout.LabelField("No public variables.");
            EditorGUI.indentLevel--;
            return;
        }

        IUdonSymbolTable symbolTable = program.SymbolTable;
        string[] exportedSymbolNames = symbolTable.GetExportedSymbols();

        if(publicVariables != null)
        {
            foreach(string publicVariableSymbol in publicVariables.Keys.ToArray())
            {
                if(!exportedSymbolNames.Contains(publicVariableSymbol))
                {
                    publicVariables.Remove(publicVariableSymbol);
                }
            }
        }

        if(exportedSymbolNames.Length <= 0)
        {
            EditorGUILayout.LabelField("No public variables.");
            EditorGUI.indentLevel--;
            return;
        }

        foreach(string exportedSymbol in exportedSymbolNames)
        {
            (object value, Type declaredType) publicVariable;
            bool enabled = true;
            if(publicVariables == null)
            {
                enabled = false;
                publicVariable = InitializePublicVariable(symbolTable.GetSymbolType(exportedSymbol), exportedSymbol);
            }
            else if(!publicVariables.TryGetValue(exportedSymbol, out publicVariable))
            {
                publicVariable = InitializePublicVariable(symbolTable.GetSymbolType(exportedSymbol), exportedSymbol);
            }

            if(publicVariable.declaredType != symbolTable.GetSymbolType(exportedSymbol))
            {
                publicVariable = InitializePublicVariable(symbolTable.GetSymbolType(exportedSymbol), exportedSymbol);
            }
            
            DrawFieldForTypeString(exportedSymbol, ref publicVariable, ref dirty, enabled);

            if(publicVariables != null)
            {
                publicVariables[exportedSymbol] = publicVariable;
            }
        }

        EditorGUI.indentLevel--;
    }

    protected virtual (object value, Type declaredType) InitializePublicVariable(Type type, string symbol)
    {
        return (null, type);
    } 

    [PublicAPI]
    protected void DrawProgramDisassembly()
    {
        EditorGUILayout.LabelField("Disassembled Program", EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(true))
        {
            string[] disassembledProgram = UdonEditorManager.Instance.DisassembleProgram(program);
            EditorGUILayout.TextArea(string.Join("\n", disassembledProgram));
        }
    }
    
    [NonSerialized]
    private readonly Dictionary<string, bool> _arrayStates = new Dictionary<string, bool>();

    protected virtual void DrawFieldForTypeString(string symbol, ref (object value, Type declaredType) publicVariable, ref bool dirty, bool enabled)
    {
        using(new EditorGUI.DisabledScope(!enabled))
        {
            // ReSharper disable RedundantNameQualifier
            EditorGUILayout.BeginHorizontal();
            (object value, Type declaredType) = publicVariable;
            if(typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
            {
                UnityEngine.Object unityEngineObjectValue = (UnityEngine.Object)value;
                EditorGUI.BeginChangeCheck();
                Rect fieldRect = EditorGUILayout.GetControlRect();
                publicVariable.value = EditorGUI.ObjectField(fieldRect, symbol, unityEngineObjectValue, declaredType, true); 
                 
                if (publicVariable.value == null && (declaredType == typeof(GameObject) || declaredType == typeof(Transform) ||
                                                        declaredType == typeof(UdonBehaviour)))
                {
                    EditorGUI.LabelField(fieldRect, new GUIContent(symbol), new GUIContent("Self (" + declaredType.Name + ")" , AssetPreview.GetMiniTypeThumbnail(declaredType)), EditorStyles.objectField);    
                }
                
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if(declaredType == typeof(string))
            {
                string stringValue = (string)value;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.TextField(symbol, stringValue);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(string[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                string[] valueArray = (string[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.TextField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : "");
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(float))
            {
                float floatValue = (float?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.FloatField(symbol, floatValue);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(float[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                float[] valueArray = (float[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.FloatField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(int))
            {
                int intValue = (int?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.IntField(symbol, intValue);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(int[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                int[] valueArray = (int[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.IntField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : 0);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(bool))
            {
                bool boolValue = (bool?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.Toggle(symbol, boolValue);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(bool[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                bool[] valueArray = (bool[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Toggle($"{i}:",
                                valueArray.Length > i && valueArray[i]);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(UnityEngine.Vector2))
            {
                Vector2 vector2Value = (Vector2?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.Vector2Field(symbol, vector2Value);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(Vector2[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Vector2[] valueArray = (Vector2[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Vector2Field($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Vector2.zero);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(UnityEngine.Vector3))
            {
                Vector3 vector3Value = (Vector3?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.Vector3Field(symbol, vector3Value);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(Vector3[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Vector3[] valueArray = (Vector3[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Vector3Field($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Vector3.zero);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(UnityEngine.Vector4))
            {
                Vector4 vector4Value = (Vector4?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.Vector4Field(symbol, vector4Value);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(Vector4[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Vector4[] valueArray = (Vector4[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.Vector4Field($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Vector4.zero);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(UnityEngine.Quaternion))
            {
                Quaternion quaternionValue = (Quaternion?)value ?? default;
                EditorGUI.BeginChangeCheck();
                Vector4 quaternionVector4 = EditorGUILayout.Vector4Field(symbol, new Vector4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w));
                publicVariable.value = new Quaternion(quaternionVector4.x, quaternionVector4.y, quaternionVector4.z, quaternionVector4.w);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(Quaternion[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Quaternion[] valueArray = (Quaternion[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            Vector4 vector4 = EditorGUILayout.Vector4Field($"{i}:",
                                valueArray.Length > i ? new Vector4(valueArray[i].x, valueArray[i].y, valueArray[i].z, valueArray[i].w) : Vector4.zero);
                            valueArray[i] = new Quaternion(vector4.x, vector4.y, vector4.z, vector4.w);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(UnityEngine.Color))
            {
                Color color2Value = (Color?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.ColorField(symbol, color2Value);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(Color[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Color[] valueArray = (Color[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.ColorField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : Color.white);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(UnityEngine.Color32))
            {
                Color32 colorValue = (Color32?)value ?? default;
                EditorGUI.BeginChangeCheck();
                publicVariable.value = (Color32)EditorGUILayout.ColorField(symbol, colorValue);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(Color32[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Color32[] valueArray = (Color32[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = (Color32)EditorGUILayout.ColorField($"{i}:",
                                valueArray.Length > i ? valueArray[i] : (Color32)Color.white);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(ParticleSystem.MinMaxCurve))
            {
                ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve?)value ?? default;
                EditorGUI.BeginChangeCheck();
                float multiplier = minMaxCurve.curveMultiplier;
                AnimationCurve minCurve = minMaxCurve.curveMin;
                AnimationCurve maxCurve = minMaxCurve.curveMax;
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(symbol);
                EditorGUI.indentLevel++;
                multiplier = EditorGUILayout.FloatField("Multiplier", multiplier);
                minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                publicVariable.value = new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            else if (declaredType == typeof(ParticleSystem.MinMaxCurve[]))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                ParticleSystem.MinMaxCurve[] valueArray = (ParticleSystem.MinMaxCurve[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            ParticleSystem.MinMaxCurve minMaxCurve = (ParticleSystem.MinMaxCurve) valueArray[i];
                            float multiplier = minMaxCurve.curveMultiplier;
                            AnimationCurve minCurve = minMaxCurve.curveMin;
                            AnimationCurve maxCurve = minMaxCurve.curveMax;
                            EditorGUILayout.BeginVertical();
                            EditorGUI.indentLevel++;
                            multiplier = EditorGUILayout.FloatField("Multiplier", multiplier);
                            minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                            maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndVertical();
                            valueArray[i] = new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType.IsEnum)
            {
                Enum enumValue = (Enum)value;
                GUI.SetNextControlName("NodeField");
                EditorGUI.BeginChangeCheck();
                publicVariable.value = EditorGUILayout.EnumPopup(symbol, enumValue);
                if(EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                }
            }
            // ReSharper disable once PossibleNullReferenceException
            else if (declaredType.IsArray && declaredType.GetElementType().IsEnum)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                Enum[] valueArray = (Enum[]) value;
                GUI.SetNextControlName("NodeField");
                bool showArray = false;
                if (_arrayStates.ContainsKey(symbol))
                {
                    showArray = _arrayStates[symbol];
                }
                else
                {
                    _arrayStates.Add(symbol, false);
                }

                EditorGUILayout.BeginHorizontal();
                showArray = EditorGUILayout.Foldout(showArray, GUIContent.none);
                _arrayStates[symbol] = showArray;
                
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray != null && valueArray.Length > 0 ? valueArray.Length : 1);
                newSize = newSize >= 0 ? newSize : 0;
                Array.Resize(ref valueArray, newSize);
                EditorGUILayout.EndHorizontal();

                if (showArray)
                {
                    if (valueArray != null && valueArray.Length > 0)
                    {
                        for (int i = 0; i < valueArray.Length; i++)
                        {
                            GUI.SetNextControlName("NodeField");
                            valueArray[i] = EditorGUILayout.EnumPopup($"{i}:",
                                valueArray[i]);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    publicVariable.value = valueArray;
                    dirty = true;
                }
            }
            else if(declaredType == typeof(Type))
            {
                Type typeValue = (Type)value;
                EditorGUILayout.LabelField(symbol, typeValue == null ? $"Type = null" : $"Type = {typeValue.Name}");
            }

            else if (declaredType.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(declaredType.GetElementType())) // declaredType == typeof(Transform[]
            {
                Type elementType = declaredType.GetElementType();
                Assert.IsNotNull(elementType);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField(symbol);
                EditorGUILayout.BeginVertical();
                
                if (value == null)
                {
                    value = Array.CreateInstance(elementType, 0);
                }
                
                UnityEngine.Object[] valueArray = (UnityEngine.Object[]) value;
                GUI.SetNextControlName("NodeField");
                int newSize = EditorGUILayout.IntField("size:",
                    valueArray.Length > 0 ? valueArray.Length : 1);
                Array.Resize(ref valueArray, newSize);
                Assert.IsNotNull(valueArray);

                if (valueArray.Length > 0)
                {
                    for (int i = 0; i < valueArray.Length; i++)
                    {
                        GUI.SetNextControlName("NodeField");
                        valueArray[i] = EditorGUILayout.ObjectField($"{i}:", valueArray.Length > i ? valueArray[i] : null, declaredType.GetElementType(), true);
                    }
                }
                
                EditorGUILayout.EndVertical();
                if (EditorGUI.EndChangeCheck())
                {
                    Array destinationArray = Array.CreateInstance(elementType, valueArray.Length);
                    Array.Copy(valueArray, destinationArray, valueArray.Length);

                    publicVariable.value = destinationArray;

                    dirty = true;
                }
            }
            else
            {
                EditorGUILayout.LabelField(symbol + " no defined editor for type of " + declaredType);
            }
            // ReSharper restore RedundantNameQualifier

            IUdonSyncMetadata sync = program.SyncMetadataTable.GetSyncMetadataFromSymbol(symbol);
            if(sync != null)
            {
                GUILayout.Label($"sync{sync.Properties[0].InterpolationAlgorithmName}", GUILayout.Width(80));
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    #region Serialization Methods

    [SerializeField, HideInInspector]
    private SerializationData serializationData;

    SerializationData ISupportsPrefabSerialization.SerializationData
    {
        get => serializationData;
        set => serializationData = value;
    }
    
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
        OnAfterDeserialize();
    }

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
        OnBeforeSerialize();
        UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
    }

    [PublicAPI]
    protected virtual void OnAfterDeserialize()
    {
    }

    [PublicAPI]
    protected virtual void OnBeforeSerialize()
    {
    }

    DataFormat IOverridesSerializationFormat.GetFormatToSerializeAs(bool isPlayer)
    {
        return DataFormat.JSON;
    }

    #endregion
}
