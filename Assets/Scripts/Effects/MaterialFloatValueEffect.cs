using System;
using UnityEngine;

namespace Projectiles
{
	[Serializable]
	public class MaterialFloatValueEffect : IEffect
	{
		// PUBLIC MEMBERS

		public bool IsValid => _renderer != null;

		// PRIVATE MEMBERS

		[SerializeField]
		private MeshRenderer _renderer;
		[SerializeField]
		private int _materialSlot = 0;
		[SerializeField]
		private string _valueName = "_Value";
		[SerializeField]
		private float _fromValue = 0f;
		[SerializeField]
		private float _toValue = 1f;
		[SerializeField]
		private float _duration = 3f;

		private Material _materialInstance;
		private int _valueNameID;

		private float _initialValue;
		private float _time;

		// IEffect INTEFACE

		bool IEffect.IsActive   { get; set; }
		bool IEffect.IsFinished => false;

		void IEffect.Activate()
		{
			_time = 0f;

			if (_materialInstance == null)
			{
				_materialInstance = _renderer.materials[_materialSlot];
				_valueNameID = Shader.PropertyToID(_valueName);

				_initialValue = _materialInstance.GetFloat(_valueNameID);
			}

			_materialInstance.SetFloat(_valueNameID, _fromValue);
		}

		void IEffect.Update()
		{
			_time += Time.deltaTime;
			float value = Mathf.Lerp(_fromValue, _toValue, _time / _duration);

			_materialInstance.SetFloat(_valueNameID, value);
		}

		void IEffect.Deactivate()
		{
			_materialInstance.SetFloat(_valueNameID, _initialValue);
		}
	}
}