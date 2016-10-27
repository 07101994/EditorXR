﻿using UnityEngine.UI;

namespace UnityEngine.VR.Helpers
{
	public class GridCellSizeAdjuster : MonoBehaviour
	{
		RectTransform m_LayoutGroupTransform;

		[SerializeField]
		GridLayoutGroup m_LayoutGroup;

		[SerializeField]
		float m_XScalePadding = 0.01f;

		void Awake()
		{
			m_LayoutGroupTransform = m_LayoutGroup.transform as RectTransform;
			DriveCellSizeWithLayoutGroupTransform();
		}

		void Update()
		{
			if (m_LayoutGroupTransform.hasChanged)
				DriveCellSizeWithLayoutGroupTransform();
		}

		void DriveCellSizeWithLayoutGroupTransform()
		{
			if (!m_LayoutGroupTransform)
				return;

			m_LayoutGroup.cellSize = new Vector2(Mathf.Abs(m_LayoutGroupTransform.rect.xMin) + Mathf.Abs(m_LayoutGroupTransform.rect.xMax) + m_XScalePadding, m_LayoutGroup.cellSize.y);
		}
	}
}