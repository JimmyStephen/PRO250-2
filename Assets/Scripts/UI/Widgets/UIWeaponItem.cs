using TMPro;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIWeaponItem : UIListItemBase<UIWeapon>
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private TextMeshProUGUI _indexText;

		// MONOBEHAVIOUR

		protected void Start()
		{
			_indexText.text = Content.Slot.ToString();
		}
	}
}