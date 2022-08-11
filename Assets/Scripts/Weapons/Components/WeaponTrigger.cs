using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class WeaponTrigger : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private int _cadence = 600;
		[SerializeField]
		private EInputButtons _fireButton = EInputButtons.Fire;
		[SerializeField]
		private bool _fireOnKeyDownOnly;

		[Networked]
		private TickTimer _fireCooldown { get; set; }

		private int _fireTicks;

		// WeaponComponent INTERFACE

		public override bool IsBusy => _fireCooldown.ExpiredOrNotRunning(Runner) == false;

		public override void ProcessInput(WeaponContext context, ref WeaponDesires desires, bool weaponBusy)
		{
			if (weaponBusy == true)
				return;

			if (_fireCooldown.ExpiredOrNotRunning(Runner) == false)
				return;
			
			var fireInput = _fireOnKeyDownOnly == true ? context.PressedInput : context.Input;
			desires.Fire = fireInput.IsSet(_fireButton);
		}

		public override void OnFixedUpdate(WeaponContext context, WeaponDesires desires)
		{
			if (desires.HasFired == true)
			{
				_fireCooldown = TickTimer.CreateFromTicks(Runner, _fireTicks);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			base.Spawned();

			float fireTime = 60f / _cadence;
			_fireTicks = (int)System.Math.Ceiling(fireTime / (double)Runner.DeltaTime);
		}
	}
}