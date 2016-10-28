﻿using UnityEditor;
using UnityEngine;
using UnityEditor.VR;
using UnityEngine.VR.Utilities;

[RequireComponent(typeof(Camera))]
public class VRSmoothCamera : MonoBehaviour
{
	public Camera smoothCamera { get { return m_SmoothCamera; } }
	Camera m_SmoothCamera;

	[SerializeField]
	int m_TargetDisplay;
	[SerializeField, Range(1, 180)]
	int m_FieldOfView = 40;
	[SerializeField]
	float m_PositionSmoothingMultiplier = 3;

	Camera m_VRCamera;
	RenderTexture m_RenderTexture;

	Vector3 position;
	Vector3 forward;

	void Awake()
	{
		m_VRCamera = GetComponent<Camera>();

		m_SmoothCamera = U.Object.CreateGameObjectWithComponent<Camera>();
		m_SmoothCamera.transform.position = m_VRCamera.transform.position;
		m_SmoothCamera.transform.rotation = m_VRCamera.transform.rotation;
		m_SmoothCamera.enabled = false;

		position = m_SmoothCamera.transform.position;
		forward = m_SmoothCamera.transform.forward;
	}

	void OnDestroy()
	{
		U.Object.Destroy(m_SmoothCamera.gameObject);
	}

	void LateUpdate()
	{
		m_SmoothCamera.CopyFrom(m_VRCamera); // This copies the transform as well
		var vrCameraTexture = m_VRCamera.targetTexture;
		if (vrCameraTexture && (!m_RenderTexture || m_RenderTexture.width != vrCameraTexture.width || m_RenderTexture.height != vrCameraTexture.height))
		{
			Rect guiRect = new Rect(0, 0, vrCameraTexture.width, vrCameraTexture.height);
			Rect cameraRect = EditorGUIUtility.PointsToPixels(guiRect);
			VRView.activeView.CreateCameraTargetTexture(ref m_RenderTexture, cameraRect, false);
			m_RenderTexture.name = "Smooth Camera RT";
		}
		m_SmoothCamera.targetTexture = m_RenderTexture;
		m_SmoothCamera.targetDisplay = m_TargetDisplay;
		m_SmoothCamera.cameraType = CameraType.Game;
		m_SmoothCamera.rect = new Rect(0, 0, 1f, 1f);
		m_SmoothCamera.stereoTargetEye = StereoTargetEyeMask.None;
		m_SmoothCamera.fieldOfView = m_FieldOfView;

		position = Vector3.Lerp(position, m_VRCamera.transform.position, Time.unscaledDeltaTime * m_PositionSmoothingMultiplier);
		forward = Vector3.Lerp(forward, m_VRCamera.transform.forward, Time.unscaledDeltaTime * m_PositionSmoothingMultiplier);

		m_SmoothCamera.transform.forward = forward;
		m_SmoothCamera.transform.position = position - m_SmoothCamera.transform.forward * 0.9f;

		// Don't render any HMD-related visual proxies
		var hidden = m_VRCamera.GetComponentsInChildren<Renderer>();
		foreach (var h in hidden)
			h.enabled = false;

		RenderTexture.active = m_SmoothCamera.targetTexture;
		m_SmoothCamera.Render();
		RenderTexture.active = null;

		foreach (var h in hidden)
			h.enabled = true;
	}
}
