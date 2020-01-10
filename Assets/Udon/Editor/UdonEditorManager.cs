using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.EditorBindings;
using VRC.Udon.EditorBindings.Interfaces;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.UAssembly.Interfaces;
using Object = UnityEngine.Object;

namespace VRC.Udon.Editor
{
    public class UdonEditorManager : IUdonEditorInterface
    {
        #region Singleton

        private static UdonEditorManager _instance;

        public static UdonEditorManager Instance => _instance ?? (_instance = new UdonEditorManager());

        #endregion

        #region Public Properties

        public double ProgramRefreshDelayRemaining => REFRESH_QUEUE_WAIT_PERIOD - (EditorApplication.timeSinceStartup - _lastProgramRefreshQueueTime);

        #endregion

        #region Private Constants

        private const double REFRESH_QUEUE_WAIT_PERIOD = 300.0;

        #endregion

        #region Private Fields

        private readonly UdonEditorInterface _udonEditorInterface;
        private readonly HashSet<UdonBehaviour> _udonBehaviours = new HashSet<UdonBehaviour>();

        private readonly HashSet<AbstractUdonProgramSource> _programSourceRefreshQueue = new HashSet<AbstractUdonProgramSource>();
        private double _lastProgramRefreshQueueTime;

        #endregion

        #region Initialization

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            _instance = new UdonEditorManager();
        }

        #endregion

        #region Constructors

        private UdonEditorManager()
        {
            _udonEditorInterface = new UdonEditorInterface();
            _udonEditorInterface.AddTypeResolver(new UdonBehaviourTypeResolver());

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += ProgramSourceRefreshUpdate;
        }

        #endregion

        #region UdonBehaviour and ProgramSource Refresh

        private void ProgramSourceRefreshUpdate()
        {
            if(Application.isPlaying)
            {
                return;
            }

            if(_programSourceRefreshQueue.Count <= 0)
            {
                return;
            }

            if(EditorApplication.timeSinceStartup - _lastProgramRefreshQueueTime < REFRESH_QUEUE_WAIT_PERIOD)
            {
                return;
            }

            RefreshQueuedProgramSources();
        }

        private void RefreshQueuedProgramSources()
        {
            foreach(AbstractUdonProgramSource programSource in _programSourceRefreshQueue)
            {
                if(programSource == null)
                {
                    return;
                }

                try
                {
                    programSource.RefreshProgram();
                }
                catch(Exception e)
                {
                    Debug.LogError($"Failed to refresh program '{programSource.name}' due to exception '{e}'.");
                }
            }

            _programSourceRefreshQueue.Clear();
        }

        public bool IsProgramSourceRefreshQueued(AbstractUdonProgramSource programSource)
        {
            if(_programSourceRefreshQueue.Count <= 0)
            {
                return false;
            }

            if(!_programSourceRefreshQueue.Contains(programSource))
            {
                return false;
            }

            return true;
        }

        public void QueueProgramSourceRefresh(AbstractUdonProgramSource programSource)
        {
            if(Application.isPlaying)
            {
                return;
            }
            
            if(programSource == null)
            {
                return;
            }

            _lastProgramRefreshQueueTime = EditorApplication.timeSinceStartup;

            if(_programSourceRefreshQueue.Contains(programSource))
            {
                return;
            }

            _programSourceRefreshQueue.Add(programSource);
        }
        
        public void CancelQueuedProgramSourceRefresh(AbstractUdonProgramSource programSource)
        {
            if(programSource == null)
            {
                return;
            }

            if(_programSourceRefreshQueue.Contains(programSource))
            {
                _programSourceRefreshQueue.Remove(programSource);
            }
        }

        [PublicAPI]
        public void TriggerUdonBehaviourProgramRefresh(AbstractUdonProgramSource updatedProgramSource)
        {
            _udonBehaviours.Clear();
            _udonBehaviours.UnionWith(Object.FindObjectsOfType<UdonBehaviour>());

            HashSet<string> prefabAssetPaths = new HashSet<string>();
            foreach(UdonBehaviour udonBehaviour in _udonBehaviours)
            {
                if(udonBehaviour.programSource != updatedProgramSource)
                {
                    continue;
                }

                udonBehaviour.RefreshProgram();

                if(!PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                {
                    continue;
                }

                string prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(udonBehaviour);
                if(!prefabAssetPaths.Contains(prefabAssetPath))
                {
                    prefabAssetPaths.Add(prefabAssetPath);
                }
            }

            RefreshPrefabUdonBehaviours(prefabAssetPaths.ToList());
        }

        [PublicAPI]
        public static void RefreshAllPrefabUdonBehaviours()
        {
            RefreshPrefabUdonBehaviours(GetAllPrefabAssetPaths());
        }

        private static void RefreshPrefabUdonBehaviours(List<string> prefabAssetPaths)
        {
            if(prefabAssetPaths.Count == 0)
            {
                return;
            }

            foreach((string prefabPath, int index) in prefabAssetPaths.Select((item, index) => (item, index)))
            {
                using(EditPrefabAssetScope editScope = new EditPrefabAssetScope(prefabPath))
                {
                    if(!editScope.IsEditable)
                    {
                        continue;
                    }

                    UdonBehaviour[] udonBehaviours = editScope.PrefabRoot.GetComponentsInChildren<UdonBehaviour>();
                    if(udonBehaviours.Length <= 0)
                    {
                        continue;
                    }

                    foreach(UdonBehaviour udonBehaviour in udonBehaviours)
                    {
                        udonBehaviour.RefreshProgram();
                    }

                    editScope.MarkDirty();
                }
            }
        }

        #endregion

        #region Scene Manager Callbacks

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            foreach(UdonBehaviour udonBehaviour in Object.FindObjectsOfType<UdonBehaviour>())
            {
                if(udonBehaviour.gameObject.scene != scene)
                {
                    continue;
                }

                udonBehaviour.RefreshProgram();

                if(_udonBehaviours.Contains(udonBehaviour))
                {
                    continue;
                }

                _udonBehaviours.Add(udonBehaviour);
            }
        }

        private void OnSceneSaving(Scene scene, string _)
        {
            RefreshQueuedProgramSources();

            _udonBehaviours.RemoveWhere(o => o == null);
            foreach(UdonBehaviour udonBehaviour in Object.FindObjectsOfType<UdonBehaviour>())
            {
                if(udonBehaviour.gameObject.scene != scene)
                {
                    continue;
                }

                udonBehaviour.RefreshProgram();

                if(_udonBehaviours.Contains(udonBehaviour))
                {
                    continue;
                }

                _udonBehaviours.Add(udonBehaviour);
            }
        }

        private void OnSceneClosing(Scene scene, bool removingScene)
        {
            RefreshQueuedProgramSources();

            foreach(UdonBehaviour udonBehaviour in Object.FindObjectsOfType<UdonBehaviour>())
            {
                if(udonBehaviour.gameObject.scene != scene)
                {
                    continue;
                }

                udonBehaviour.RefreshProgram();

                if(_udonBehaviours.Contains(udonBehaviour))
                {
                    continue;
                }

                _udonBehaviours.Remove(udonBehaviour);
            }
        }

        #endregion

        #region PlayMode Callback

        private void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if(playModeStateChange != PlayModeStateChange.ExitingEditMode && playModeStateChange != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            RefreshQueuedProgramSources();

            _udonBehaviours.Clear();
            _udonBehaviours.UnionWith(Object.FindObjectsOfType<UdonBehaviour>());
            foreach(UdonBehaviour udonBehaviour in _udonBehaviours)
            {
                udonBehaviour.RefreshProgram();
            }
        }

        #endregion

        #region IUdonEditorInterface Methods

        public IUdonVM ConstructUdonVM()
        {
            return _udonEditorInterface.ConstructUdonVM();
        }

        public IUdonProgram Assemble(string assembly)
        {
            return _udonEditorInterface.Assemble(assembly);
        }

        public IUdonWrapper GetWrapper()
        {
            return _udonEditorInterface.GetWrapper();
        }

        public void RegisterWrapperModule(IUdonWrapperModule wrapperModule)
        {
            _udonEditorInterface.RegisterWrapperModule(wrapperModule);
        }

        public IUdonHeap ConstructUdonHeap()
        {
            return _udonEditorInterface.ConstructUdonHeap();
        }

        public IUdonHeap ConstructUdonHeap(uint heapSize)
        {
            return _udonEditorInterface.ConstructUdonHeap(heapSize);
        }

        public string CompileGraph(
            IUdonCompilableGraph graph, INodeRegistry nodeRegistry,
            out Dictionary<string, (string uid, string fullName, int index)> linkedSymbols,
            out Dictionary<string, (object value, Type type)> heapDefaultValues
        )
        {
            return _udonEditorInterface.CompileGraph(graph, nodeRegistry, out linkedSymbols, out heapDefaultValues);
        }

        public Type GetTypeFromTypeString(string typeString)
        {
            return _udonEditorInterface.GetTypeFromTypeString(typeString);
        }

        public void AddTypeResolver(IUAssemblyTypeResolver typeResolver)
        {
            _udonEditorInterface.AddTypeResolver(typeResolver);
        }

        public string[] DisassembleProgram(IUdonProgram program)
        {
            return _udonEditorInterface.DisassembleProgram(program);
        }

        public string DisassembleInstruction(IUdonProgram program, ref uint offset)
        {
            return _udonEditorInterface.DisassembleInstruction(program, ref offset);
        }

        public UdonNodeDefinition GetNodeDefinition(string identifier)
        {
            return _udonEditorInterface.GetNodeDefinition(identifier);
        }

        public IEnumerable<UdonNodeDefinition> GetNodeDefinitions()
        {
            return _udonEditorInterface.GetNodeDefinitions();
        }

        public Dictionary<string, INodeRegistry> GetNodeRegistries()
        {
            return _udonEditorInterface.GetNodeRegistries();
        }

        public IEnumerable<UdonNodeDefinition> GetNodeDefinitions(string baseIdentifier)
        {
            return _udonEditorInterface.GetNodeDefinitions(baseIdentifier);
        }

        #endregion

        #region Prefab Utilities

        private static List<string> GetAllPrefabAssetPaths()
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => path.EndsWith(".prefab"))
                .Where(path => path.StartsWith("Assets")).ToList();
        }

        private class EditPrefabAssetScope : IDisposable
        {
            private readonly string _assetPath;
            private readonly GameObject _prefabRoot;
            public GameObject PrefabRoot => _disposed ? null : _prefabRoot;

            private readonly bool _isEditable;
            public bool IsEditable => !_disposed && _isEditable;

            private bool _dirty = false;
            private bool _disposed;

            public EditPrefabAssetScope(string assetPath)
            {
                _assetPath = assetPath;
                _prefabRoot = PrefabUtility.LoadPrefabContents(_assetPath);
                _isEditable = !PrefabUtility.IsPartOfImmutablePrefab(_prefabRoot);
            }

            public void MarkDirty()
            {
                _dirty = true;
            }

            public void Dispose()
            {
                if(_disposed)
                {
                    return;
                }

                _disposed = true;

                if(_dirty)
                {
                    try
                    {
                        PrefabUtility.SaveAsPrefabAsset(_prefabRoot, _assetPath);
                    }
                    catch(Exception e)
                    {
                        Debug.LogError($"Failed to save changes to prefab at '{_assetPath}' due to exception '{e}'.");
                    }
                }

                PrefabUtility.UnloadPrefabContents(_prefabRoot);
            }
        }

        #endregion
    }
}
