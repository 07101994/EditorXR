﻿using ListView;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR.Handles;
using UnityEngine.VR.Utilities;

public class FolderListItem : ListViewItem<FolderData>
{
	private const float kMargin = 0.01f;
	private const float kIndent = 0.02f;

	private const float kExpandArrowRotateSpeed = 0.4f;

	[SerializeField]
	private Text m_Text;

	[SerializeField]
	private BaseHandle m_Cube;

	[SerializeField]
	private BaseHandle m_ExpandArrow;

	[SerializeField]
	private Material m_NoClipCubeMaterial;

	[SerializeField]
	private Material m_NoClipExpandArrowMaterial;

	[SerializeField]
	private Color m_HoverColor;

	[SerializeField]
	private Color m_SelectedColor;

	private Color m_NormalColor;

	private bool m_Hovering;

	private Renderer m_CubeRenderer;

	Transform m_CubeTransform;

	public Material cubeMaterial { get { return m_CubeRenderer.sharedMaterial; } }

	public Action<FolderData> selectFolder;

	public override void Setup(FolderData listData)
	{
		base.Setup(listData);
		// First time setup
		if (m_CubeRenderer == null)
		{
			// Cube material might change for hover state, so we always instance it
			m_CubeRenderer = m_Cube.GetComponent<Renderer>();
			m_NormalColor = m_CubeRenderer.sharedMaterial.color;
			U.Material.GetMaterialClone(m_CubeRenderer);

			m_ExpandArrow.dragEnded += ToggleExpanded;
			m_Cube.dragStarted += SelectFolder;
			m_Cube.dragEnded += ToggleExpanded;

			m_Cube.hoverStarted += OnHoverStarted;
			m_Cube.hoverEnded += OnHoverEnded;
		}

		m_CubeTransform = m_Cube.transform;
		m_Text.text = listData.name;

		// HACK: We need to kick the canvasRenderer to update the mesh properly
		m_Text.gameObject.SetActive(false);
		m_Text.gameObject.SetActive(true);

		m_ExpandArrow.gameObject.SetActive(listData.children != null);
		m_Hovering = false;
	}

	public void SetMaterials(Material textMaterial, Material expandArrowMaterial)
	{
		m_Text.material = textMaterial;
		m_ExpandArrow.GetComponent<Renderer>().sharedMaterial = expandArrowMaterial;
	}

	public void UpdateSelf(float width, int depth)
	{
		var cubeScale = m_CubeTransform.localScale;
		cubeScale.x = width;
		m_CubeTransform.localScale = cubeScale;

		var arrowWidth = m_ExpandArrow.transform.localScale.x * 0.5f;
		var halfWidth = width * 0.5f;
		var indent = kIndent * depth;
		var doubleMargin = kMargin * 2;
		m_ExpandArrow.transform.localPosition = new Vector3(kMargin + indent - halfWidth, m_ExpandArrow.transform.localPosition.y, 0);

		// Text is next to arrow, with a margin and indent, rotated toward camera
		m_Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, (width - doubleMargin - indent) * 1 / m_Text.transform.localScale.x);
		m_Text.transform.localPosition = new Vector3(doubleMargin + indent + arrowWidth - halfWidth, m_Text.transform.localPosition.y, 0);

		m_Text.transform.localRotation = U.Camera.LocalRotateTowardCamera(transform.parent.rotation);

		// Rotate arrow for expand state
		m_ExpandArrow.transform.localRotation = Quaternion.Lerp(m_ExpandArrow.transform.localRotation,
												Quaternion.AngleAxis(90f, Vector3.right) * (data.expanded ? Quaternion.AngleAxis(90f, Vector3.back) : Quaternion.identity),
												kExpandArrowRotateSpeed);

		// Set selected/hover/normal color
		if (data.selected)
			m_CubeRenderer.sharedMaterial.color = m_SelectedColor;
		else if (m_Hovering)
			m_CubeRenderer.sharedMaterial.color = m_HoverColor;
		else
			m_CubeRenderer.sharedMaterial.color = m_NormalColor;
	}

	private void ToggleExpanded(BaseHandle handle, HandleEventData eventData)
	{
		data.expanded = !data.expanded;
	}

	private void SelectFolder(BaseHandle baseHandle, HandleEventData eventData)
	{
		var folderItem = baseHandle.GetComponentInParent<FolderListItem>();
		selectFolder(folderItem.data);
	}

	private void OnHoverStarted(BaseHandle baseHandle, HandleEventData eventData)
	{
		m_Hovering = true;
	}

	private void OnHoverEnded(BaseHandle baseHandle, HandleEventData eventData)
	{
		m_Hovering = false;
	}

	private void OnDestroy()
	{
		if (m_CubeRenderer)
			U.Object.Destroy(m_CubeRenderer.sharedMaterial);
	}
}