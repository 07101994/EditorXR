﻿#if UNITY_EDITOR && UNITY_2017_2_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.XR;

namespace UnityEditor.Experimental.EditorVR.Core
{
    public struct ContextSettings
    {
        public bool copySceneCameraSettings;
        public bool supportCameraFX;

        public ContextSettings(bool copySceneCameraSettings, bool supportCameraFX)
        {
            this.copySceneCameraSettings = copySceneCameraSettings;
            this.supportCameraFX = supportCameraFX;
        }
    }

    [CreateAssetMenu(menuName = "EditorVR/EditorVR Context")]
    class EditorVRContext : ScriptableObject, IEditingContext
    {
        [SerializeField]
        float m_RenderScale = 1f;

        [SerializeField]
        bool m_CopySceneCameraSettings = true;

        [SerializeField]
        bool m_SupportCameraFX = true;

        [SerializeField]
        internal List<MonoScript> m_DefaultToolStack;

        EditorVR m_Instance;

        public ContextSettings contextSettings { get { return new ContextSettings(m_CopySceneCameraSettings, m_SupportCameraFX); } }

        public void Setup()
        {
            EditorVR.defaultTools = m_DefaultToolStack.Select(ms => ms.GetClass()).ToArray();
            m_Instance = ObjectUtils.CreateGameObjectWithComponent<EditorVR>();
            XRSettings.eyeTextureResolutionScale = m_RenderScale;
        }

        public void Dispose()
        {
            if (m_Instance)
            {
                m_Instance.Shutdown(); // Give a chance for dependent systems (e.g. serialization) to shut-down before destroying
                ObjectUtils.Destroy(m_Instance.gameObject);
                m_Instance = null;
            }
        }
    }
}
#endif
