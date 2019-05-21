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
		[FMODUnity.EventRef] public string[] EventPaths;
		public bool StartAutomatically = false;
		public FmodEvent[] Events { get; private set; }
		public FmodEvent Event => Events[0];

		private void Awake()
		{
			Events = new FmodEvent[EventPaths.Length];
			for (int i = 0; i < Events.Length; i++)
				Events[i] = new FmodEvent(EventPaths[i]).LoadMarkers();
		}

		void Start()
		{
			if (StartAutomatically)
				StartEvents();
		}

		public void StartEvents()
		{
			for (int i = 0; i < Events.Length; i++)
				Events[i].Start();
		}

		public void StopEvents(bool release)
		{
			for (int i = 0; i < Events.Length; i++)
				if (Events[i] != null && Events[i].IsValid)
				{
					Events[i].Stop();
					if (release)
						Events[i].Release();
				}
		}

		public void ReleaseEvents()
		{
			for (int i = 0; i < Events.Length; i++)
				if (Events[i] != null && Events[i].IsValid)
				{
					Events[i].Release();
				}
		}

		void Update()
		{
			for (int i = 0; i < Events.Length; i++)
				if (Events[i] != null && Events[i].IsValid)
					Events[i].Position = transform.position;
		}

		void OnDestroy()
		{
			StopEvents(true);
		}

		public void JumpTo(string marker)
		{
			int appliedTo = 0;
			for (int i = 0; i < Events.Length; i++)
			{
				if (Events[i] == null || !Events[i].IsValid || !Events[i].HasMarker(marker))
					continue;

				appliedTo++;
				Events[i].JumpToMarker(marker);
			}

			if (appliedTo < 1)
				UnityEngine.Debug.LogError("Marker was not applied to any events");
		}

		public float this[string paramName]
		{
			set
			{
				for (int i = 0; i < Events.Length; i++)
					if (Events[i] != null && Events[i].IsValid)
						Events[i][paramName] = value;
			}
		}
	}
}