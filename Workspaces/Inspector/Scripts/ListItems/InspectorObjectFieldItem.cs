﻿using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR.Handles;
using UnityEngine.VR.Modules;
using UnityEngine.VR.UI;
using UnityEngine.VR.Utilities;
using Object = UnityEngine.Object;

public class InspectorObjectFieldItem : InspectorPropertyItem
{
	[SerializeField]
	Text m_FieldLabel;

	Type m_ObjectType;
	string m_ObjectTypeName;

	public override void Setup(InspectorData data)
	{
		base.Setup(data);

		m_ObjectTypeName = U.Object.NicifySerializedPropertyType(m_SerializedProperty.type);
		m_ObjectType = U.Object.TypeNameToType(m_ObjectTypeName);

		SetObject(m_SerializedProperty.objectReferenceValue);
	}

	bool SetObject(Object obj)
	{
		var objectReference = m_SerializedProperty.objectReferenceValue;

		if (obj == null)
			m_FieldLabel.text = string.Format("None ({0})", m_ObjectTypeName);
		else
		{
			var objType = obj.GetType();
			if (!objType.IsAssignableFrom(m_ObjectType))
			{
				if (obj.Equals(objectReference)) // Show type mismatch for old serialized data
					m_FieldLabel.text = "Type Mismatch";
				return false;
			}
			m_FieldLabel.text = string.Format("{0} ({1})", obj.name, obj.GetType().Name);
		}

		if (obj == null && m_SerializedProperty.objectReferenceValue == null)
			return true;
		if (m_SerializedProperty.objectReferenceValue != null && m_SerializedProperty.objectReferenceValue.Equals(obj))
			return true;

		m_SerializedProperty.objectReferenceValue = obj;

		data.serializedObject.ApplyModifiedProperties();

		return true;
	}

	public void ClearButton()
	{
		SetObject(null);
	}

	protected override object GetDropObjectForFieldBlock(Transform fieldBlock)
	{
		return m_SerializedProperty.objectReferenceValue;
	}

	protected override bool CanDropForFieldBlock(Transform fieldBlock, IDroppable droppable)
	{
		if (droppable == null)
			return false;

		var dropObject = droppable.GetDropObject();
		return dropObject is Object;
	}

	protected override void ReceiveDropForFieldBlock(Transform fieldBlock, IDroppable droppable)
	{
		if (droppable == null)
			return;

		var dropObject = droppable.GetDropObject();
		SetObject(dropObject as Object);
	}
}