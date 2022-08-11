using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class WeaponBarrel : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Projectile _projectilePrefab;
		[SerializeField]
		private StandaloneProjectile _standaloneProjectilePrefab;
		[SerializeField]
		private int _projectilesPerShot = 1;
		[SerializeField]
		private float _dispersion = 1f;

		// WeaponComponent INTERFACE

		public override void ProcessInput(WeaponContext context, ref WeaponDesires desires, bool weaponBusy)
		{
			if (desires.Fire == true && desires.AmmoAvailable == true)
			{
				desires.HasFired = true;
			}
		}

		public override void OnFixedUpdate(WeaponContext context, WeaponDesires desires)
		{
			if (desires.HasFired == false)
				return;

			if (_dispersion > 0f)
			{
				Random.InitState(Runner.Simulation.Tick * unchecked((int)Object.Id.Raw));
			}

			var direction = context.FireDirection;

			for (int i = 0; i < _projectilesPerShot; i++)
			{
				var projectileDirection = direction;

				if (_dispersion > 0f)
				{
					// We use sphere on purpose -> non-uniform distribution (more projectiles in the center)
					var randomDispersion = Random.insideUnitSphere * _dispersion;
					projectileDirection = Quaternion.Euler(randomDispersion.x, randomDispersion.y, randomDispersion.z) * direction;
				}

				if (_projectilePrefab != null)
				{
					context.ProjectileManager.AddProjectile(_projectilePrefab, context.FirePosition, projectileDirection, WeaponActionId);
					HasFired();
				}
				else
				{
					// Create unique prediction key
					var predictionKey = new NetworkObjectPredictionKey
					{
						Byte0 = (byte)Runner.Simulation.Tick, // Low number part is enough
						Byte1 = (byte)Object.InputAuthority.PlayerId,
						Byte2 = (byte)i,
					};

					var standaloneProjectile = context.ProjectileManager.AddProjectile(_standaloneProjectilePrefab, context.FirePosition, projectileDirection, predictionKey, WeaponActionId);
					if (standaloneProjectile != null)
					{
						standaloneProjectile.SetWeaponBarrelPosition(BarrelTransform.position);
						HasFired();
					}
				}
			}
		}
	}
}
