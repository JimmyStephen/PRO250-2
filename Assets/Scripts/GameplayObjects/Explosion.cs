using Fusion;

namespace Projectiles
{
	using UnityEngine;

	public class Explosion : ContextBehaviour, IPredictedSpawnBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private LayerMask  _targetMask;
		[SerializeField]
		private LayerMask  _blockingMask;
		[SerializeField]
		private EHitType   _hitType = EHitType.Explosion;

		[SerializeField, Tooltip("It is usually better to check from point slightly above explosion to filter out terrain discrepancies")]
		private Vector3    _explosionCheckOffset = new Vector3(0f, 0.5f, 0f);
		[SerializeField]
		private float      _innerRadius = 1f;
		[SerializeField]
		private float      _outerRadius = 6f;

		[SerializeField]
		private float      _innerDamage = 100f;
		[SerializeField]
		private float      _outerDamage = 10f;

		[SerializeField]
		private float      _despawnDelay = 3f;

		[SerializeField]
		private Transform  _effectRoot;


		private TickTimer  _despawnTimer;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			base.Spawned();

			ShowEffect();

			if (Object.HasStateAuthority == true || Object.IsPredictedSpawn == true)
			{
				Explode();
			}

			if (Object.IsPredictedSpawn == false)
			{
				_despawnTimer = TickTimer.CreateFromSeconds(Runner, _despawnDelay);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (Object.HasStateAuthority == false)
				return;
			if (_despawnTimer.Expired(Runner) == false)
				return;

			Runner.Despawn(Object);
		}

		// IPredictedSpawnBehaviour INTERFACE

		void IPredictedSpawnBehaviour.PredictedSpawnSpawned()
		{
			Spawned();
		}

		void IPredictedSpawnBehaviour.PredictedSpawnUpdate()
		{
			FixedUpdateNetwork();
		}

		void IPredictedSpawnBehaviour.PredictedSpawnRender()
		{
			Render();
		}

		void IPredictedSpawnBehaviour.PredictedSpawnFailed()
		{
			Runner.Despawn(Object, true);
		}

		void IPredictedSpawnBehaviour.PredictedSpawnSuccess()
		{
			// Nothing special is needed
		}

		// PRIVATE METHODS

		private void Explode()
		{
			var hits = ListPool.Get<LagCompensatedHit>(16);
			var hitRoots = ListPool.Get<int>(16);

			var position = transform.position + _explosionCheckOffset;

			int count = Runner.LagCompensation.OverlapSphere(position, _outerRadius, Object.InputAuthority, hits, _targetMask);
			bool damageFalloff = _innerRadius < _outerRadius && _innerDamage != _outerDamage;

			for (int i = 0; i < count; i++)
			{
				var hit = hits[i];

				if (hit.Hitbox == null)
					continue;

				var hitTarget = hit.Hitbox.Root.GetComponent<IHitTarget>();

				if (hitTarget == null)
					continue;

				int hitRootID = hit.Hitbox.Root.GetInstanceID();
				if (hitRoots.Contains(hitRootID) == true)
					continue; // Same object was hit multiple times

				// TODO: Replace this when detailed hit info will be fixed
				var direction = hit.GameObject.transform.position - position;
				float distance = direction.magnitude;
				direction /= distance; // Normalize

				// Check if direction to the hitbox is not obstructed
				if (Runner.GetPhysicsScene().Raycast(position, direction, distance, _blockingMask) == true)
					continue;

				hitRoots.Add(hitRootID);

				float damage = _innerDamage;

				if (damageFalloff == true && distance > _innerRadius)
				{
					damage = MathUtility.Map(_innerRadius, _outerRadius, _innerDamage, _outerDamage, distance);
				}

				// TODO: Remove this when detailed hit info will be fixed
				hit.Point = hit.GameObject.transform.position;
				hit.Normal = -direction;

				HitUtility.ProcessHit(Object.InputAuthority, direction, hit, damage, _hitType);
			}

			ListPool.Return(hitRoots);
			ListPool.Return(hits);
		}

		private void ShowEffect()
		{
			if (Runner.Mode == SimulationModes.Server)
				return;

			if (_effectRoot != null)
			{
				_effectRoot.SetActive(true);
				_effectRoot.localScale = Vector3.one * _outerRadius * 2f;
			}
		}
	}
}
