using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class SimpleTurret : NetworkBehaviour, IProjectileManager
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Transform _fireTransform;

		private Weapon _weapon;
		private WeaponContext _weaponContext = new WeaponContext();

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (Object.HasStateAuthority == false)
				return;

			// Fire constantly
			_weaponContext.Input.SetDown(EInputButtons.Fire);
			_weaponContext.FirePosition = _fireTransform.position;
			_weaponContext.FireDirection = _fireTransform.forward;

			_weapon.ProcessInput(_weaponContext);
		}

		public override void Render()
		{
			_weapon.OnRender(_weaponContext);
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_weapon = GetComponentInChildren<Weapon>(true);
			_weaponContext.ProjectileManager = this;
		}

		// IProjectileManager INTERFACE

		StandaloneProjectile IProjectileManager.AddProjectile(StandaloneProjectile projectilePrefab, Vector3 firePosition, Vector3 direction, NetworkObjectPredictionKey? predictionKey, byte weaponAction)
		{
			return Runner.Spawn(projectilePrefab, firePosition, Quaternion.LookRotation(direction), Object.InputAuthority, null, predictionKey);
		}

		void IProjectileManager.AddProjectile(Projectile projectilePrefab, Vector3 firePosition, Vector3 direction, byte weaponAction)
		{
			throw new System.NotImplementedException();
		}
	}
}
