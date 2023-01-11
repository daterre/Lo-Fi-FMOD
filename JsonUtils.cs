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

		public static string SerializeArray<T>(T[] arr)
		{
			Wrapper<T> wrapper = new Wrapper<T>() { array = arr };
			string json = JsonUtility.ToJson(wrapper);
			int openBracket = json.IndexOf('[');
			return json.Substring(openBracket, json.LastIndexOf(']') - openBracket + 1);
		}

		[System.Serializable]
		private class Wrapper<T>
		{
			public T[] array;
		}
	}
}