﻿#if UNITY_EDITOR
using System;
using UnityEditor.Experimental.EditorVR.Modules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEditor.Experimental.EditorVR.UI.Button;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
    sealed class FilterButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IRayEnterHandler
    {
        const float k_HoverAlpha = 1;
        const float k_NormalAlpha = 0.95f;

        public Button button
        {
            get { return m_Button; }
        }

        [SerializeField]
        Button m_Button;

        [SerializeField]
        Image m_EyePanel;

        [SerializeField]
        Image m_Eye;

        [SerializeField]
        Image m_TextPanel;

        Transform m_InteractingRayOrigin;

        public Text text
        {
            get { return m_Text; }
        }

        [SerializeField]
        Text m_Text;

        public Color color
        {
            set
            {
                m_Eye.color = value;
                m_Text.color = value;
            }
        }

        public event Action<Transform> hovered;
        public event Action<Transform> clicked;

        void Awake()
        {
            m_Button.onClick.AddListener(OnButtonClicked);
        }

        void OnDestroy()
        {
            m_Button.onClick.RemoveAllListeners();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var c = m_EyePanel.color;
            c.a = k_HoverAlpha;
            m_EyePanel.color = c;
            m_TextPanel.color = c;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var c = m_EyePanel.color;
            c.a = k_NormalAlpha;
            m_EyePanel.color = c;
            m_TextPanel.color = c;
        }

        public void OnRayEnter(RayEventData eventData)
        {
            m_InteractingRayOrigin = eventData.rayOrigin;

            if (hovered != null)
                hovered(eventData.rayOrigin);
        }

        void OnButtonClicked()
        {
            if (clicked != null)
                clicked(m_InteractingRayOrigin);
        }
    }
}
#endif
