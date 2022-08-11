using DG.Tweening;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIScreenEffects : UIWidget
	{
		// PRIVATE METHODS

		[SerializeField]
		private CanvasGroup _hitGroup;
		[SerializeField]
		private UIBehaviour _deathGroup;

		[Header("Animation")]
		[SerializeField]
		private float _hitFadeInDuratio = 0.1f;
		[SerializeField]
		private float _hitFadeOutDuration = 0.7f;

		[Header("Audio")]
		[SerializeField]
		private AudioSetup _hitSound;
		[SerializeField]
		private AudioSetup _deathSound;

		// PUBLIC METHODS

		public void OnHitTaken(HitData hit)
		{
			if (hit.Amount <= 0)
				return;

			if (hit.Action == EHitAction.Damage)
			{
				float alpha = Mathf.Lerp(0, 1f, hit.Amount / 20f);

				ShowHit(_hitGroup, alpha);
				PlaySound(_hitSound, EForceBehaviour.ForceAny);

				if (hit.IsFatal == true)
				{
					_deathGroup.SetActive(true);
					PlaySound(_deathSound, EForceBehaviour.ForceAny);
				}
			}
		}

		public void UpdateEffects(PlayerAgent agent)
		{
			_deathGroup.SetActive(agent.Health.IsAlive == false);
		}

		// MONOBEHAVIOUR

		protected override void OnVisible()
		{
			base.OnVisible();

			_hitGroup.SetActive(true);
			_hitGroup.alpha = 0f;

			_deathGroup.SetActive(false);
		}

		// PRIVATE METHODS

		private void ShowHit(CanvasGroup group, float targetAlpha)
		{
			DOTween.Kill(group);

			group.DOFade(targetAlpha, _hitFadeInDuratio);
			group.DOFade(0f, _hitFadeOutDuration).SetDelay(_hitFadeInDuratio);
		}
	}
}
