﻿#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Handles;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
	abstract class Workspace : MonoBehaviour, IWorkspace, IInstantiateUI, IUsesStencilRef, IConnectInterfaces, IUsesViewerScale
	{
		public static readonly Vector3 DefaultBounds = new Vector3(0.7f, 0.4f, 0.4f);
		public static readonly Vector3 MinBounds = new Vector3(0.55f, 0.4f, 0.1f);

		public const float HandleMargin = -0.15f; // Compensate for base size from frame model

		public event Action<IWorkspace> destroyed;

		protected WorkspaceUI m_WorkspaceUI;

		protected Vector3? m_CustomStartingBounds;

		public Vector3 minBounds { get { return m_MinBounds; } set { m_MinBounds = value; } }
		[SerializeField]
		Vector3 m_MinBounds = MinBounds;

		public Bounds contentBounds
		{
			get { return m_ContentBounds; }
			set
			{
				if (!value.Equals(contentBounds))
				{
					var size = value.size;
					size.x = Mathf.Max(size.x, minBounds.x);
					size.y = Mathf.Max(size.y, minBounds.y);
					size.z = Mathf.Max(size.z, minBounds.z);

					m_ContentBounds.size = size; //Only set size, ignore center.
					UpdateBounds();
					OnBoundsChanged();
				}
			}
		}
		Bounds m_ContentBounds;

		[SerializeField]
		GameObject m_BasePrefab;

		Vector3 m_DragStart;
		Vector3 m_PositionStart;
		Vector3 m_BoundSizeStart;
		bool m_Dragging;
		bool m_Moving;
		Coroutine m_VisibilityCoroutine;
		Coroutine m_ResetSizeCoroutine;

		public Bounds outerBounds
		{
			get
			{
				const float kOuterBoundsCenterOffset = 0.225f; //Amount to lower the center of the outerBounds for better interaction with menus
				return new Bounds(contentBounds.center + Vector3.down * kOuterBoundsCenterOffset,
					new Vector3(
						contentBounds.size.x,
						contentBounds.size.y,
						contentBounds.size.z
						));
			}
		}

		public Bounds vacuumBounds { get { return outerBounds; } }

		public byte stencilRef { get; set; }

		/// <summary>
		/// If true, allow the front face of the workspace to dynamically adjust its angle when rotated
		/// </summary>
		public bool dynamicFaceAdjustment { set { m_WorkspaceUI.dynamicFaceAdjustment = value; } }

		/// <summary>
		/// If true, prevent the resizing of a workspace via the front and back resize handles
		/// </summary>
		public bool preventFrontBackResize { set { m_WorkspaceUI.preventFrontBackResize = value; } }

		/// <summary>
		/// If true, prevent the resizing of a workspace via the left and right resize handles
		/// </summary>
		public bool preventLeftRightResize { set { m_WorkspaceUI.preventLeftRightResize = value; } }

		/// <summary>
		/// (-1 to 1) ranged value that controls the separator mask's X-offset placement
		/// A value of zero will leave the mask in the center of the workspace
		/// </summary>
		public float topPanelDividerOffset
		{
			set
			{
				m_WorkspaceUI.topPanelDividerOffset = value;
				m_WorkspaceUI.bounds = contentBounds;
			}
		}

		public Transform topPanel { get { return m_WorkspaceUI.topPanel; } }

		public Transform frontPanel { get { return m_WorkspaceUI.frontPanel; } }

		public virtual void Setup()
		{
			var baseObject = this.InstantiateUI(m_BasePrefab);
			baseObject.transform.SetParent(transform, false);

			m_WorkspaceUI = baseObject.GetComponent<WorkspaceUI>();
			this.ConnectInterfaces(m_WorkspaceUI);
			m_WorkspaceUI.closeClicked += OnCloseClicked;
			m_WorkspaceUI.resetSizeClicked += OnResetClicked;

			m_WorkspaceUI.sceneContainer.transform.localPosition = Vector3.zero;

			//Do not set bounds directly, in case OnBoundsChanged requires Setup override to complete
			m_ContentBounds = new Bounds(Vector3.up * DefaultBounds.y * 0.5f, m_CustomStartingBounds ?? DefaultBounds); // If custom bounds have been set, use them as the initial bounds
			UpdateBounds();

			//Set up DirectManipulator
			var directManipulator = m_WorkspaceUI.directManipulator;
			directManipulator.target = transform;
			directManipulator.translate = Translate;
			directManipulator.rotate = Rotate;

			//Set up the front "move" handle highlight, the move handle is used to translate/rotate the workspace
			var moveHandle = m_WorkspaceUI.moveHandle;
			moveHandle.dragStarted += OnMoveHandleDragStarted;
			moveHandle.dragEnded += OnMoveHandleDragEnded;
			moveHandle.hoverStarted += OnMoveHandleHoverStarted;
			moveHandle.hoverEnded += OnMoveHandleHoverEnded;

			var handles = new []
			{
				m_WorkspaceUI.leftHandle,
				m_WorkspaceUI.frontHandle,
				m_WorkspaceUI.backHandle,
				m_WorkspaceUI.rightHandle
			};

			foreach (var handle in handles)
			{
				handle.dragStarted += OnHandleDragStarted;
				handle.dragging += OnHandleDragging;
				handle.dragEnded += OnHandleDragEnded;
				handle.hoverStarted += OnHandleHoverStarted;
				handle.hoverEnded += OnHandleHoverEnded;
			}

			this.StopCoroutine(ref m_VisibilityCoroutine);

			m_VisibilityCoroutine = StartCoroutine(AnimateShow());
		}

		public void Close()
		{
			this.StopCoroutine(ref m_VisibilityCoroutine);
			m_VisibilityCoroutine = StartCoroutine(AnimateHide());
		}

		public virtual void OnHandleDragStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			m_WorkspaceUI.highlightsVisible = true;
			m_PositionStart = transform.position;
			m_DragStart = eventData.rayOrigin.position;
			m_BoundSizeStart = contentBounds.size;
			m_Dragging = true;
		}

		public virtual void OnHandleDragging(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (m_Dragging)
			{
				var viewerScale = this.GetViewerScale();
				var dragVector = (eventData.rayOrigin.position - m_DragStart) / viewerScale;
				var bounds = contentBounds;
				var positionOffset = Vector3.zero;

				if (handle.Equals(m_WorkspaceUI.leftHandle))
				{
					bounds.size = m_BoundSizeStart + Vector3.left * Vector3.Dot(dragVector, transform.right);
					positionOffset = transform.right * Vector3.Dot(dragVector, transform.right) * 0.5f;
				}

				if (handle.Equals(m_WorkspaceUI.frontHandle))
				{
					bounds.size = m_BoundSizeStart + Vector3.back * Vector3.Dot(dragVector, transform.forward);
					positionOffset = transform.forward * Vector3.Dot(dragVector, transform.forward) * 0.5f;
				}

				if (handle.Equals(m_WorkspaceUI.rightHandle))
				{
					bounds.size = m_BoundSizeStart + Vector3.right * Vector3.Dot(dragVector, transform.right);
					positionOffset = transform.right * Vector3.Dot(dragVector, transform.right) * 0.5f;
				}

				if (handle.Equals(m_WorkspaceUI.backHandle))
				{
					bounds.size = m_BoundSizeStart + Vector3.forward * Vector3.Dot(dragVector, transform.forward);
					positionOffset = transform.forward * Vector3.Dot(dragVector, transform.forward) * 0.5f;
				}

				contentBounds = bounds;

				if (contentBounds.size == bounds.size) //Don't reposition if we hit minimum bounds
					transform.position = m_PositionStart + positionOffset * viewerScale;
			}
		}

		public virtual void OnHandleDragEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			m_WorkspaceUI.highlightsVisible = false;
			m_Dragging = false;
		}

		public virtual void OnHandleHoverStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
		}

		public virtual void OnHandleHoverEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
		}

		void Translate(Vector3 deltaPosition, Transform rayOrigin, bool constrained)
		{
			transform.position += deltaPosition;
		}

		void Rotate(Quaternion deltaRotation)
		{
			transform.rotation *= deltaRotation;
		}

		public virtual void OnCloseClicked()
		{
			Close();
		}

		public virtual void OnResetClicked()
		{
			this.StopCoroutine(ref m_ResetSizeCoroutine);

			m_ResetSizeCoroutine = StartCoroutine(AnimateResetSize());
		}

		public void SetUIHighlightsVisible(bool value)
		{
			m_WorkspaceUI.highlightsVisible = value;
		}

		void UpdateBounds()
		{
			m_WorkspaceUI.bounds = contentBounds;
		}

		protected virtual void OnDestroy()
		{
			destroyed(this);
		}

		protected virtual void OnBoundsChanged()
		{
		}

		IEnumerator AnimateShow()
		{
			m_WorkspaceUI.highlightsVisible = true;

			var targetScale = Vector3.one;
			var scale = Vector3.zero;
			var smoothVelocity = Vector3.zero;
			var currentDuration = 0f;
			const float kTargetDuration = 0.75f;
			while (currentDuration < kTargetDuration)
			{
				currentDuration += Time.unscaledDeltaTime;
				transform.localScale = scale;
				scale = MathUtilsExt.SmoothDamp(scale, targetScale, ref smoothVelocity, kTargetDuration, Mathf.Infinity, Time.unscaledDeltaTime);
				yield return null;
			}

			transform.localScale = targetScale;

			m_WorkspaceUI.highlightsVisible = false;
			m_VisibilityCoroutine = null;
		}

		IEnumerator AnimateHide()
		{
			var targetScale = Vector3.zero;
			var scale = transform.localScale;
			var smoothVelocity = Vector3.zero;
			var currentDuration = 0f;
			const float kTargetDuration = 0.185f;
			while (currentDuration < kTargetDuration)
			{
				currentDuration += Time.unscaledDeltaTime;
				transform.localScale = scale;
				scale = MathUtilsExt.SmoothDamp(scale, targetScale, ref smoothVelocity, kTargetDuration, Mathf.Infinity, Time.unscaledDeltaTime);
				yield return null;
			}
			transform.localScale = targetScale;

			m_WorkspaceUI.highlightsVisible = false;
			m_VisibilityCoroutine = null;
			ObjectUtils.Destroy(gameObject);
		}

		void OnMoveHandleDragStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (m_Dragging)
				return;

			m_Moving = true;
			m_WorkspaceUI.highlightsVisible = true;
		}

		void OnMoveHandleDragEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (m_Dragging)
				return;

			m_Moving = false;
			m_WorkspaceUI.highlightsVisible = false;
		}

		void OnMoveHandleHoverStarted(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (m_Dragging || m_Moving)
				return;

			m_WorkspaceUI.frontHighlightVisible = true;
		}

		void OnMoveHandleHoverEnded(BaseHandle handle, HandleEventData eventData = default(HandleEventData))
		{
			if (m_Dragging || m_Moving)
				return;

			m_WorkspaceUI.frontHighlightVisible = false;
		}

		IEnumerator AnimateResetSize()
		{
			var currentBoundsSize = contentBounds.size;
			var currentBoundsCenter = contentBounds.center;
			var targetBoundsSize = m_CustomStartingBounds ?? minBounds;
			var targetBoundsCenter = Vector3.zero;
			var smoothVelocitySize = Vector3.zero;
			var smoothVelocityCenter = Vector3.zero;
			var currentDuration = 0f;
			const float kTargetDuration = 0.75f;
			while (currentDuration < kTargetDuration)
			{
				currentDuration += Time.unscaledDeltaTime;
				currentBoundsCenter = MathUtilsExt.SmoothDamp(currentBoundsCenter, targetBoundsCenter, ref smoothVelocityCenter, kTargetDuration, Mathf.Infinity, Time.unscaledDeltaTime);
				currentBoundsSize = MathUtilsExt.SmoothDamp(currentBoundsSize, targetBoundsSize, ref smoothVelocitySize, kTargetDuration, Mathf.Infinity, Time.unscaledDeltaTime);
				contentBounds = new Bounds(currentBoundsCenter, currentBoundsSize);
				OnBoundsChanged();
				yield return null;
			}
		}
	}
}
#endif
