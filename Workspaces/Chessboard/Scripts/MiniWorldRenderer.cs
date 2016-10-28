﻿using System.Collections.Generic;
﻿using System;
using UnityEngine;

using UnityEngine.VR.Utilities;

public class MiniWorldRenderer : MonoBehaviour
{
	const float kMinScale = 0.001f;

	private Camera m_MiniCamera;

	bool[] m_RendererPreviousEnable = new bool[0];

	public MiniWorld miniWorld { private get; set; }
	public LayerMask cullingMask { private get; set; }

	public List<Renderer> ignoreList
	{
		set
		{
			m_IgnoreList = value;
			if(m_IgnoreList.Count > m_RendererPreviousEnable.Length)
				m_RendererPreviousEnable = new bool[m_IgnoreList.Count];
		}
	}
	List<Renderer> m_IgnoreList = new List<Renderer>();
	public Func<bool> preProcessRender { private get; set; }
	public Action postProcessRender { private get; set; }

	private void OnEnable()
	{
		GameObject go = new GameObject("MiniWorldCamera", typeof(Camera));
		go.hideFlags = HideFlags.DontSave;
		m_MiniCamera = go.GetComponent<Camera>();
		go.SetActive(false);
		Camera.onPostRender += RenderMiniWorld;
	}

	private void OnDisable()
	{
		Camera.onPostRender -= RenderMiniWorld;
		U.Object.Destroy(m_MiniCamera.gameObject);
	}

	private void RenderMiniWorld(Camera camera)
	{
		// Do not render if miniWorld scale is too low to avoid errors in the console
		if (miniWorld && miniWorld.transform.lossyScale.magnitude > kMinScale)
		{
			m_MiniCamera.CopyFrom(camera);

			m_MiniCamera.cullingMask = cullingMask;
			m_MiniCamera.clearFlags = CameraClearFlags.Nothing;
			m_MiniCamera.worldToCameraMatrix = camera.worldToCameraMatrix * miniWorld.miniToReferenceMatrix;
			Shader shader = Shader.Find("Custom/Custom Clip Planes");
			Shader.SetGlobalVector("_GlobalClipCenter", miniWorld.referenceBounds.center);
			Shader.SetGlobalVector("_GlobalClipExtents", miniWorld.referenceBounds.extents);

			for (var i = 0; i < m_IgnoreList.Count; i++)
			{
				var hiddenRenderer = m_IgnoreList[i];
				if (hiddenRenderer) 
				{
					m_RendererPreviousEnable[i] = hiddenRenderer.enabled;
					hiddenRenderer.enabled = false;
				}
			}

			if(preProcessRender())
				m_MiniCamera.RenderWithShader(shader, string.Empty);

			postProcessRender();

			for (var i = 0; i < m_IgnoreList.Count; i++)
			{
				var hiddenRenderer = m_IgnoreList[i];
				if (hiddenRenderer)
					hiddenRenderer.enabled = m_RendererPreviousEnable[i];
			}
		}
	}
}