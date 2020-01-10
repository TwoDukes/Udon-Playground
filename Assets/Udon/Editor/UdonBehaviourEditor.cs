using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRC.Udon.Editor
{
    [CustomEditor(typeof(UdonBehaviour))]
    public class UdonBehaviourEditor : UnityEditor.Editor
    {
        private SerializedProperty _programSourceProperty;
        private int _newProgramType = 1;

        private void OnEnable()
        {
            _programSourceProperty = serializedObject.FindProperty("programSource");

            UdonBehaviour udonTarget = (UdonBehaviour)target;
            if(udonTarget != null)
            {
                udonTarget.WantRepaint += Repaint;
            }
        }

        private void OnDisable()
        {
            UdonBehaviour udonTarget = (UdonBehaviour)target;
            if(udonTarget != null)
            {
                udonTarget.WantRepaint -= Repaint;
            }
        }

        public override void OnInspectorGUI()
        {
            UdonBehaviour udonTarget = (UdonBehaviour)target;

            bool dirty = false;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _programSourceProperty.objectReferenceValue = EditorGUILayout.ObjectField(
                "Program Source",
                _programSourceProperty.objectReferenceValue,
                typeof(AbstractUdonProgramSource),
                false
            );

            if(_programSourceProperty.objectReferenceValue == null)
            {
                List<(string displayName, Type newProgramType)> programSourceTypesForNewMenu = GetProgramSourceTypesForNewMenu();
                if(GUILayout.Button("New Program"))
                {
                    (string displayName, Type newProgramType) = programSourceTypesForNewMenu.ElementAt(_newProgramType);

                    string udonBehaviourName = udonTarget.name;
                    Scene scene = udonTarget.gameObject.scene;
                    ScriptableObject newProgramSource = CreateUdonProgramSourceAsset(newProgramType, displayName, scene, udonBehaviourName);

                    _programSourceProperty.objectReferenceValue = newProgramSource;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                _newProgramType = EditorGUILayout.Popup(
                    "",
                    _newProgramType,
                    programSourceTypesForNewMenu.Select(t => t.displayName).ToArray(),
                    GUILayout.ExpandWidth(false)
                );
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.EndHorizontal();
            }

            if(EditorGUI.EndChangeCheck())
            {
                dirty = true;
                serializedObject.ApplyModifiedProperties();
            }

            udonTarget.RunEditorUpdate(ref dirty);
            if(dirty && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(udonTarget.gameObject.scene);
            }
        }

        private static ScriptableObject CreateUdonProgramSourceAsset(Type newProgramType, string displayName, Scene scene, string udonBehaviourName)
        {
            string assetPath;
            if(string.IsNullOrEmpty(scene.path))
            {
                assetPath = "Assets/";
            }
            else
            {
                string scenePath = Path.GetDirectoryName(scene.path);
                string folderName = $"{scene.name}_UdonProgramSources";
                string folderPath = $"{scenePath}/{folderName}";

                if(!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder(scenePath, folderName);
                }

                assetPath = folderPath + "/";
            }

            assetPath = $"{assetPath}{udonBehaviourName} {displayName}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            ScriptableObject asset = CreateInstance(newProgramType);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        private static List<(string displayName, Type newProgramType)> GetProgramSourceTypesForNewMenu()
        {
            Type abstractProgramSourceType = typeof(AbstractUdonProgramSource);
            Type attributeNewMenuAttributeType = typeof(UdonProgramSourceNewMenuAttribute);

            List<(string displayName, Type newProgramType)> programSourceTypesForNewMenu = new List<(string displayName, Type newProgramType)>();
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                object[] attributesUncast;
                try
                {
                    attributesUncast = assembly.GetCustomAttributes(attributeNewMenuAttributeType, false);
                }
                catch
                {
                    attributesUncast = new object[0];
                }

                foreach(object attributeUncast in attributesUncast)
                {
                    if(!(attributeUncast is UdonProgramSourceNewMenuAttribute udonProgramSourceNewMenuAttribute))
                    {
                        continue;
                    }

                    if(!abstractProgramSourceType.IsAssignableFrom(udonProgramSourceNewMenuAttribute.Type))
                    {
                        continue;
                    }

                    programSourceTypesForNewMenu.Add((udonProgramSourceNewMenuAttribute.DisplayName, udonProgramSourceNewMenuAttribute.Type));
                }
            }

            return programSourceTypesForNewMenu;
        }
    }
}
