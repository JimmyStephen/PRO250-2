using UnityEngine;

namespace Projectiles
{
	public class AgentBody : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private GameObject _immortalityEffect;
		[SerializeField]
		private MaterialFloatValueEffect _dissolveEffect;

		private EffectsComponent _effects;
		private Health _health;

		// ContextBehaviour INTERFACE

		public override void Spawned()
		{
			_health.FatalHitTaken += OnFatalHit;
		}

		public override void Render()
		{
			_immortalityEffect.SetActive(_health.IsImmortal);
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_health = GetComponent<Health>();
			_effects = GetComponent<EffectsComponent>();
		}

		// PRIVATE METHODS

		private void OnFatalHit(HitData hit)
		{
			if (_dissolveEffect.IsValid == true)
			{
				_effects.ActivateEffect(_dissolveEffect);
			}
		}
	}
}