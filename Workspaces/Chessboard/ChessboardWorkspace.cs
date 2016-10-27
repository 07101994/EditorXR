﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.Handles;
using UnityEngine.VR.Utilities;
using UnityEngine.VR.Workspaces;

public class ChessboardWorkspace : Workspace, IMiniWorld
{
	private static readonly float kInitReferenceYOffset = kDefaultBounds.y / 2.001f; // Show more space above ground than below
	private const float kInitReferenceScale = 25f; // We want to see a big region by default

	//TODO: replace with dynamic values once spatial hash lands
	// Scale slider min/max (maps to referenceTransform unifrom scale)
	private const float kMinScale = 0.1f;
	private const float kMaxScale = 35;

	[SerializeField]
	private GameObject m_ContentPrefab;

	[SerializeField]
	private GameObject m_UIPrefab;

	[SerializeField]
	private GameObject m_FilterPrefab;

	private ChessboardUI m_ChessboardUI;
	private MiniWorld m_MiniWorld;
	private Material m_GridMaterial;
	private ZoomSliderUI m_ZoomSliderUI;

	private readonly List<RayData> m_RayData = new List<RayData>(2);
	private float m_ScaleStartDistance;
	bool m_Dragging;

	private class RayData
	{
		public Transform rayOrigin;
		public Vector3 rayOriginStart;
		public Vector3 refTransformStartPosition;
		public Vector3 refTransformStartScale;
	}

	public Transform referenceTransform { get { return m_MiniWorld.referenceTransform; } }
	public Func<IMiniWorld, bool> preProcessRender { set { m_MiniWorld.preProcessRender = value; } }
	public Action<IMiniWorld> postProcessRender { set { m_MiniWorld.postProcessRender = value; } }
	public Vector3 miniWorldScale { get { return m_MiniWorld.miniWorldScale; } }
	public Transform miniWorldTransform { get { return m_MiniWorld.miniWorldTransform; } }
	public bool Contains(Vector3 position) { return m_MiniWorld.Contains(position); }
	public List<Renderer> ignoreList { set { m_MiniWorld.ignoreList = value;  } } 

	public override void Setup()
	{
		// Initial bounds must be set before the base.Setup() is called
		minBounds = new Vector3(kMinBounds.x, kMinBounds.y, 0.25f);
		m_CustomStartingBounds = new Vector3(kMinBounds.x, kMinBounds.y, 0.5f);

		base.Setup();

		U.Object.Instantiate(m_ContentPrefab, m_WorkspaceUI.sceneContainer, false);
		m_ChessboardUI = GetComponentInChildren<ChessboardUI>();
		m_GridMaterial = U.Material.GetMaterialClone(m_ChessboardUI.grid);

		// Set up MiniWorld
		m_MiniWorld = GetComponentInChildren<MiniWorld>();
		m_MiniWorld.referenceTransform.position = Vector3.up * kInitReferenceYOffset * kInitReferenceScale;
		m_MiniWorld.referenceTransform.localScale = Vector3.one * kInitReferenceScale;

		// Set up ControlBox
		var panZoomHandle = m_ChessboardUI.panZoomHandle;
		// ControlBox shouldn't move with miniWorld
		panZoomHandle.transform.parent = m_WorkspaceUI.sceneContainer;
		panZoomHandle.transform.localPosition = Vector3.down * panZoomHandle.transform.localScale.y * 0.5f;
		panZoomHandle.dragStarted += OnControlDragStarted;
		panZoomHandle.dragging += OnControlDragging;
		panZoomHandle.dragEnded += OnControlDragEnded;
		panZoomHandle.hoverStarted += OnControlHoverStarted;
		panZoomHandle.hoverEnded += OnControlHoverEnded;

		// Set up UI
		var UI = U.Object.Instantiate(m_UIPrefab, m_WorkspaceUI.frontPanel, false);
		m_ZoomSliderUI = UI.GetComponentInChildren<ZoomSliderUI>();
		m_ZoomSliderUI.sliding += OnSliding;
		m_ZoomSliderUI.zoomSlider.maxValue = kMaxScale;
		m_ZoomSliderUI.zoomSlider.minValue = kMinScale;
		m_ZoomSliderUI.zoomSlider.direction = UnityEngine.UI.Slider.Direction.RightToLeft; // Invert direction for expected ux; zoom in as slider moves left to right
		m_ZoomSliderUI.zoomSlider.value = kInitReferenceScale;

		// Propagate initial bounds
		OnBoundsChanged();
	}

	private void Update()
	{
		//Set grid height, deactivate if out of bounds
		float gridHeight = m_MiniWorld.referenceTransform.position.y / m_MiniWorld.referenceTransform.localScale.y;
		var grid = m_ChessboardUI.grid;
		if (Mathf.Abs(gridHeight) < contentBounds.extents.y)
		{
			grid.gameObject.SetActive(true);
			grid.transform.localPosition = Vector3.down * gridHeight;
		}
		else
		{
			grid.gameObject.SetActive(false);
		}

		// Update grid material if ClipBox has moved
		m_GridMaterial.mainTextureScale = new Vector2(
			m_MiniWorld.referenceTransform.localScale.x * contentBounds.size.x,
			m_MiniWorld.referenceTransform.localScale.z * contentBounds.size.z);
		m_GridMaterial.mainTextureOffset =
			Vector2.one * 0.5f // Center grid
			+ new Vector2(m_GridMaterial.mainTextureScale.x % 2, m_GridMaterial.mainTextureScale.y % 2) * -0.5f // Scaling offset
			+ new Vector2(m_MiniWorld.referenceTransform.position.x, m_MiniWorld.referenceTransform.position.z); // Translation offset
	}

	protected override void OnBoundsChanged()
	{
		m_MiniWorld.transform.localPosition = Vector3.up * contentBounds.extents.y;
		var correctedBounds = new Bounds(contentBounds.center, new Vector3(contentBounds.size.x, contentBounds.size.y, contentBounds.size.z - 0.14f));
		m_MiniWorld.localBounds = correctedBounds;

		m_ChessboardUI.boundsCube.transform.localScale = correctedBounds.size;

		m_ChessboardUI.grid.transform.localScale = new Vector3(correctedBounds.size.x, correctedBounds.size.z, 1);

		var controlBox = m_ChessboardUI.panZoomHandle;
		controlBox.transform.localScale = new Vector3(correctedBounds.size.x, controlBox.transform.localScale.y, correctedBounds.size.z);
	}

	private void OnSliding(float value)
	{
		ScaleMiniWorld(value);
	}

	void ScaleMiniWorld(float value)
	{
		var scaleDiff = (value - m_MiniWorld.referenceTransform.localScale.x) / m_MiniWorld.referenceTransform.localScale.x;
		m_MiniWorld.referenceTransform.position += Vector3.up * m_MiniWorld.referenceBounds.extents.y * scaleDiff;
		m_MiniWorld.referenceTransform.localScale = Vector3.one * value;
	}

	private void OnControlDragStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_Dragging = true;
		m_WorkspaceUI.topHighlight.visible = true;

		if (m_RayData.Count == 1) // On introduction of second ray
			m_ScaleStartDistance = (m_RayData[0].rayOrigin.position - eventData.rayOrigin.position).magnitude;

		m_RayData.Add(new RayData
		{
			rayOrigin = eventData.rayOrigin,
			rayOriginStart = eventData.rayOrigin.position,
			refTransformStartPosition = m_MiniWorld.referenceTransform.position,
			refTransformStartScale = m_MiniWorld.referenceTransform.localScale
		});
	}

	private void OnControlDragging(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		var rayData = m_RayData[0];
		if (!eventData.rayOrigin.Equals(rayData.rayOrigin)) // Do not execute for the second ray
			return;
		var referenceTransform = m_MiniWorld.referenceTransform;
		var rayOrigin = eventData.rayOrigin;
		
		// Rotate translation by inverse workspace yaw
		Quaternion yawRotation = Quaternion.AngleAxis(transform.rotation.eulerAngles.y, Vector3.down);

		// Translate
		referenceTransform.position = rayData.refTransformStartPosition
			+ yawRotation * Vector3.Scale(rayData.rayOriginStart - rayOrigin.transform.position, referenceTransform.localScale);

		// If we have two rays, scale
		if (m_RayData.Count > 1)
		{
			var otherRay = m_RayData[1];
			referenceTransform.localScale = otherRay.refTransformStartScale * (m_ScaleStartDistance
				/ (otherRay.rayOrigin.position - rayOrigin.position).magnitude);

			m_ZoomSliderUI.zoomSlider.value = referenceTransform.localScale.x;
		}
	}

	private void OnControlDragEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_Dragging = false;
		m_WorkspaceUI.topHighlight.visible = false;

		m_RayData.RemoveAll(rayData => rayData.rayOrigin.Equals(eventData.rayOrigin));
	}

	private void OnControlHoverStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		m_WorkspaceUI.topHighlight.visible = true;
	}

	private void OnControlHoverEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
	{
		if (isMiniWorldRay(eventData.rayOrigin))
			return;

		if (!m_Dragging)
			m_WorkspaceUI.topHighlight.visible = false;
	}

	protected override void OnDestroy()
	{
		U.Object.Destroy(m_GridMaterial);
		base.OnDestroy();
	}
}