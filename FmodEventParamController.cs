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
		FmodEventController _ev;

		public void SetValue(float value)
		{
			if (_ev == null)
				_ev = GetComponentInChildren<FmodEventController>();
			if (_ev == null)
			{
				UnityEngine.Debug.LogError("[FMOD] No FmodEventController found on this object or any of its children.");
				return;
			}

			_ev.Event[ParamName] = value;
		}
	}
}