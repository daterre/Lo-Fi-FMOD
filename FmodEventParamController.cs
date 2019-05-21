using UnityEngine;
using System.IO;
using System;
using FMOD;
using FMOD.Studio;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;

namespace LoFiPeople.FMOD
{
	public class FmodEventParamController: MonoBehaviour
	{
		public string ParamName;

		public void SetValue(float value)
		{
			GetComponentInChildren<FmodEventController>().Event[ParamName] = value;
		}
	}
}