using UnityEngine;

namespace Projectiles
{
	// Hitscan projectile with dummy flying bullet
	public class FlyingHitscanProjectile : HitscanProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _speed = 40f;

		private Vector3 _startPosition;
		private Vector3 _targetPosition;

		private float _time;
		private float _duration;

		// Projectile INTERFACE

		protected override void OnActivated(ProjectileContext context, ref ProjectileData data)
		{
			base.OnActivated(context, ref data);

			_startPosition = context.BarrelTransform.position;
			_targetPosition = data.ImpactPosition != Vector3.zero ? data.ImpactPosition : data.FirePosition + data.FireVelocity * _maxDistance;

			transform.position = _startPosition;
			transform.rotation = Quaternion.LookRotation(data.FireVelocity);

			_duration = Vector3.Magnitude(_targetPosition - _startPosition) / _speed;
			_time = 0f;
		}

		public override void OnRender(ProjectileContext context, ref ProjectileData data)
		{
			base.OnRender(context, ref data);

			_time += Time.deltaTime;

			float progress = _time / _duration;
			transform.position = Vector3.Lerp(_startPosition, _targetPosition, progress);

			if (_time >= _duration)
			{
				SpawnImpactVisual(context, ref data);

				transform.position = _targetPosition;
				IsFinished = true;
			}
		}
	}
}
