using System.Collections.Generic;
using UnityEngine;

namespace Projectiles
{
	public interface IEffect
	{
		bool IsActive   { get; set; }
		bool IsFinished { get; }

		void Activate();
		void Deactivate();
		void Update();
	}

	public class EffectsComponent : MonoBehaviour
	{
		// PRIVATE MEMBERS

		private List<EffectData> _effects = new List<EffectData>(32);

		// PUBLIC METHODS

		public void ActivateEffect(IEffect effect, bool value)
		{
			if (effect == null)
				return;

			if (value == true && effect.IsActive == false)
			{
				ActivateEffect(effect);
			}
			else if (value == false && effect.IsActive == true)
			{
				DeactivateEffect(effect);
			}
		}

		public void ActivateEffect(IEffect effect, float maxDuration = 0f)
		{
			if (effect == null)
				return;

			if (effect.IsActive == true && maxDuration <= 0f)
				return; // Effect already active and we do not want to prolong it

			int index = GetEffectIndex(effect);

			if (index >= 0)
			{
				// Prolong effect
				var effectData = _effects[index];

				effectData.FinishTime = maxDuration > 0f ? Time.timeSinceLevelLoad + maxDuration : 0f;

				_effects[index] = effectData;
			}
			else
			{
				_effects.Add(new EffectData()
				{
					Effect = effect,
					FinishTime = maxDuration > 0f ? Time.timeSinceLevelLoad + maxDuration : 0f,
				});

				effect.IsActive = true;
				effect.Activate();
			}
		}

		public void DeactivateEffect(IEffect effect)
		{
			if (effect == null)
				return;

			int index = GetEffectIndex(effect);

			if (index < 0)
				return;

			DeactivateEffect(index, true);
		}

		public void DeactivateAllEffects()
		{
			for (int i = 0; i < _effects.Count; i++)
			{
				DeactivateEffect(i, false);
			}

			_effects.Clear();
		}

		// MONOBEHAVIOUR

		protected void Update()
		{
			float currentTime = Time.timeSinceLevelLoad;

			for (int i = _effects.Count - 1; i >= 0; i--)
			{
				var effectData = _effects[i];

				if (effectData.FinishTime > 0f && effectData.FinishTime <= currentTime)
				{
					DeactivateEffect(i, true);
					continue;
				}

				effectData.Effect.Update();

				if (effectData.Effect.IsFinished == true)
				{
					DeactivateEffect(i, true);
				}
			}
		}

		protected void OnDisable()
		{
			DeactivateAllEffects();
		}

		// PRIVATE MEMBERS

		private void DeactivateEffect(int index, bool remove)
		{
			var effect = _effects[index].Effect;

			effect.Deactivate();
			effect.IsActive = false;

			if (remove == true)
			{
				_effects.RemoveAt(index);
			}
		}

		private int GetEffectIndex(IEffect effect)
		{
			for (int i = 0; i < _effects.Count; i++)
			{
				if (effect == _effects[i].Effect)
					return i;
			}

			return -1;
		}

		// CLASSES / STRUCTS / ENUMS

		public struct EffectData
		{
			public IEffect Effect;
			public float FinishTime;
		}
	}
}