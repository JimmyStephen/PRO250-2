using UnityEngine;

namespace Projectiles
{
	// TODO: Have NetworkedWeaponComponent and WeaponComponent to avoid unnecessary network behaviours?
	[RequireComponent(typeof(WeaponAction))]
	public abstract class WeaponComponent : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public byte         WeaponActionId    { get; set; }
		public Weapon       Weapon            { get; set; }
		public Transform    BarrelTransform   { get; set; }
		public int          Priority          => _priority;

		public virtual bool IsBusy            => false;

		// PRIVATE MEMBERS

		[SerializeField]
		private int _priority;

		// PROTECTED METHODS

		public abstract void ProcessInput(WeaponContext context, ref WeaponDesires desires, bool weaponBusy);
		public abstract void OnFixedUpdate(WeaponContext context, WeaponDesires desires);
		public virtual void OnRender(WeaponContext context, ref WeaponDesires desires) { }

		protected void HasFired()
		{
			var weaponAction = Weapon.WeaponActions[WeaponActionId];
			weaponAction.HasFired();
		}
	}
}
