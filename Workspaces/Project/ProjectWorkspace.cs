﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.Handles;
using UnityEngine.VR.Modules;
using UnityEngine.VR.Utilities;
using UnityEngine.VR.Workspaces;
using UnityObject = UnityEngine.Object;

public class ProjectWorkspace : Workspace, IPlaceObjects, IPreview, IProjectFolderList, IFilterUI, ISpatialHash
{
	const float kLeftPaneRatio = 0.3333333f; // Size of left pane relative to workspace bounds
	const float kPaneMargin = 0.01f;
	const float kPanelMargin = 0.01f;
	const float kScrollMargin = 0.03f;
	const float kYBounds = 0.2f;

	const float kMinScale = 0.03f;
	const float kMaxScale = 0.2f;

	bool m_AssetGridDragging;
	bool m_FolderPanelDragging;
	Transform m_AssetGridHighlightContainer;
	Transform m_FolderPanelHighlightContainer;

	[SerializeField]
	GameObject m_ContentPrefab;

	[SerializeField]
	GameObject m_SliderPrefab;

	[SerializeField]
	GameObject m_FilterPrefab;

	ProjectUI m_ProjectUI;
	FilterUI m_FilterUI;

	Vector3 m_ScrollStart;
	float m_ScrollOffsetStart;
	FolderData m_OpenFolder;

	public Action<Transform, Vector3> placeObject { private get; set; }

	public Func<Transform, Transform> getPreviewOriginForRayOrigin { private get; set; }
	public PreviewDelegate preview { private get; set; }

	public FolderData[] folderData
	{
		private get { return m_ProjectUI.folderListView.data; }
		set
		{
			var oldData = m_ProjectUI.folderListView.data;
			if (oldData != null)
				CopyExpandStates(oldData[0], value[0]);

			m_ProjectUI.folderListView.data = value;
			SelectFolder(m_OpenFolder != null ? GetFolderDataByInstanceID(value[0], m_OpenFolder.instanceID) : value[0]);
		}
	}
	public Func<FolderData[]> getFolderData { private get; set; }

	public List<string> filterList
	{
		set { m_FilterUI.filterList = value; }
	}
	public Func<List<string>> getFilterList { private get; set; }

	public Action<UnityObject> addObjectToSpatialHash { get; set; }
	public Action<UnityObject> removeObjectFromSpatialHash { get; set; }

	public override void Setup()
	{
		// Initial bounds must be set before the base.Setup() is called
		minBounds = new Vector3(kMinBounds.x, kMinBounds.y, 0.5f);
		m_CustomStartingBounds = minBounds;

		base.Setup();

		topPanelDividerOffset = -0.2875f; // enable & position the top-divider(mask) slightly to the left of workspace center

		var contentPrefab = U.Object.Instantiate(m_ContentPrefab, m_WorkspaceUI.sceneContainer, false);
		m_ProjectUI = contentPrefab.GetComponent<ProjectUI>();

		var filterPrefab = U.Object.Instantiate(m_FilterPrefab, m_WorkspaceUI.frontPanel, false);
		m_FilterUI = filterPrefab.GetComponent<FilterUI>();
		m_FilterUI.filterList = getFilterList();

		var sliderPrefab = U.Object.Instantiate(m_SliderPrefab, m_WorkspaceUI.frontPanel, false);
		var zoomSlider = sliderPrefab.GetComponent<ZoomSliderUI>();
		zoomSlider.zoomSlider.minValue = kMinScale;
		zoomSlider.zoomSlider.maxValue = kMaxScale;
		zoomSlider.zoomSlider.value = m_ProjectUI.assetListView.scaleFactor;
		zoomSlider.sliding += Scale;

		m_ProjectUI.folderListView.selectFolder = SelectFolder;

		var assetListView = m_ProjectUI.assetListView;
		assetListView.testFilter = TestFilter;
		assetListView.placeObject = placeObject;
		assetListView.getPreviewOriginForRayOrigin = getPreviewOriginForRayOrigin;
		assetListView.preview = preview;
		assetListView.addObjectToSpatialHash = addObjectToSpatialHash;
		assetListView.removeObjectFromSpatialHash = removeObjectFromSpatialHash;

		folderData = getFolderData();

		var scrollHandles = new[]
		{
			m_ProjectUI.folderScrollHandle,
			m_ProjectUI.assetScrollHandle
		};
		foreach (var handle in scrollHandles)
		{
			// Scroll Handle shouldn't move on bounds change
			handle.transform.parent = m_WorkspaceUI.sceneContainer;

			handle.dragStarted += OnScrollDragStarted;
			handle.dragging += OnScrollDragging;
			handle.dragEnded += OnScrollDragEnded;
		}

		// Hookup highlighting calls
		m_ProjectUI.assetScrollHandle.dragStarted += OnAssetGridDragHighlightBegin;
		m_ProjectUI.assetScrollHandle.dragEnded += OnAssetGridDragHighlightEnd;
		m_ProjectUI.assetScrollHandle.hoverStarted += OnAssetGridHoverHighlightBegin;
		m_ProjectUI.assetScrollHandle.hoverEnded += OnAssetGridHoverHighlightEnd;
		m_ProjectUI.folderScrollHandle.dragStarted += OnFolderPanelDragHighlightBegin;
		m_ProjectUI.folderScrollHandle.dragEnded += OnFolderPanelDragHighlightEnd;
		m_ProjectUI.folderScrollHandle.hoverStarted += OnFolderPanelHoverHighlightBegin;
		m_ProjectUI.folderScrollHandle.hoverEnded += OnFolderPanelHoverHighlightEnd;

		// Assign highlight references
		m_FolderPanelHighlightContainer = m_ProjectUI.folderPanelHighlight.transform.parent.transform;
		m_AssetGridHighlightContainer = m_ProjectUI.assetGridHighlight.transform.parent.transform;

		// Propagate initial bounds
		OnBoundsChanged();
	}

	protected override void OnBoundsChanged()
	{
		const float kSideScollBoundsShrinkAmount = 0.03f;
		const float depthCompensation = 0.1375f;

		Bounds bounds = contentBounds;
		Vector3 size = bounds.size;
		size.x -= kPaneMargin * 2;
		size.x *= kLeftPaneRatio;
		size.y = kYBounds;
		size.z = size.z - depthCompensation;
		bounds.size = size;
		bounds.center = Vector3.zero;

		var halfScrollMargin = kScrollMargin * 0.5f;
		var doubleScrollMargin = kScrollMargin * 2;
		var xOffset = (contentBounds.size.x - size.x + kPaneMargin) * -0.5f;
		var folderScrollHandleXPositionOffset = 0.025f;
		var folderScrollHandleXScaleOffset = 0.015f;

		var folderScrollHandleTransform = m_ProjectUI.folderScrollHandle.transform;
		folderScrollHandleTransform.localPosition = new Vector3(xOffset - halfScrollMargin + folderScrollHandleXPositionOffset, -folderScrollHandleTransform.localScale.y * 0.5f, 0);
		folderScrollHandleTransform.localScale = new Vector3(size.x + kScrollMargin + folderScrollHandleXScaleOffset, folderScrollHandleTransform.localScale.y, size.z + doubleScrollMargin);

		var folderListView = m_ProjectUI.folderListView;
		size.x -= kSideScollBoundsShrinkAmount; // set narrow x bounds for scrolling region on left side of folder list view
		bounds.size = size;
		folderListView.bounds = bounds;
		folderListView.PreCompute(); // Compute item size
		folderListView.transform.localPosition = new Vector3(xOffset + (kSideScollBoundsShrinkAmount / 2.2f), folderListView.itemSize.y * 0.5f, 0);

		var folderPanel = m_ProjectUI.folderPanel;
		folderPanel.transform.localPosition = xOffset * Vector3.right;
		folderPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + kPanelMargin);
		folderPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.z + kPanelMargin);

		m_FolderPanelHighlightContainer.localScale = new Vector3(size.x + kSideScollBoundsShrinkAmount, 1f, size.z);

		size = contentBounds.size;
		size.x -= kPaneMargin * 2;
		size.x *= 1 - kLeftPaneRatio;
		size.z = size.z - depthCompensation;
		bounds.size = size;

		xOffset = (contentBounds.size.x - size.x + kPaneMargin) * 0.5f;

		var assetScrollHandleTransform = m_ProjectUI.assetScrollHandle.transform;
		assetScrollHandleTransform.localPosition = new Vector3(xOffset + halfScrollMargin, -assetScrollHandleTransform.localScale.y * 0.5f);
		assetScrollHandleTransform.localScale = new Vector3(size.x + kScrollMargin, assetScrollHandleTransform.localScale.y, size.z + doubleScrollMargin);

		var assetListView = m_ProjectUI.assetListView;
		assetListView.bounds = bounds;
		assetListView.PreCompute(); // Compute item size
		assetListView.transform.localPosition = Vector3.right * xOffset;

		var assetPanel = m_ProjectUI.assetPanel;
		assetPanel.transform.localPosition = xOffset * Vector3.right;
		assetPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + kPanelMargin);
		assetPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.z + kPanelMargin);

		m_AssetGridHighlightContainer.localScale = new Vector3(size.x, 1f, size.z);
	}

	void SelectFolder(FolderData data)
	{
		if (data == m_OpenFolder)
			return;

		m_OpenFolder = data;
		m_ProjectUI.folderListView.ClearSelected();
		data.selected = true;
		m_ProjectUI.assetListView.data = data.assets;
		m_ProjectUI.assetListView.scrollOffset = 0;
	}

	void OnScrollDragStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_ScrollStart = eventData.rayOrigin.transform.position;
		if (handle == m_ProjectUI.folderScrollHandle)
		{
			m_ScrollOffsetStart = m_ProjectUI.folderListView.scrollOffset;
			m_ProjectUI.folderListView.OnBeginScrolling();
		}
		else if (handle == m_ProjectUI.assetScrollHandle)
		{
			m_ScrollOffsetStart = m_ProjectUI.assetListView.scrollOffset;
			m_ProjectUI.assetListView.OnBeginScrolling();
		}
	}

	void OnScrollDragging(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		Scroll(handle, eventData);
	}

	void OnScrollDragEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		Scroll(handle, eventData);
		if (handle == m_ProjectUI.folderScrollHandle)
		{
			m_ScrollOffsetStart = m_ProjectUI.folderListView.scrollOffset;
			m_ProjectUI.folderListView.OnScrollEnded();
		}
		else if (handle == m_ProjectUI.assetScrollHandle)
		{
			m_ScrollOffsetStart = m_ProjectUI.assetListView.scrollOffset;
			m_ProjectUI.assetListView.OnScrollEnded();
		}
	}

	void Scroll(BaseHandle handle, HandleEventData eventData)
	{
		var scrollOffset = m_ScrollOffsetStart + Vector3.Dot(m_ScrollStart - eventData.rayOrigin.transform.position, transform.forward);
		if (handle == m_ProjectUI.folderScrollHandle)
			m_ProjectUI.folderListView.scrollOffset = scrollOffset;
		else if (handle == m_ProjectUI.assetScrollHandle)
			m_ProjectUI.assetListView.scrollOffset = scrollOffset;
	}

	void OnAssetGridDragHighlightBegin(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		m_AssetGridDragging = true;
		m_ProjectUI.assetGridHighlight.visible = true;
	}

	void OnAssetGridDragHighlightEnd(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		m_AssetGridDragging = false;
		m_ProjectUI.assetGridHighlight.visible = false;
	}

	void OnAssetGridHoverHighlightBegin(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_ProjectUI.assetGridHighlight.visible = true;
	}

	void OnAssetGridHoverHighlightEnd(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		if (!m_AssetGridDragging)
			m_ProjectUI.assetGridHighlight.visible = false;
	}

	void OnFolderPanelDragHighlightBegin(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_FolderPanelDragging = true;
		m_ProjectUI.folderPanelHighlight.visible = true;
	}

	void OnFolderPanelDragHighlightEnd(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_FolderPanelDragging = false;
		m_ProjectUI.folderPanelHighlight.visible = false;
	}

	void OnFolderPanelHoverHighlightBegin(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		m_ProjectUI.folderPanelHighlight.visible = true;
	}

	void OnFolderPanelHoverHighlightEnd(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (!m_FolderPanelDragging)
			m_ProjectUI.folderPanelHighlight.visible = false;
	}

	void Scale(float value)
	{
		m_ProjectUI.assetListView.scaleFactor = value;
	}

	bool TestFilter(string type)
	{
		return FilterUI.TestFilter(m_FilterUI.searchQuery, type);
	}

	FolderData GetFolderDataByInstanceID(FolderData data, int instanceID)
	{
		if (data.instanceID == instanceID)
			return data;

		if (data.children != null)
		{
			foreach (var child in data.children)
			{
				var folder = GetFolderDataByInstanceID(child, instanceID);
				if (folder != null)
					return folder;
			}
		}
		return null;
	}

	// Not used, but could be helpful
	bool ExpandToFolder(FolderData container, FolderData search)
	{
		if (container.instanceID == search.instanceID)
			return true;

		bool found = false;

		if (container.children != null)
		{
			foreach (var child in container.children)
			{
				if (ExpandToFolder(child, search))
					found = true;
			}
		}

		if (found)
			container.expanded = true;

		return found;
	}

	// In case a folder was moved up the hierarchy, we must search the entire destination root for every source folder
	void CopyExpandStates(FolderData source, FolderData destinationRoot)
	{
		var match = GetFolderDataByInstanceID(destinationRoot, source.instanceID);
		if (match != null)
			match.expanded = source.expanded;

		if (source.children != null)
		{
			foreach (var child in source.children)
			{
				CopyExpandStates(child, destinationRoot);
			}
		}
	}
}