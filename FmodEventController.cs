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
	public class FmodEventController: MonoBehaviour
	{
		[FMODUnity.EventRef] public string EventPath;
		public bool StartAutomatically = false;
		public FmodEvent Event { get; private set; }

		private void Awake()
		{
			Event = new FmodEvent(EventPath)
				.LoadMarkers();
		}

		private void Start()
		{
			if (StartAutomatically)
				Event.Start();
		}

		private void Update()
		{
			if (Event != null && Event.IsValid)
				Event.Position = transform.position;
		}

		private void OnDestroy()
		{
			if (Event != null && Event.IsValid)
				Event.Stop().Release();
		}

		public void JumpTo(string marker)
		{
			if (Event == null || !Event.IsValid)
			{
				UnityEngine.Debug.LogError("[FMOD] Event is not playing, can't jump to marker");
				return;
			}

			Event.JumpToMarker(marker);
		}
	}
}