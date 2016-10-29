﻿using System;
using UnityEngine.VR.Utilities;

namespace UnityEngine.VR.Actions
{
	[ActionMenuItem("Paste", ActionMenuItemAttribute.kDefaultActionSectionName, 6)]
	public class Paste : MonoBehaviour, IAction, ISpatialHash
	{
		const int directionCount = 6;
		static int directionCounter;

		[SerializeField]
		float positionOffset = 1.5f;

		public Sprite icon { get { return m_Icon; } }
		[SerializeField]
		private Sprite m_Icon;

		public static Object buffer { get; set; }

		public Action<Object> addObjectToSpatialHash { get; set; }
		public Action<Object> removeObjectFromSpatialHash { get; set; }

		public bool ExecuteAction()
		{
			//return EditorApplication.ExecuteActionMenuItem("Edit/Paste");

			if (buffer != null)
			{
				var pasted = Instantiate(buffer);
				pasted.hideFlags = HideFlags.None;
				var go = pasted as GameObject;
				if (go)
				{
					var transform = go.transform;
					var bounds = U.Object.GetTotalBounds(transform);
					var offset = Vector3.one;
					if (bounds.HasValue)
					{
						var camera = U.Camera.GetMainCamera();
						var viewDirection = camera.transform.position - transform.position;
						viewDirection.y = 0;
						offset = Quaternion.LookRotation(viewDirection) * Quaternion.AngleAxis((float)directionCounter / directionCount * 360, Vector3.forward) * Vector3.left * bounds.Value.size.magnitude * positionOffset;
						directionCounter++;
					}
					go.transform.position += offset;
					go.SetActive(true);
				}

				addObjectToSpatialHash(pasted);

				return true;
			}

			return false;
		}
	}
}