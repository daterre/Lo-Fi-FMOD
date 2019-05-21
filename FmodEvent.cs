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
		event Func<FmodEvent, string, Sound> _onProgrammerSoundCreated;

		global::FMOD.Studio.EVENT_CALLBACK _callbackSink = null;
		Dictionary<string, int> _markers = null;
        bool _markersLoaded = false;

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

		void EnableCallbacks()
		{
			if (_onPlayStateChanged == null && _onMarker == null && _onProgrammerSoundCreated == null)
				return;

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
					EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND
					),
					"Failed to set callback.");
			}
		}
		
		void DisableCallbacks()
		{
			if (_onPlayStateChanged != null || _onMarker != null || _onProgrammerSoundCreated != null)
				return;

			if (_callbackSink != null)
			{
				Do(Instance.setCallback(null),
					"Failed to unset callback.");
				_callbackSink = null;
			}
		}

		[AOT.MonoPInvokeCallbackAttribute(typeof(global::FMOD.Studio.EVENT_CALLBACK))]
		global::FMOD.RESULT FmodEventCallback(global::FMOD.Studio.EVENT_CALLBACK_TYPE type, global::FMOD.Studio.EventInstance instance, IntPtr parameterPtr)
		{
			switch(type)
			{
				case EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
					if (_onMarker != null)
					{
						var parameter = (TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(TIMELINE_MARKER_PROPERTIES));
						_onMarker.Invoke(this, parameter.name);
					}
					break;
				case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
					if (_onProgrammerSoundCreated != null)
					{
						UnityEngine.Debug.Log("CREATE_PROGRAMMER_SOUND");
						var parameter = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));

						Sound sound = _onProgrammerSoundCreated.Invoke(this, parameter.name);
						if (sound.hasHandle())
						{
							parameter.sound = sound.handle;
							parameter.subsoundIndex = -1;
							Marshal.StructureToPtr(parameter, parameterPtr, false);
						}
					}
					break;
				default:
					if (_onPlayStateChanged != null)
					{
						_onPlayStateChanged.Invoke(this);
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
				"Failed to get event description."
			);
		}

		public void Release()
		{
			if (!Instance.isValid())
				return;

			Do(Instance.setCallback(null, EVENT_CALLBACK_TYPE.ALL), "Failed to remove car callback");
			_callbackSink = null;

			Do(Instance.release(),
				"Failed to release event instance.");
		}

		void IDisposable.Dispose()
		{
			this.Release();
		}

		public int TimeMilliseconds
		{
			//get { throw new NotImplementedException(); }
			set { Do(Instance.setTimelinePosition(value), "Failed to set timeline"); }
		}

		public PLAYBACK_STATE PlaybackState
		{
			get
			{
				PLAYBACK_STATE state;
				Do(Instance.getPlaybackState(out state),
					"Failed to get playback state.");
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
			get
			{
				int length = 0;
				Do(Description.getLength(out length), "Failed to get event timeline length.");
				return length / 1000f;
			}
		}

		public FmodEvent Start()
		{
			Do(Instance.start(), "Failed to start event.");
			return this;
		}

		public FmodEvent Pause()
		{
			Do(Instance.setPaused(true),
				"Failed to pause event.");
			return this;
		}

		public FmodEvent Resume()
		{
			Do(Instance.setPaused(false),
				"Failed to resume event.");
			return this;
		}

		public FmodEvent Stop(bool hard = false)
		{
			Do(Instance.stop(hard ? global::FMOD.Studio.STOP_MODE.IMMEDIATE : global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT),
				"Failed to stop event instance.");
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
				UnityEngine.Debug.LogError("No markers available. Try calling LoadMarkers() first");
				return this;
			}

			int ms;
			if (_markers.TryGetValue(marker, out ms))
			{
				this.TimeMilliseconds = ms;
			}
			else
				UnityEngine.Debug.LogErrorFormat("Can't find marker named '{0}'", marker);

			return this;
		}

		public float this[string name]
		{
			get {
				float val, fval;
				Do(Instance.getParameterValue(name, out val, out fval), string.Format("Failed to get parameter '{0}'.", name));
				return fval;
			}
			set
			{
				Do(Instance.setParameterValue(name, value), string.Format("Failed to set parameter '{0}'.", name));
			}
		}

		public FmodEvent SetParam(string name, float value)
		{
			this[name] = value;
			return this;
		}

		void Do(RESULT result, string error, FmodSeverity severity = FmodSeverity.Error)
		{
			FmodUtils.Check(result, error, this.EventPath, severity);
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

		public static void KillAllInTransform(Transform transform, bool withChildren)
		{
			MonoBehaviour[] scripts = withChildren ?
				transform.GetComponentsInChildren<MonoBehaviour>(true) :
				transform.GetComponents<MonoBehaviour>();

			foreach(MonoBehaviour script in scripts)
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

			foreach(FmodEvent ev in events)
			{
				if (ev != null && ev.IsValid)
					ev.Stop(true).Release();
			}
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
				//UnityEngine.Debug.LogErrorFormat("Could not find marker file Resources/{0}.json", resourcePath);
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