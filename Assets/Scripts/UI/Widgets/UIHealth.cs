using UnityEngine;

namespace Projectiles.UI
{
	public class UIHealth : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private UIValue _healthValue;

		// PUBLIC METHODS

		public void UpdateHealth(Health health)
		{
			_healthValue.SetValue(health.CurrentHealth);
		}
	}
}