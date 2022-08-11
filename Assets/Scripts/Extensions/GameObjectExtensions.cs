namespace Projectiles
{
	using System.Collections.Generic;
	using UnityEngine;

	public static partial class GameObjectExtensions
	{
		// PUBLIC METHODS

		public static T GetComponentNoAlloc<T>(this GameObject gameObject) where T : class
		{
			return GameObjectExtensions<T>.GetComponentNoAlloc(gameObject);
		}

		public static void SetActiveSafe(this GameObject gameObject, bool value)
		{
			if (gameObject == null)
				return;

			if (gameObject.activeSelf == value)
				return;

			gameObject.SetActive(value);
		}

		public static void SetLayer(this GameObject gameObject, int layer, bool includeChildren = false)
		{
			if (includeChildren == false)
			{
				gameObject.layer = layer;
			}
			else
			{
				gameObject.transform.SetLayer(layer, includeChildren);
			}
		}

		public static void SetLayer(this Transform parent, int layer, bool includeChildren = false)
		{
			parent.gameObject.layer = layer;

			if (includeChildren == false)
				return;

			for (int i = 0, count = parent.childCount; i < count; i++)
			{
				parent.GetChild(i).SetLayer(layer, true);
			}
		}
	}

	public static partial class GameObjectExtensions<T> where T : class
	{
		// PRIVATE MEMBERS

		private static List<T> _components = new List<T>();

		// PUBLIC METHODS

		public static T GetComponentNoAlloc(GameObject gameObject)
		{
			_components.Clear();

			gameObject.GetComponents(_components);

			if (_components.Count > 0)
			{
				T component = _components[0];

				_components.Clear();

				return component;
			}

			return null;
		}
	}
}
