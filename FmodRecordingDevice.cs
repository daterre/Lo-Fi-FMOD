using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FMOD;
using FMODUnity;
using FMOD.Studio;
using UnityEngine;
using System.Runtime.InteropServices;

namespace LoFiPeople.FMOD
{
	public class FmodRecordingDevice
	{
		public int DeviceIndex { get; private set; }
		public string Name { get; private set; }
		public int SampleRate { get; private set; }
		public int Channels { get; private set; }
		public DRIVER_STATE State { get; private set; }

		public int DesiredLatency
		{
			/* User specified latency */
			get { return (SampleRate * LATENCY_MS) / 1000; }
		}

		public int DriftThreshold
		{
			/* The point where we start compensating for drift */
			get { return (SampleRate * DRIFT_MS) / 1000; }
		}

		const int DRIFT_MS = 1;
		const int LATENCY_MS = 0;

		public global::FMOD.Sound StartRecording()
		{
			

			/*
				Create user sound to record into, then start recording.
			*/
			global::FMOD.CREATESOUNDEXINFO soundInfo = new global::FMOD.CREATESOUNDEXINFO()
			{
				cbsize = Marshal.SizeOf(typeof(global::FMOD.CREATESOUNDEXINFO)),
				format = global::FMOD.SOUND_FORMAT.PCM16,
				defaultfrequency = SampleRate,
				length = (uint)(SampleRate * Channels * sizeof(short)),  /* 1 second buffer, size here doesn't change latency */
				numchannels = Channels
			};

			global::FMOD.Sound sound;
			FmodUtils.Check(RuntimeManager.LowlevelSystem.createSound(string.Empty, global::FMOD.MODE.OPENUSER | global::FMOD.MODE.LOOP_NORMAL, ref soundInfo, out sound));
			FmodUtils.Check(RuntimeManager.LowlevelSystem.recordStart(this.DeviceIndex, sound, true));

			return sound;
		}

		//--------------------------------------------------

		public static IEnumerable<FmodRecordingDevice> GetAllDevices()
		{
			int totalMicCount;
			int connectedMicCount;
			FmodUtils.Check(RuntimeManager.LowlevelSystem.getRecordNumDrivers(out totalMicCount, out connectedMicCount), "Failed to list recording devices.");

			for (int i = 0; i < connectedMicCount; i++)
			{
				yield return GetDevice(i);
			}
		}

		public static FmodRecordingDevice GetDevice(int deviceIndex)
		{
			string name;
			System.Guid guid;
			int systemrate;
			SPEAKERMODE speakermode;
			int speakermodechannels;
			DRIVER_STATE state;

			FmodUtils.Check(RuntimeManager.LowlevelSystem.getRecordDriverInfo(
					deviceIndex,
					out name,
					100,
					out guid,
					out systemrate,
					out speakermode,
					out speakermodechannels,
					out state
				),
				"Failed to get recording device");

			return new FmodRecordingDevice()
			{
				DeviceIndex = deviceIndex,
				Name = name,
				SampleRate = systemrate,
				Channels = speakermodechannels,
				State = state
			};
		}
	}
}
