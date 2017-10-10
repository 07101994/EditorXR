#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputNew;
using XRAuthoring;

namespace UnityEditor.Experimental.EditorVR.Menus
{
	sealed class RadialMenu : MonoBehaviour, IInstantiateUI, IAlternateMenu, IUsesMenuOrigins, ICustomActionMap,
		IControlHaptics, IUsesNode, IConnectInterfaces
	{
		const float k_ActivationThreshold = 0.5f; // Do not consume thumbstick or activate menu if the control vector's magnitude is below this threshold

		public ActionMap actionMap { get {return m_RadialMenuActionMap; } }
		[SerializeField]
		ActionMap m_RadialMenuActionMap;

		[SerializeField]
		RadialMenuUI m_RadialMenuPrefab;

		[SerializeField]
		HapticPulse m_ReleasePulse;

		[SerializeField]
		HapticPulse m_ButtonHoverPulse;

		[SerializeField]
		HapticPulse m_ButtonClickedPulse;

		RadialMenuUI m_RadialMenuUI;
		List<ActionMenuData> m_MenuActions;
		Transform m_AlternateMenuOrigin;
		MenuHideFlags m_MenuHideFlags = MenuHideFlags.Hidden;

		public event Action<Transform> itemWasSelected;

		public Transform rayOrigin { private get; set; }

		public Transform menuOrigin { get; set; }

		public GameObject menuContent { get { return m_RadialMenuUI.gameObject; } }

		public Node? node { get; set; }

		public Bounds localBounds { get { return default(Bounds); } }

		public List<ActionMenuData> menuActions
		{
			get { return m_MenuActions; }
			set
			{
				m_MenuActions = value;

				if (m_RadialMenuUI)
					m_RadialMenuUI.actions = value;
			}
		}

		public Transform alternateMenuOrigin
		{
			get { return m_AlternateMenuOrigin; }
			set
			{
				m_AlternateMenuOrigin = value;

				if (m_RadialMenuUI != null)
					m_RadialMenuUI.alternateMenuOrigin = value;
			}
		}

		public MenuHideFlags menuHideFlags
		{
			get { return m_MenuHideFlags; }
			set
			{
				if (m_MenuHideFlags != value)
				{
					m_MenuHideFlags = value;
					if (m_RadialMenuUI)
						m_RadialMenuUI.visible = value == 0;
				}
			}
		}

		void Start()
		{
			m_RadialMenuUI = this.InstantiateUI(m_RadialMenuPrefab.gameObject).GetComponent<RadialMenuUI>();
			m_RadialMenuUI.alternateMenuOrigin = alternateMenuOrigin;
			m_RadialMenuUI.actions = menuActions;
			this.ConnectInterfaces(m_RadialMenuUI); // Connect interfaces before performing setup on the UI
			m_RadialMenuUI.Setup();
			m_RadialMenuUI.buttonHovered += OnButtonHovered;
			m_RadialMenuUI.buttonClicked += OnButtonClicked;
		}

		public void ProcessInput(ActionMapInput input, ConsumeControlDelegate consumeControl)
		{
			var radialMenuInput = (RadialMenuInput)input;
			if (radialMenuInput == null || m_MenuHideFlags != 0)
				return;
			
			var inputDirection = radialMenuInput.navigate.vector2;

			if (inputDirection.magnitude > k_ActivationThreshold)
			{
				// Composite controls need to be consumed separately
				consumeControl(radialMenuInput.navigateX);
				consumeControl(radialMenuInput.navigateY);
				m_RadialMenuUI.buttonInputDirection = inputDirection;
			}
			else
			{
				m_RadialMenuUI.buttonInputDirection = Vector3.zero;
				return;
			}

			var selectControl = radialMenuInput.selectItem;
			m_RadialMenuUI.pressedDown = selectControl.wasJustPressed;
			if (m_RadialMenuUI.pressedDown)
				consumeControl(selectControl);

			if (selectControl.wasJustReleased)
			{
				this.Pulse(node, m_ReleasePulse);

				m_RadialMenuUI.SelectionOccurred();

				if (itemWasSelected != null)
					itemWasSelected(rayOrigin);

				consumeControl(selectControl);
			}
		}

		void OnButtonClicked()
		{
			this.Pulse(node, m_ButtonClickedPulse);
		}

		void OnButtonHovered()
		{
			this.Pulse(node, m_ButtonHoverPulse);
		}
	}
}
#endif
