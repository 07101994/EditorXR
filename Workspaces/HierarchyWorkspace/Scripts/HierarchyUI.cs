﻿#if UNITY_EDITOR
using UnityEditor.Experimental.EditorVR.Handles;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
	sealed class HierarchyUI : MonoBehaviour
	{
		public HierarchyListViewController listView
		{
			get { return m_ListView; }
		}
		[SerializeField]
		HierarchyListViewController m_ListView;

		public BaseHandle scrollHandle
		{
			get { return m_ScrollHandle; }
		}
		[SerializeField]
		BaseHandle m_ScrollHandle;

		public RectTransform listPanel
		{
			get { return m_ListPanel; }
		}
		[SerializeField]
		RectTransform m_ListPanel;
	}
}
#endif
