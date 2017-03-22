﻿#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Handles;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
	[MainMenuItem("Hierarchy", "Workspaces", "View all GameObjects in your scene(s)")]
	class HierarchyWorkspace : Workspace, IFilterUI, IUsesHierarchyData, ISelectionChanged, IMoveCameraRig
	{
		[SerializeField]
		GameObject m_ContentPrefab;

		[SerializeField]
		GameObject m_FilterPrefab;

		[SerializeField]
		GameObject m_FocusPrefab;

		[SerializeField]
		GameObject m_CreateEmptyPrefab;

		HierarchyUI m_HierarchyUI;
		FilterUI m_FilterUI;

		HierarchyData m_SelectedRow;

		bool m_Scrolling;

		public List<HierarchyData> hierarchyData
		{
			set
			{
				m_HierarchyData = value;

				if (m_HierarchyUI)
					m_HierarchyUI.listView.data = value;
			}
		}

		List<HierarchyData> m_HierarchyData;

		public List<string> filterList
		{
			set
			{
				m_FilterList = value;

				if (m_FilterUI)
					m_FilterUI.filterList = value;
			}
		}

		List<string> m_FilterList;

		public MoveCameraRigDelegate moveCameraRig { private get; set; }

		public string searchQuery { get { return m_FilterUI.searchQuery; } }

		public override void Setup()
		{
			// Initial bounds must be set before the base.Setup() is called
			minBounds = new Vector3(0.375f, k_MinBounds.y, 0.5f);
			m_CustomStartingBounds = minBounds;

			base.Setup();

			var contentPrefab = ObjectUtils.Instantiate(m_ContentPrefab, m_WorkspaceUI.sceneContainer, false);
			m_HierarchyUI = contentPrefab.GetComponent<HierarchyUI>();
			hierarchyData = m_HierarchyData;

			m_FilterUI = ObjectUtils.Instantiate(m_FilterPrefab, m_WorkspaceUI.frontPanel, false).GetComponent<FilterUI>();
			foreach (var mb in m_FilterUI.GetComponentsInChildren<MonoBehaviour>())
			{
				connectInterfaces(mb);
			}
			m_FilterUI.filterList = m_FilterList;

			var focusUI = ObjectUtils.Instantiate(m_FocusPrefab, m_WorkspaceUI.frontPanel, false);
			foreach (var mb in focusUI.GetComponentsInChildren<MonoBehaviour>())
			{
				connectInterfaces(mb);
			}
			focusUI.GetComponentInChildren<Button>(true).onClick.AddListener(FocusSelection);

			var createEmptyUI = ObjectUtils.Instantiate(m_CreateEmptyPrefab, m_WorkspaceUI.frontPanel, false);
			foreach (var mb in createEmptyUI.GetComponentsInChildren<MonoBehaviour>())
			{
				connectInterfaces(mb);
			}
			createEmptyUI.GetComponentInChildren<Button>(true).onClick.AddListener(CreateEmptyGameObject);

			var listView = m_HierarchyUI.listView;
			listView.selectRow = SelectRow;
			listView.matchesFilter = this.MatchesFilter;
			listView.getSearchQuery = () => searchQuery;
			connectInterfaces(listView);

			var scrollHandle = m_HierarchyUI.scrollHandle;
			scrollHandle.dragStarted += OnScrollDragStarted;
			scrollHandle.dragging += OnScrollDragging;
			scrollHandle.dragEnded += OnScrollDragEnded;
			scrollHandle.hoverStarted += OnScrollHoverStarted;
			scrollHandle.hoverEnded += OnScrollHoverEnded;

			contentBounds = new Bounds(Vector3.zero, m_CustomStartingBounds.Value);

			var scrollHandleTransform = m_HierarchyUI.scrollHandle.transform;
			scrollHandleTransform.SetParent(m_WorkspaceUI.topFaceContainer);
			scrollHandleTransform.localScale = new Vector3(1.03f, 0.02f, 1.02f); // Extra space for scrolling
			scrollHandleTransform.localPosition = new Vector3(0f, -0.01f, 0f); // Offset from content for collision purposes

			// Propagate initial bounds
			OnBoundsChanged();
		}

		protected override void OnBoundsChanged()
		{
			var size = contentBounds.size;
			var listView = m_HierarchyUI.listView;
			var bounds = contentBounds;
			size.y = float.MaxValue; // Add height for dropdowns
			size.x -= 0.04f; // Shrink the content width, so that there is space allowed to grab and scroll
			size.z -= 0.15f; // Reduce the height of the inspector contents as to fit within the bounds of the workspace
			bounds.size = size;
			listView.bounds = bounds;

			var listPanel = m_HierarchyUI.listPanel;
			listPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
			listPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.z);
	}

		static void SelectRow(int index)
		{
#if UNITY_EDITOR
			var gameObject = EditorUtility.InstanceIDToObject(index) as GameObject;
			if (gameObject && Selection.activeGameObject != gameObject)
				Selection.activeGameObject = gameObject;
			else
				Selection.activeGameObject = null;
#endif
		}

		void OnScrollDragStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			m_Scrolling = true;

			m_WorkspaceUI.topHighlight.visible = true;
			m_WorkspaceUI.amplifyTopHighlight = false;

			m_HierarchyUI.listView.OnBeginScrolling();
		}

		void OnScrollDragging(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			m_HierarchyUI.listView.scrollOffset -= Vector3.Dot(eventData.deltaPosition, handle.transform.forward) / getViewerScale();
		}

		void OnScrollDragEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			m_Scrolling = false;

			m_WorkspaceUI.topHighlight.visible = false;

			m_HierarchyUI.listView.OnScrollEnded();
		}

		void OnScrollHoverStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (!m_Scrolling)
			{
				m_WorkspaceUI.topHighlight.visible = true;
				m_WorkspaceUI.amplifyTopHighlight = true;
			}
		}

		void OnScrollHoverEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (!m_Scrolling)
			{
				m_WorkspaceUI.topHighlight.visible = false;
				m_WorkspaceUI.amplifyTopHighlight = false;
			}
		}

		public void OnSelectionChanged()
		{
			m_HierarchyUI.listView.SelectRow(Selection.activeInstanceID);
		}

		void FocusSelection()
		{
			if (Selection.gameObjects.Length == 0)
				return;

			var mainCamera = CameraUtils.GetMainCamera().transform;
			var bounds = ObjectUtils.GetBounds(Selection.gameObjects);

			var size = bounds.size;
			size.y = 0;
			var maxSize = size.MaxComponent();

			const float kExtraDistance = 0.25f; // Add some extra distance so selection isn't in your face
			maxSize += kExtraDistance;

			var viewDirection = mainCamera.transform.forward;
			viewDirection.y = 0;
			viewDirection.Normalize();

			var cameraDiff = mainCamera.position - CameraUtils.GetCameraRig().position;
			cameraDiff.y = 0;

			moveCameraRig(bounds.center - cameraDiff - viewDirection * maxSize);
		}

		static void CreateEmptyGameObject()
		{
			var camera = CameraUtils.GetMainCamera().transform;
			var go = new GameObject();
			go.transform.position = camera.position + camera.forward;
			Selection.activeGameObject = go;
		}
	}
}

#endif
