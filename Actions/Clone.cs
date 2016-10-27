﻿using System;
using UnityEditor;
using UnityEngine.VR.Utilities;

namespace UnityEngine.VR.Actions
{
	[ActionMenuItem("Clone", ActionMenuItemAttribute.kDefaultActionSectionName, 3)]
	public class Clone : MonoBehaviour, IAction, ISpatialHash
	{
		public Sprite icon { get { return m_Icon; } }
		[SerializeField]
		private Sprite m_Icon;

		public Action<Object> addObjectToSpatialHash { get; set; }
		public Action<Object> removeObjectFromSpatialHash { get; set; }

		public bool ExecuteAction()
		{
			const float range = 4f;
			var selection = Selection.GetTransforms(SelectionMode.Editable);
			foreach (var s in selection)
			{
				var clone = U.Object.Instantiate(s.gameObject);
				Vector3 cloneOffset = new Vector3(s.position.x + Random.Range(-range, range), s.position.y + Random.Range(-range, range), s.position.z + Random.Range(-range, range)) + (Vector3.one * 0.5f);
				clone.transform.position = s.position + cloneOffset;
				addObjectToSpatialHash(clone);
			}

			return true;
		}
	}
}