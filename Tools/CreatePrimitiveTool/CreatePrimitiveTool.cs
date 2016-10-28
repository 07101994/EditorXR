using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR;
using UnityEngine.VR.Tools;
using UnityEngine.InputNew;
using UnityEngine.VR.Actions;
using UnityEngine.VR.Utilities;
using UnityObject = UnityEngine.Object;

[MainMenuItem("Primitives", "Create", "Create standard primitives")]
public class CreatePrimitiveTool : MonoBehaviour, ITool, IStandardActionMap, IInstantiateMenuUI, ICustomRay, IToolActions, ISpatialHash
{
	class PrimitiveToolAction : IAction
	{
		public Sprite icon { get; internal set; }
		public bool ExecuteAction()
		{
			return true;
		}
	}

	private PrimitiveType m_SelectedPrimitiveType = PrimitiveType.Cube;
	private bool m_Freeform = false;

	[SerializeField]
	private Canvas m_CanvasPrefab;
	private bool m_CanvasSpawned;

	private CreatePrimitiveMenu m_MenuUI;

	private GameObject m_CurrentGameObject = null;

	private const float kDrawDistance = 0.075f;

	private Vector3 m_PointA = Vector3.zero;
	private Vector3 m_PointB = Vector3.zero;

	private PrimitiveCreationStates m_State = PrimitiveCreationStates.PointA;

	public Node selfNode { get; set; }

	public Standard standardInput {	get; set; }

	public Func<Node,MenuOrigin,GameObject,GameObject> instantiateMenuUI { private get; set; }

	public Transform rayOrigin { get; set; }
	public Action hideDefaultRay { private get; set; }
	public Action showDefaultRay { private get; set; }

	public List<IAction> toolActions { get; private set; }
	public event Action<Node?> startRadialMenu = delegate { };

	public Action<UnityObject> addObjectToSpatialHash { get; set; }
	public Action<UnityObject> removeObjectFromSpatialHash { get; set; }

	private enum PrimitiveCreationStates
	{
		PointA,
		PointB,
		Freeform,
	}

	void Awake()
	{
		toolActions = new List<IAction>() {};
	}

	void OnDestroy()
	{
		if (m_MenuUI)
			U.Object.Destroy(m_MenuUI.gameObject);
	}

	void Update()
	{
		if (!m_CanvasSpawned)
		{
			SpawnCanvas();
		}

		if (!m_MenuUI.isActiveAndEnabled)
			return;

		switch (m_State)
		{
			case PrimitiveCreationStates.PointA:
			{
				HandlePointA();
				break;
			}
			case PrimitiveCreationStates.PointB:
			{
				UpdatePositions();
				SetScalingForObjectType();
				CheckForTriggerRelease();
				break;
			}
			case PrimitiveCreationStates.Freeform:
			{
				UpdatePositions();
				UpdateFreeformScale();
				CheckForTriggerRelease();
				break;
			}
		}
	}

	void SpawnCanvas()
	{
		//hideDefaultRay();
		var go = instantiateMenuUI(selfNode,MenuOrigin.Main,m_CanvasPrefab.gameObject);
		m_MenuUI = go.GetComponent<CreatePrimitiveMenu>();
		m_MenuUI.selectPrimitive = SetSelectedPrimitive;
		m_CanvasSpawned = true;
	}

	void SetSelectedPrimitive(PrimitiveType type,bool isFreeform)
	{
		m_SelectedPrimitiveType = type;
		m_Freeform = isFreeform;
	}

	void HandlePointA()
	{
		if(standardInput.action.wasJustPressed)
		{
			m_CurrentGameObject = GameObject.CreatePrimitive(m_SelectedPrimitiveType);
			m_CurrentGameObject.transform.localScale = new Vector3(0.0025f,0.0025f,0.0025f);
			m_PointA = rayOrigin.position + rayOrigin.forward * kDrawDistance;
			m_CurrentGameObject.transform.position = m_PointA;

			if(m_Freeform)
				m_State = PrimitiveCreationStates.Freeform;
			else
				m_State = PrimitiveCreationStates.PointB;

			addObjectToSpatialHash(m_CurrentGameObject);
		}
	}

	void SetScalingForObjectType()
	{
		var corner = (m_PointA - m_PointB).magnitude;

		// it feels better to scale the capsule and cylinder type primitives vertically with the drawpoint
		if(m_SelectedPrimitiveType == PrimitiveType.Capsule || m_SelectedPrimitiveType == PrimitiveType.Cylinder 
			|| m_SelectedPrimitiveType == PrimitiveType.Cube)
			m_CurrentGameObject.transform.localScale = Vector3.one * corner * 0.5f;
		else
			m_CurrentGameObject.transform.localScale = Vector3.one * corner;
	}

	void UpdatePositions()
	{
		m_PointB = rayOrigin.position + rayOrigin.forward * kDrawDistance;
		m_CurrentGameObject.transform.position = (m_PointA + m_PointB) * 0.5f;
	}

	void UpdateFreeformScale()
	{
		Vector3 maxCorner = Vector3.Max(m_PointA,m_PointB);
		Vector3 minCorner = Vector3.Min(m_PointA,m_PointB);
		m_CurrentGameObject.transform.localScale = (maxCorner - minCorner);
	}

	void CheckForTriggerRelease()
	{
		if(standardInput.action.wasJustReleased)
			m_State = PrimitiveCreationStates.PointA;
	}
}
