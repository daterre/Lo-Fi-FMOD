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
	public static class FmodUtils
	{
		static Func<string,string> _defaultError = (p) => "Action returned error.";
		public static void Check(RESULT result, Func<string,string> error = null, string errorParam = null, string path = null, FmodSeverity severity = FmodSeverity.Exception)
		{
			if (result == RESULT.OK)
				return;

			string msg = string.Format("[FMOD] {0} ({1})",
				(error == null ? _defaultError : error).Invoke(errorParam),
				result);

			if (path != null)
				msg += string.Format("\nEvent path: {0}", path);

			if (severity == FmodSeverity.Exception)
				throw new FmodException(msg);
			else if (severity == FmodSeverity.Error)
				UnityEngine.Debug.LogError(msg);
			else
				UnityEngine.Debug.LogWarning(msg);

		}
		/*
		public static global::FMOD.Sound CreateSound(int sampleSize, int channels = 1, int sampleRate = 44100)
		{
			// Explicitly create the delegate object and assign it to a member so it doesn't get freed
			// by the garbage collected while it's being used
			//_pcmReadCallback = new global::FMOD.SOUND_PCMREADCALLBACK(PcmReadCallback);
			//_pcmSetPosCallback = new global::FMOD.SOUND_PCMSETPOSCALLBACK(PcmSetPosCallback);

			global::FMOD.CREATESOUNDEXINFO soundInfo = new global::FMOD.CREATESOUNDEXINFO()
			{
				cbsize = Marshal.SizeOf(typeof(global::FMOD.CREATESOUNDEXINFO)),
				format = global::FMOD.SOUND_FORMAT.PCMFLOAT,
				defaultfrequency = sampleRate,
				length = (uint)(sampleSize * channels * sizeof(float)),
				numchannels = channels,
				//pcmreadcallback = _pcmReadCallback,
				//pcmsetposcallback = _pcmSetPosCallback
			};

			global::FMOD.Sound sound;
			FmodUtils.Check(FMODUnity.RuntimeManager.LowlevelSystem.createSound(string.Empty, global::FMOD.MODE.OPENUSER | global::FMOD.MODE.LOOP_NORMAL, ref soundInfo, out sound));
			return sound;
		}
		*/
	}


	public enum FmodSeverity
	{
		Warning,
		Error,
		Exception
	}
}