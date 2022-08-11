using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	public class UIWeapon : UIBehaviour
	{
		// PUBLIC MEMBERS

		public int Slot { get; private set; }

		// PRIVATE MEMBERS

		[SerializeField]
		private Image _icon;
		[SerializeField]
		private TextMeshProUGUI _name;
		[SerializeField]
		private TextMeshProUGUI[] _weaponActions;

		// PUBLIC METHODS

		public void SetData(Weapon weapon)
		{
			if (weapon == null || weapon.Object == null)
				return;

			Slot = weapon.WeaponSlot;

			_name.SetTextSafe(weapon.DisplayName);

			if (_icon != null)
			{
				_icon.sprite = weapon.Icon;
				_icon.SetActive(weapon.Icon != null);
			}

			int actionsCount = weapon.WeaponActions.SafeCount();
			for (int i = 0; i < _weaponActions.Length; i++)
			{
				var action = i < actionsCount ? weapon.WeaponActions[i] : null;

				if (action != null)
				{
					_weaponActions[i].text = action.Description;
				}

				_weaponActions[i].SetActive(action != null);
			}
		}
	}
}