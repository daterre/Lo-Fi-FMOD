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
	public class FmodEvent: IDisposable
	{
		public EventInstance Instance;
		public EventDescription Description;
		public readonly string EventPath;

		event Action<FmodEvent> _onPlayStateChanged;
		event Action<FmodEvent, string> _onMarker;
		event Action<FmodEvent, string> _onSoundPlayed;
		event Action<FmodEvent, string> _onSoundStopped;
		event Func<FmodEvent, string, Sound> _onProgrammerSoundCreated;

		global::FMOD.Studio.EVENT_CALLBACK _callbackSink = null;
		Dictionary<string, int> _markers = null;
		bool _markersLoaded = false;

		#if ENABLE_IL2CPP
		// Necessary because callbacks can only be static on AOT platforms
		static Dictionary<IntPtr, FmodEvent> _callbacks = new Dictionary<IntPtr, FmodEvent>();
		#endif

		public event Action<FmodEvent> OnPlayStateChanged
		{
			add { _onPlayStateChanged += value; EnableCallbacks(); }
			remove { _onPlayStateChanged -= value; DisableCallbacks(); }
		}
		public event Action<FmodEvent, string> OnMarker
		{
			add { _onMarker += value; EnableCallbacks(); }
			remove { _onMarker -= value; DisableCallbacks(); }
		}
		public event Func<FmodEvent, string, Sound> OnProgrammerSoundCreated
		{
			add { _onProgrammerSoundCreated += value; EnableCallbacks(); }
			remove { _onProgrammerSoundCreated -= value; DisableCallbacks(); }
		}

		public event Action<FmodEvent, string> OnSoundPlayed
		{
			add { _onSoundPlayed += value; EnableCallbacks(); }
			remove { _onSoundPlayed -= value; DisableCallbacks(); }
		}

		public event Action<FmodEvent, string> OnSoundStopped
		{
			add { _onSoundStopped += value; EnableCallbacks(); }
			remove { _onSoundStopped -= value; DisableCallbacks(); }
		}

		void EnableCallbacks()
		{
			if (_onPlayStateChanged == null &&
				_onMarker == null &&
				_onSoundPlayed == null &&
				_onSoundStopped == null &&
				_onProgrammerSoundCreated == null
				)
				return;

#if ENABLE_IL2CPP
			lock (_callbacks)
			{
				if (!_callbacks.ContainsKey(this.Instance.handle))
					_callbacks[this.Instance.handle] = this;
			}
#endif

			if (_callbackSink == null)
			{
				_callbackSink = new EVENT_CALLBACK(FmodEventCallback);
				Do(Instance.setCallback(_callbackSink,
					EVENT_CALLBACK_TYPE.STARTED |
					EVENT_CALLBACK_TYPE.STARTING |
					EVENT_CALLBACK_TYPE.RESTARTED |
					EVENT_CALLBACK_TYPE.STOPPED |
					EVENT_CALLBACK_TYPE.START_FAILED |
					EVENT_CALLBACK_TYPE.TIMELINE_MARKER |
					EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND |
					EVENT_CALLBACK_TYPE.SOUND_PLAYED |
					EVENT_CALLBACK_TYPE.SOUND_STOPPED
					),
					(p) => "Failed to set callback.");
			}
		}

		void DisableCallbacks()
		{
			if (_onPlayStateChanged != null || _onMarker != null || _onProgrammerSoundCreated != null)
				return;

			#if ENABLE_IL2CPP
			lock (_callbacks)
				_callbacks.Remove(this.Instance.handle);
			#endif

			if (_callbackSink != null)
			{
				Do(Instance.setCallback(null),
					(p) => "Failed to unset callback.");
				_callbackSink = null;
			}
		}

#if ENABLE_IL2CPP
		[AOT.MonoPInvokeCallback(typeof(global::FMOD.Studio.EVENT_CALLBACK))]
		static global::FMOD.RESULT FmodEventCallback(global::FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instance, IntPtr parameterPtr)
		{
			if (!new EventInstance(instance).isValid())
			{
				UnityEngine.Debug.LogError("[FMOD] Received callback from invalid event instance.");
				return RESULT.OK;
			}

			FmodEvent ev;
			lock (_callbacks)
			{
				if (!_callbacks.TryGetValue(instance, out ev))
				{
					UnityEngine.Debug.LogError($"[FMOD] Received callback {type} from unknown event instance.");
					return RESULT.OK;
				}
			}
			return FmodEventCallbackHandler(type, parameterPtr, ev);
		}

#else
		[AOT.MonoPInvokeCallback(typeof(global::FMOD.Studio.EVENT_CALLBACK))]
		global::FMOD.RESULT FmodEventCallback(global::FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instance, IntPtr parameterPtr)
		{
			return FmodEventCallbackHandler(type, parameterPtr, this);
		}
#endif

		static RESULT FmodEventCallbackHandler(EVENT_CALLBACK_TYPE type, IntPtr parameterPtr, FmodEvent ev)
		{
			if (ev == null)
			{
				UnityEngine.Debug.LogError($"[FMOD] Received callback {type} from unknown event instance.");
				return RESULT.OK;
			}

			switch (type)
			{
				case EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
					if (ev._onMarker != null)
					{
						var parameter = (TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(TIMELINE_MARKER_PROPERTIES));
						ev._onMarker.Invoke(ev, parameter.name);
					}
					break;
				case EVENT_CALLBACK_TYPE.SOUND_PLAYED:
					if (ev._onSoundPlayed != null)
					{
						if (new Sound(parameterPtr).getName(out string soundName, 512) == RESULT.OK)
							ev._onSoundPlayed.Invoke(ev, soundName);
					}
					break;
				case EVENT_CALLBACK_TYPE.SOUND_STOPPED:
					if (ev._onSoundStopped != null)
					{
						if (new Sound(parameterPtr).getName(out string soundName, 512) == RESULT.OK)
							ev._onSoundStopped.Invoke(ev, soundName);
					}
					break;
				case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
					if (ev._onProgrammerSoundCreated != null)
					{
						var parameter = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));

						Sound sound = ev._onProgrammerSoundCreated.Invoke(ev, parameter.name);
						if (sound.hasHandle())
						{
							parameter.sound = sound.handle;
							parameter.subsoundIndex = -1;
							Marshal.StructureToPtr(parameter, parameterPtr, false);
						}
					}
					break;
				default:
					try
					{
						if (ev._onPlayStateChanged != null)
							ev._onPlayStateChanged.Invoke(ev);
					}
					catch (NullReferenceException)
					{
						UnityEngine.Debug.Log("[FMOD] Something is null here, not sure what.");
					}
					break;
			}
			return RESULT.OK;
		}

		public FmodEvent(string fmodEventPath)
		{
			EventPath = fmodEventPath;
			Instance = FMODUnity.RuntimeManager.CreateInstance(fmodEventPath);
			Do(Instance.getDescription(out Description),
				(p) => "Failed to get event description."
			);
		}

		public void Release()
		{
			if (!Instance.isValid())
				return;

			Do(Instance.setCallback(null, EVENT_CALLBACK_TYPE.ALL), (p) => "Failed to remove callback");
			_callbackSink = null;

			#if ENABLE_IL2CPP
			lock (_callbacks)
				_callbacks.Remove(this.Instance.handle);
			#endif

			Do(Instance.release(), (p) => "Failed to release event instance.");
		}

		void IDisposable.Dispose()
		{
			this.Release();
		}

		~FmodEvent()
		{
			this.Release();
		}

		public float Time
		{
			get => this.TimeMilliseconds / 1000f;
			set => this.TimeMilliseconds = Mathf.RoundToInt(value * 1000);
		}

		public int TimeMilliseconds
		{
			get
			{
				int ms;
				Do(Instance.getTimelinePosition(out ms), (p) => "Failed to get timeline position");
				return ms;
			}
			set
			{
				Do(Instance.setTimelinePosition(value), (p) => "Failed to set timeline position");
			}
		}

		public PLAYBACK_STATE PlaybackState
		{
			get
			{
				PLAYBACK_STATE state;
				Do(Instance.getPlaybackState(out state),
					(p) => "Failed to get playback state.");
				return state;
			}
		}


		public Vector3 Position
		{
			//get { throw new NotImplementedException(); }
			set
			{
				Instance.set3DAttributes(RuntimeUtils.To3DAttributes(value));
			}
		}

		public FmodEvent SetPosition(Vector3 position)
		{
			this.Position = position;
			return this;
		}

		public bool IsValid
		{
			get { return Instance.isValid(); }
		}

		public float Length
		{
			get => LengthMilliseconds / 1000f;
		}

		public int LengthMilliseconds
		{
			get
			{
				int length = 0;
				Do(Description.getLength(out length), (p) => "Failed to get event timeline length.");
				return length;
			}
		}

		public FmodEvent Start()
		{
			Do(Instance.start(), (p) => "Failed to start event.");
			return this;
		}

		public FmodEvent Pause()
		{
			IsPaused = true;
			return this;
		}


		public bool IsPaused
		{
			get
			{
				bool paused = false;
				Do(Instance.getPaused(out paused),
					(p) => "Failed to check if event is paused.");
				return paused;
			}
			set
			{
				Do(Instance.setPaused(value),
					(p) => "Failed to change pause state.");
			}
		}

		public FmodEvent Resume()
		{
			Do(Instance.setPaused(false),
				(p) => "Failed to resume event.");
			return this;
		}

		public FmodEvent Stop(bool hard = false)
		{
			Do(Instance.stop(hard ? global::FMOD.Studio.STOP_MODE.IMMEDIATE : global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT),
				(p) => "Failed to stop event instance.");
			return this;
		}

		public FmodEvent LoadMarkers()
		{
			_markers = FmodMarker.Load(this.EventPath);
			_markersLoaded = true;

			return this;
		}

		public bool HasMarker(string marker)
		{
			if (!_markersLoaded)
				LoadMarkers();
			return _markers != null && _markers.ContainsKey(marker);
		}

		public FmodEvent JumpToMarker(string marker)
		{
			if (!IsValid)
				return this;

			if (!_markersLoaded)
				LoadMarkers();

			marker = marker.Trim();
			if (_markers == null)
			{
				UnityEngine.Debug.LogError("[FMOD] No markers available. Try calling LoadMarkers() first");
				return this;
			}

			int ms;
			if (_markers.TryGetValue(marker, out ms))
			{
				this.TimeMilliseconds = ms;
			}
			else
				UnityEngine.Debug.LogError($"[FMOD] Can't find marker named '{marker}'");

			return this;
		}

		public float this[string name]
		{
			get {
				float val, fval;
				Do(Instance.getParameterByName(name, out val, out fval), (p) => string.Format("Failed to get parameter '{0}'.", p), name);
				return fval;
			}
			set
			{
				Do(Instance.setParameterByName(name, value), (p) => string.Format("Failed to set parameter '{0}'.", p), name, FmodSeverity.Warning);
			}
		}

		public float GetParamFinal(string name)
		{
			return this[name];
		}

		public float GetParamRaw(string name)
		{
			float val, fval;
			Do(Instance.getParameterByName(name, out val, out fval), (p) => string.Format("Failed to get parameter '{0}'.", p), name);
			return val;
		}

		public FmodEvent SetParam(string name, float value)
		{
			this[name] = value;
			return this;
		}

		void Do(RESULT result, Func<string,string> error, FmodSeverity severity = FmodSeverity.Error)
		{
			FmodUtils.Check(result, error, null, this.EventPath, severity);
		}

		void Do(RESULT result, Func<string,string> error, string errorParam, FmodSeverity severity = FmodSeverity.Error)
		{
			FmodUtils.Check(result, error, errorParam, this.EventPath, severity);
		}

		public static void PlayOneShot(string eventPath)
		{
			FMODUnity.RuntimeManager.PlayOneShot(eventPath);
		}

		public static void PlayOneShot(string eventPath, Action<FmodEvent> init)
		{
			var ev = new FmodEvent(eventPath);
			init(ev);
			ev.Start().Release();
		}

		public static void PlayAndReleaseWhenDone(string eventPath, Action<FmodEvent> init)
		{
			var ev = new FmodEvent(eventPath);
			init(ev);
			ev.OnPlayStateChanged += e =>
			{
				if (e.PlaybackState == PLAYBACK_STATE.STOPPING)
					e.Release();
			};
			ev.Start();
		}

		public static void KillAllInTransform(Transform transform, bool withChildren)
		{
			MonoBehaviour[] scripts = withChildren ?
				transform.GetComponentsInChildren<MonoBehaviour>(true) :
				transform.GetComponents<MonoBehaviour>();

			foreach (MonoBehaviour script in scripts)
			{
				KillAllInScript(script);
			}
		}

		public static void KillAllInScript(MonoBehaviour script)
		{
			Type type = script.GetType();
			PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			IEnumerable<FmodEvent> events =
				props.Where(prop => typeof(FmodEvent).IsAssignableFrom(prop.PropertyType)).Select(prop => (FmodEvent)prop.GetValue(script, null))
				.Union(
					fields.Where(field => typeof(FmodEvent).IsAssignableFrom(field.FieldType)).Select(field => (FmodEvent)field.GetValue(script))
				)
				;

			foreach (FmodEvent ev in events)
				StopAndRelease(ev, true);
		}

		public static bool IsValidAndNotNull(FmodEvent ev)
		{
			return (ev != null && ev.IsValid);
		}

		public static bool IsValidAndPlaying(FmodEvent ev)
		{
			return IsValidAndNotNull(ev) && ev.PlaybackState != PLAYBACK_STATE.STOPPED;
		}

		public static bool Stop(FmodEvent ev, bool hard = false)
		{
			bool valid;
			if (valid = IsValidAndNotNull(ev))
				ev.Stop(hard);
			return valid;
		}

		public static bool Release(FmodEvent ev)
		{
			bool valid;
			if (valid = IsValidAndNotNull(ev))
				ev.Release();
			return valid;
		}

		public static bool StopAndRelease(FmodEvent ev, bool hard = false)
		{
			Stop(ev, hard);
			return Release(ev);
		}
	}

	[Serializable]
	public class FmodMarker
	{
		public string name;
		public double position;

		public static string MarkerJsonFolder = "FMOD Markers";

		public static Dictionary<string, int> Load(TextAsset markerJson)
		{
			FmodMarker[] markers = JsonUtils.DeserializeArray<FmodMarker>(markerJson.text);
			var output = new Dictionary<string, int>();
			for (int i = 0; i < markers.Length; i++)
				output[markers[i].name] = Mathf.FloorToInt((float)(markers[i].position * 1000));

			return output;
		}

		public static Dictionary<string, int> Load(string eventPath)
		{
			string resourcePath = string.Format("{0}/{1}", MarkerJsonFolder, Path.GetFileNameWithoutExtension(eventPath));
			var jsonAsset = Resources.Load(resourcePath);
			if (jsonAsset == null)
			{
				//UnityEngine.Debug.LogErrorFormat("[FMOD] Could not find marker file Resources/{0}.json", resourcePath);
				return null;
			}
			return Load((TextAsset)jsonAsset);
		}
	}

	[Serializable]
	public class FmodException : Exception
	{
		public FmodException() { }
		public FmodException(string message) : base(message) { }
		public FmodException(string message, Exception inner) : base(message, inner) { }
		protected FmodException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}