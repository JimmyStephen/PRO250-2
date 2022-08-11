using Fusion;
using UnityEngine;

namespace Projectiles
{
	public abstract class StandaloneProjectile : ContextBehaviour, IPredictedSpawnBehaviour
	{
		// PUBLIC MEMBERS

		public PlayerRef PredictedInputAuthority;

		// PUBLIC METHODS
		
		// Projectiles are fired from camera, weapon fire position specifies
		// from which position projectile visuals should fly out
		public virtual void SetWeaponBarrelPosition(Vector3 position)
		{
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
	}
}
