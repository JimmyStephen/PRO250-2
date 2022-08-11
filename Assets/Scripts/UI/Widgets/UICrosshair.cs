using DG.Tweening;
using UnityEngine;

namespace Projectiles.UI
{
	public class UICrosshair : UIWidget
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private CanvasGroup _hitPerformedGroup;
		[SerializeField]
		private AudioSetup _hitPerformedSound;
		[SerializeField]
		private float _hitGroupDelay = 0.15f;
		[SerializeField]
		private float _hitGroupFadeInDuration = 0.1f;
		[SerializeField]
		private float _hitGroupFadeOutDuration = 0.8f;

		private int _lastSoundFrame;

		// PUBLIC METHODS

		public void HitPerformed(HitData hitData)
		{
			PlayEffect(_hitPerformedSound);

			DOTween.Kill(_hitPerformedGroup);

			_hitPerformedGroup.DOFade(1f, _hitGroupFadeInDuration).SetDelay(_hitGroupDelay);
			_hitPerformedGroup.DOFade(0f, _hitGroupFadeOutDuration).SetDelay(_hitGroupDelay + _hitGroupFadeInDuration + 0.1f);
		}

		// PRIVATE MEMBERS

		private void PlayEffect(AudioSetup setup)
		{
			if (Time.frameCount == _lastSoundFrame)
				return; // Play only one sound per frame

			SceneUI.PlaySound(setup);
			_lastSoundFrame = Time.frameCount;
		}
	}
}

