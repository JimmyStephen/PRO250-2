using Fusion;
using UnityEngine;

namespace Projectiles
{
	public struct WeaponDesires
	{
		public bool AmmoAvailable;
		public bool Fire;
		public bool HasFired;
		public bool Reload;
	}

	public class Weapon : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public int            WeaponSlot       => _weaponSlot;
		public bool           IsArmed          { get; private set; }

		public Transform[]    BarrelTransforms { get; private set; }
		public WeaponAction[] WeaponActions    => _weaponActions;

		public string         DisplayName      => _displayName;
		public Sprite         Icon             => _icon;

		// PRIVATE MEMBERS

		[SerializeField]
		private int _weaponSlot;

		[Header("UI")]
		[SerializeField]
		private string _displayName;
		[SerializeField]
		private Sprite _icon;

		private WeaponAction[] _weaponActions;

		// PUBLIC METHODS

		public void ArmWeapon()
		{
			if (IsArmed == true)
				return;

			IsArmed = true;
			OnArmed();
		}

		public void DisarmWeapon()
		{
			if (IsArmed == false)
				return;

			IsArmed = false;
			OnDisarmed();
		}

		public virtual void ProcessInput(WeaponContext context)
		{
			Assert.Check(Runner.Stage != default, "Process input should be called from FixedUpdateNetwork");

			// When weapon is busy (e.g. firing, reloading) we cannot start new actions
			bool isBusy = IsBusy();

			for (int i = 0; i < _weaponActions.Length; i++)
			{
				if (i > 0 && isBusy == false)
				{
					// Check busy status of previous weapon action
					// because it might changed this tick
					isBusy |= _weaponActions[i - 1].IsBusy();
				}

				_weaponActions[i].ProcessInput(context, isBusy);
			}
		}

		public virtual void OnRender(WeaponContext context)
		{
			for (int i = 0; i < _weaponActions.Length; i++)
			{
				_weaponActions[i].OnRender(context);
			}
		}

		public bool IsBusy()
		{
			for (int i = 0; i < _weaponActions.Length; i++)
			{
				if (_weaponActions[i].IsBusy() == true)
					return true;
			}

			return false;
		}

		// PROTECTED METHODS

		protected virtual void OnArmed()
		{
			// Do visual effects, sounds here
			// OnArmed is executed in render only
		}

		protected virtual void OnDisarmed()
		{
			// OnDisarmed is executed in render only
		}

		// MONOBEHAVIOUR

		protected virtual void Awake()
		{
			_weaponActions = GetComponentsInChildren<WeaponAction>(false);

			if (_weaponActions.Length > 0)
			{
				BarrelTransforms = new Transform[_weaponActions.Length];

				for (int i = 0; i < _weaponActions.Length; i++)
				{
					_weaponActions[i].Initialize(this, (byte)i);

					BarrelTransforms[i] = _weaponActions[i].BarrelTransform;
				}
			}
			else
			{
				// Make sure there is at least one dummy barrel transform
				BarrelTransforms = new Transform[] { transform };
			}
		}
	}
}
