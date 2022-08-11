using UnityEngine;

namespace Projectiles
{
	[RequireComponent(typeof(Health))]
	public class HitReactions : MonoBehaviour
	{
		// PRIVATE MEMBERSa

		[Header("Animation")]
		[SerializeField]
		private Animation _animation;
		[SerializeField]
		private AnimationClip _hitClip;
		[SerializeField]
		private AnimationClip _fatalHitClip;

		private Health _health;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_health = GetComponent<Health>();
		}

		protected void OnEnable()
		{
			_health.HitTaken += OnHitTaken;
		}

		protected void OnDisable()
		{
			_health.HitTaken -= OnHitTaken;
		}

		// PRIVATE MEMBERS

		private void OnHitTaken(HitData hitData)
		{
			if (_animation != null)
			{
				_animation.PlayForward(hitData.IsFatal == true ? _fatalHitClip : _hitClip);
			}
		}
	}
}