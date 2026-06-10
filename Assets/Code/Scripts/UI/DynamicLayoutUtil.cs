using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DynamicLayoutUtil : MonoBehaviour
{
	[SerializeField] private GameObject target;

    public void ForceUpdateUI()
    {
		LayoutRebuilder.ForceRebuildLayoutImmediate(target.GetComponent<RectTransform>());
	}
}
