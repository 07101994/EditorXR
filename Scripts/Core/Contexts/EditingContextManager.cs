#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputNew;
using System.IO;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.Experimental.EditorVR.Core
{
    [InitializeOnLoad]
    sealed class EditingContextManager : MonoBehaviour
    {
        [SerializeField]
        static UnityObject m_DefaultContext;

        internal const string k_SettingsPath = "ProjectSettings/EditingContextManagerSettings.asset";
        internal const string k_UserSettingsPath = "Library/EditingContextManagerSettings.asset";

        const string k_LaunchOnExitPlaymode = "EditingContextManager.LaunchOnExitPlaymode";

        internal static EditingContextManager s_Instance;
        static InputManager s_InputManager;
        static List<IEditingContext> s_AvailableContexts;
        static EditingContextManagerSettings s_Settings;
        static IEditingContext s_CurrentContext;

        string[] m_ContextNames;
        int m_SelectedContextIndex;

        readonly List<IEditingContext> m_PreviousContexts = new List<IEditingContext>();

        internal static IEditingContext defaultContext
        {
            get
            {
                var context = s_AvailableContexts.Find(c => c.Equals(m_DefaultContext)) ?? s_AvailableContexts.First();

                var defaultContextName = s_Settings.defaultContextName;
                if (!string.IsNullOrEmpty(defaultContextName))
                {
                    var foundContext = s_AvailableContexts.Find(c => c.name == defaultContextName);
                    if (foundContext != null)
                        context = foundContext;
                }

                return context;
            }
            set
            {
                s_Settings.defaultContextName = value.name;
            }
        }

        internal IEditingContext currentContext
        {
            get { return s_CurrentContext; }
        }

        static EditingContextManager()
        {
            VRView.viewEnabled += OnVRViewEnabled;
            VRView.viewDisabled += OnVRViewDisabled;
            VRView.cameraSetupStarted += OnCameraSetupStarted;

            EditorApplication.update += ReopenOnExitPlaymode;
        }

        static void OnVRViewEnabled()
        {
            InitializeInputManager();
            s_Instance = ObjectUtils.CreateGameObjectWithComponent<EditingContextManager>();
        }

        static void OnVRViewDisabled()
        {
            ObjectUtils.Destroy(s_Instance.gameObject);
            ObjectUtils.Destroy(s_InputManager.gameObject);
        }

        public static ContextSettings OnCameraSetupStarted()
        {
            ContextSettings settings = new ContextSettings();
            if (s_CurrentContext != null)
                settings = s_CurrentContext != null ? s_CurrentContext.contextSettings : defaultContext.contextSettings;

            return settings;
        }

        [MenuItem("Window/EditorXR %e", false)]
        internal static void ShowEditorVR()
        {
            // Using a utility window improves performance by saving from the overhead of DockArea.OnGUI()
            EditorWindow.GetWindow<VRView>(true, "EditorXR", true);
        }

        [MenuItem("Window/EditorXR %e", true)]
        static bool ShouldShowEditorVR()
        {
            return PlayerSettings.virtualRealitySupported;
        }

        [MenuItem("Edit/Project Settings/EditorXR/Default Editing Context")]
        static void EditProjectSettings()
        {
            var settings = LoadProjectSettings();
            settings.name = "Default Editing Context";
            Selection.activeObject = settings;
        }

        // Life cycle management across playmode switches is an odd beast indeed, and there is a need to reliably relaunch
        // EditorVR after we switch back out of playmode (assuming the view was visible before a playmode switch). So,
        // we watch until playmode is done and then relaunch.  
        static void ReopenOnExitPlaymode()
        {
            var launch = EditorPrefs.GetBool(k_LaunchOnExitPlaymode, false);
            if (!launch || !EditorApplication.isPlaying)
            {
                EditorPrefs.DeleteKey(k_LaunchOnExitPlaymode);
                EditorApplication.update -= ReopenOnExitPlaymode;
                if (launch)
                    EditorApplication.delayCall += ShowEditorVR;
            }
        }

#if UNITY_2017_2_OR_NEWER
        static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode)
            {
                EditorPrefs.SetBool(k_LaunchOnExitPlaymode, true);
                var view = VRView.activeView;
                if (view)
                    view.Close();
            }
        }

        void OnEnable()
        {
            s_Settings = LoadUserSettings();

            ISetEditingContextMethods.getAvailableEditingContexts = GetAvailableEditingContexts;
            ISetEditingContextMethods.getPreviousEditingContexts = GetPreviousEditingContexts;
            ISetEditingContextMethods.setEditingContext = SetEditingContext;
            ISetEditingContextMethods.restorePreviousEditingContext = RestorePreviousContext;

            var availableContexts = GetAvailableEditingContexts();
            m_ContextNames = availableContexts.Select(c => c.name).ToArray();

            if (s_AvailableContexts.Count == 0)
                throw new Exception("You can't start EditorXR without at least one context. Try re-importing the package or use version control to restore the default context asset");

            SetEditingContext(defaultContext);

            if (s_AvailableContexts.Count > 1)
                VRView.afterOnGUI += OnVRViewGUI;

            // Force the window to repaint every tick, since we need live updating
            // This also allows scripts with [ExecuteInEditMode] to run
            EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;

            VRView.afterOnGUI -= OnVRViewGUI;

            if (s_CurrentContext != null)
            {
                defaultContext = s_CurrentContext;
                s_CurrentContext.Dispose();
            }

            s_AvailableContexts = null;

            ISetEditingContextMethods.getAvailableEditingContexts = null;
            ISetEditingContextMethods.getPreviousEditingContexts = null;
            ISetEditingContextMethods.setEditingContext = null;
            ISetEditingContextMethods.restorePreviousEditingContext = null;

            SaveUserSettings(s_Settings);
        }
#endif

        void OnVRViewGUI(EditorWindow window)
        {
            var view = (VRView)window;
            GUILayout.BeginArea(view.guiRect);
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                {
                    DoGUI(m_ContextNames, ref m_SelectedContextIndex, () => SetEditingContext(s_AvailableContexts[m_SelectedContextIndex]));
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        internal static void DoGUI(string[] contextNames, ref int selectedContextIndex, Action callback = null)
        {
            selectedContextIndex = EditorGUILayout.Popup(string.Empty, selectedContextIndex, contextNames);
            if (GUI.changed)
            {
                if (callback != null)
                    callback();
                GUIUtility.ExitGUI();
            }
        }

        internal void SetEditingContext(IEditingContext context)
        {
            if (context == null)
                return;

            if (s_CurrentContext != null)
            {
                m_PreviousContexts.Insert(0, s_CurrentContext);
                s_CurrentContext.Dispose();
            }

            context.Setup();
            s_CurrentContext = context;

            m_SelectedContextIndex = s_AvailableContexts.IndexOf(context);
        }

        internal void RestorePreviousContext()
        {
            if (m_PreviousContexts.Count > 0)
                SetEditingContext(m_PreviousContexts.First());
        }

        static List<IEditingContext> GetEditingContextAssets()
        {
            var types = ObjectUtils.GetImplementationsOfInterface(typeof(IEditingContext));
            var searchString = "t: " + string.Join(" t: ", types.Select(t => t.FullName).ToArray());
            var assets = AssetDatabase.FindAssets(searchString);

            var availableContexts = new List<IEditingContext>();
            foreach (var asset in assets)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(asset);
                var context = AssetDatabase.LoadMainAssetAtPath(assetPath) as IEditingContext;
                availableContexts.Add(context);
            }

            return availableContexts;
        }

        internal static string[] GetEditingContextNames()
        {
            var availableContexts = GetEditingContextAssets();
            return availableContexts.Select(c => c.name).ToArray();
        }

        static List<IEditingContext> GetAvailableEditingContexts()
        {
            if (s_AvailableContexts == null)
                s_AvailableContexts = GetEditingContextAssets();

            return s_AvailableContexts;
        }

        List<IEditingContext> GetPreviousEditingContexts()
        {
            return m_PreviousContexts;
        }

        internal static EditingContextManagerSettings LoadProjectSettings()
        {
            EditingContextManagerSettings settings = ScriptableObject.CreateInstance<EditingContextManagerSettings>();
            if (File.Exists(k_SettingsPath))
                JsonUtility.FromJsonOverwrite(File.ReadAllText(k_SettingsPath), settings);

            return settings;
        }

        internal static EditingContextManagerSettings LoadUserSettings()
        {
            EditingContextManagerSettings settings;
            if (File.Exists(k_UserSettingsPath)
                && File.GetLastWriteTime(k_UserSettingsPath) > File.GetLastWriteTime(k_SettingsPath))
            {
                settings = ScriptableObject.CreateInstance<EditingContextManagerSettings>();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(k_UserSettingsPath), settings);
            }
            else
                settings = LoadProjectSettings();

            return settings;
        }

        internal static void ResetProjectSettings()
        {
            File.Delete(k_UserSettingsPath);

            if (EditorUtility.DisplayDialog("Delete Project Settings?", "Would you like to remove the project-wide settings, too?", "Yes", "No"))
                File.Delete(k_SettingsPath);
        }

        internal static void SaveProjectSettings(EditingContextManagerSettings settings)
        {
            File.WriteAllText(k_SettingsPath, JsonUtility.ToJson(settings, true));
        }

        internal static void SaveUserSettings(EditingContextManagerSettings settings)
        {
            File.WriteAllText(k_UserSettingsPath, JsonUtility.ToJson(settings, true));
        }

        static void InitializeInputManager()
        {
            // HACK: InputSystem has a static constructor that is relied upon for initializing a bunch of other components, so
            // in edit mode we need to handle lifecycle explicitly
            var managers = Resources.FindObjectsOfTypeAll<InputManager>();
            foreach (var m in managers)
            {
                ObjectUtils.Destroy(m.gameObject);
            }

            managers = Resources.FindObjectsOfTypeAll<InputManager>();

            if (managers.Length == 0)
            {
                // Attempt creating object hierarchy via an implicit static constructor call by touching the class
                InputSystem.ExecuteEvents();
                managers = Resources.FindObjectsOfTypeAll<InputManager>();

                if (managers.Length == 0)
                {
                    typeof(InputSystem).TypeInitializer.Invoke(null, null);
                    managers = Resources.FindObjectsOfTypeAll<InputManager>();
                }
            }
            Assert.IsTrue(managers.Length == 1, "Only one InputManager should be active; Count: " + managers.Length);

            s_InputManager = managers[0];
            var go = s_InputManager.gameObject;
            go.hideFlags = ObjectUtils.hideFlags;
            ObjectUtils.SetRunInEditModeRecursively(go, true);

            // These components were allocating memory every frame and aren't currently used in EditorVR
            ObjectUtils.Destroy(s_InputManager.GetComponent<JoystickInputToEvents>());
            ObjectUtils.Destroy(s_InputManager.GetComponent<MouseInputToEvents>());
            ObjectUtils.Destroy(s_InputManager.GetComponent<KeyboardInputToEvents>());
            ObjectUtils.Destroy(s_InputManager.GetComponent<TouchInputToEvents>());
        }
    }
}
#endif
