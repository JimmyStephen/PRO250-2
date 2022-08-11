using System;
using Fusion;

namespace Projectiles
{
	using UnityEngine;

	public class Health : ContextBehaviour, IHitTarget, IHitInstigator
	{
		// PUBLIC MEMBERS

		public event Action<HitData> HitTaken;
		public event Action<HitData> HitPerformed;
		public event Action<HitData> FatalHitTaken;

		public bool    IsAlive       => CurrentHealth > 0f;
		public bool    IsImmortal    => _immortalCooldown.ExpiredOrNotRunning(Runner) == false;
		public float   MaxHealth     => _maxHealth;

		[Networked, HideInInspector]
		public float   CurrentHealth { get; private set; }

		// PRIVATE MEMBERS

		[SerializeField]
		private float _maxHealth = 100f;
		[SerializeField]
		private Transform _headPivot;
		[SerializeField]
		private Transform _bodyPivot;
		[SerializeField]
		private Transform _groundPivot;

		[Networked]
		private int _hitCount { get; set; }
		[Networked, Capacity(8)]
		private NetworkArray<Hit> _hits { get; }

		[Networked]
		private TickTimer _immortalCooldown { get; set; }

		private int _visibleHitCount;


		// PUBLIC METHODS

		public void SetImmortality(float duration)
		{
			_immortalCooldown = TickTimer.CreateFromSeconds(Runner, duration);
		}

		public void ClearImmortality()
		{
			_immortalCooldown = default;
		}

		public void ResetHealth()
		{
			CurrentHealth = _maxHealth;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			_visibleHitCount = _hitCount;
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			HitTaken = null;
			HitPerformed = null;
			FatalHitTaken = null;
		}

		public override void Render()
		{
			if (Runner.Simulation.Mode != SimulationModes.Server)
			{
				UpdateVisibleHits();
			}
		}

		public override void CopyBackingFieldsToState(bool firstTime)
		{
			InvokeWeavedCode();

			CurrentHealth = _maxHealth;
		}

		// IHitTarget INTERFACE

		bool IHitTarget.IsActive => Object != null && IsAlive;

		Transform IHitTarget.HeadPivot   => _headPivot != null ? _headPivot : transform;
		Transform IHitTarget.BodyPivot   => _bodyPivot != null ? _bodyPivot : transform;
		Transform IHitTarget.GroundPivot => _groundPivot != null ? _groundPivot : transform;

		void IHitTarget.ProcessHit(ref HitData hitData)
		{
			ApplyHit(ref hitData);

			if (hitData.Amount == 0)
				return;

			if (IsAlive == false)
			{
				hitData.IsFatal = true;
			}

			if (Object.HasStateAuthority == true)
			{
				// On state authority we fire events immediately
				OnHitTaken(ref hitData);
			}
		}

		// IHitInstigator INTERFACE

		void IHitInstigator.HitPerformed(HitData hitData)
		{
			if (hitData.Amount > 0 && hitData.Target != (IHitTarget)this && Runner.IsResimulation == false)
			{
				HitPerformed?.Invoke(hitData);
			}
		}

		// PRIVATE METHODS

		private void ApplyHit(ref HitData hitData)
		{
			if (IsAlive == false || IsImmortal == true)
			{
				hitData.Amount = 0f;
				return;
			}

			if (hitData.Action == EHitAction.Damage)
			{
				hitData.Amount = RemoveHealth(hitData.Amount);
			}
			else if (hitData.Action == EHitAction.Heal)
			{
				hitData.Amount = AddHealth(hitData.Amount);
			}

			if (hitData.Amount <= 0)
				return;

			_hitCount++;

			var hit = new Hit
			{
				Action           = hitData.Action,
				Damage           = hitData.Amount,
				Direction        = hitData.Direction,
				RelativePosition = hitData.Position != Vector3.zero ? hitData.Position - transform.position : Vector3.zero,
				Instigator       = hitData.InstigatorRef,
			};

			int hitIndex = _hitCount % _hits.Length;
			_hits.Set(hitIndex, hit);
		}

		private float AddHealth(float amount)
		{
			float previousHealth = CurrentHealth;
			SetHealth(CurrentHealth + amount);
			return CurrentHealth - previousHealth;
		}

		private float RemoveHealth(float amount)
		{
			float previousHealth = CurrentHealth;
			SetHealth(CurrentHealth - amount);
			return previousHealth - CurrentHealth;
		}

		private void SetHealth(float health)
		{
			CurrentHealth = Mathf.Clamp(health, 0, _maxHealth);
		}

		private void UpdateVisibleHits()
		{
			if (_visibleHitCount == _hitCount)
				return;

			int hitCount = _hits.Length;
			int oldestHit = _hitCount - hitCount + 1;

			for (int i = Mathf.Max(_visibleHitCount + 1, oldestHit); i <= _hitCount; i++)
			{
				int hitIndex = i % hitCount;
				var hit = _hits.Get(hitIndex);

				var hitData = new HitData
				{
					Action        = hit.Action,
					Amount        = hit.Damage,
					Position      = transform.position + hit.RelativePosition,
					Direction     = hit.Direction,
					Normal        = -hit.Direction,
					Target        = this,
					InstigatorRef = hit.Instigator,
					IsFatal       = CurrentHealth <= 0f,
				};

				if (Object.HasStateAuthority == false)
				{
					OnHitTaken(ref hitData);
				}
			}

			_visibleHitCount = _hitCount;
		}

		private void OnHitTaken(ref HitData hitData)
		{
			// We use _hitData buffer to inform instigator about successful hit as this needs
			// to be synchronized over network as well (e.g. when spectating other players)
			if (hitData.InstigatorRef.IsValid == true && hitData.InstigatorRef == Context.ObservedPlayerRef)
			{
				var instigator = hitData.Instigator;

				if (instigator == null)
				{
					var playerObject = Runner.GetPlayerObject(hitData.InstigatorRef);
					var agent = playerObject != null ? playerObject.GetComponent<Player>().ActiveAgent : null;

					instigator = agent != null ? agent.Health : null;
				}

				if (instigator != null)
				{
					instigator.HitPerformed(hitData);
				}
			}

			HitTaken?.Invoke(hitData);

			if (hitData.IsFatal == true)
			{
				FatalHitTaken?.Invoke(hitData);
			}
		}

		// HELPERS

		public struct Hit : INetworkStruct
		{
			public EHitAction Action;
			public float      Damage;
			[Networked, Accuracy(0.01f)]
			public Vector3    RelativePosition { get; set; }
			[Networked, Accuracy(0.1f)]
			public Vector3    Direction { get; set; }
			public PlayerRef  Instigator;
		}
	}
}
