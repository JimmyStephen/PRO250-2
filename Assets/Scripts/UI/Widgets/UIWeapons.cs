using System.Collections.Generic;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIWeapons : UIWidget
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private UIWeapon _weaponDescription;
		[SerializeField]
		private UIWeaponList _weaponThumbnails;

		private int _currentWeaponSlot;
		private List<Weapon> _weapons = new List<Weapon>(32);

		private int _lastVersion;

		// PUBLIC METHODS

		public void UpdateWeapons(Weapons weapons)
		{
			UpdateData(weapons);

			if (_currentWeaponSlot != weapons.CurrentWeaponSlot)
			{
				_weaponDescription.SetData(weapons.CurrentWeapon);
				_currentWeaponSlot = weapons.CurrentWeaponSlot;

				_weaponThumbnails.Selection = _weapons.FindIndex(t => t.WeaponSlot == _currentWeaponSlot);
			}
		}

		// UIWidget INTERFACE

		protected override void OnInitialize()
		{
			base.OnInitialize();

			_weaponThumbnails.UpdateContent += OnUpdateThumbnails;
		}

		protected override void OnDeinitialize()
		{
			_weaponThumbnails.UpdateContent -= OnUpdateThumbnails;

			base.OnDeinitialize();
		}

		// PRIVATE METHODS

		private void UpdateData(Weapons weapons)
		{
			if (weapons.Version == _lastVersion)
				return; // Weapons did not change

			_weapons.Clear();
			weapons.GetAllWeapons(_weapons);

			// We are skipping first weapon (Unarmed)
			_weapons.RemoveAt(0);

			_weaponThumbnails.Refresh(_weapons.Count);
			_currentWeaponSlot = -1;

			_lastVersion = weapons.Version;
		}

		private void OnUpdateThumbnails(int index, UIWeapon weapon)
		{
			weapon.SetData(_weapons[index]);
		}
	}
}