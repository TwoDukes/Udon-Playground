#if VRC_SDK_VRCSDK3

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System;

[CustomEditor(typeof(VRC.SDK3.Components.VRCStation))]
public class VRCPlayerStationEditor3 : Editor 
{
    VRC.SDK3.Components.VRCStation myTarget;

	void OnEnable()
	{
		if(myTarget == null)
			myTarget = (VRC.SDK3.Components.VRCStation)target;
	}

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
	}
}
#endif