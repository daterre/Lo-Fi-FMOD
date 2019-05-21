using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LoFiPeople.FMOD
{
	public static class JsonUtils
	{
		
		public static T[] DeserializeArray<T>(string json)
		{
			string newJson = "{ \"array\": " + json + "}";
			Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
			return wrapper.array;
		}

		[System.Serializable]
		private class Wrapper<T>
		{
			public T[] array;
		}
	}
}