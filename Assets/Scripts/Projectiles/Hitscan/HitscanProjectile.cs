using Fusion;
using UnityEngine;

namespace Projectiles
{
	public abstract class HitscanProjectile : Projectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		protected float _maxDistance = 200f;

		// Projectile INTERFACE

		public override ProjectileData GetFireData(NetworkRunner runner, Vector3 firePosition, Vector3 fireDirection)
		{
			return new ProjectileData()
			{
				FirePosition = firePosition,
				FireVelocity = fireDirection,
			};
		}

		public override void OnFixedUpdate(ProjectileContext context, ref ProjectileData data)
		{
			if (ProjectileUtility.ProjectileCast(context.Runner, context.InputAuthority, data.FirePosition, data.FireVelocity, _maxDistance, _hitMask, out LagCompensatedHit hit) == true)
			{
				HitUtility.ProcessHit(context.InputAuthority, data.FireVelocity, hit, _damage, _hitType);

				SpawnImpact(context, ref data, hit.Point, (hit.Normal + -data.FireVelocity) * 0.5f);
			}

			// Hitscan projectile is immediately finished
			data.IsFinished = true;
		}
	}
}
