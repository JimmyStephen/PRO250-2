using Fusion;
using UnityEngine;
using DG.Tweening;

namespace Projectiles
{
	public abstract class KinematicProjectile : Projectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		protected float _startSpeed = 40f;
		[SerializeField, Tooltip("Projectile length improves hitting moving targets")]
		protected float _length = 0f;
		[SerializeField]
		private float _maxDistance = 200f;
		[SerializeField]
		private float _maxTime = 5f;
		[SerializeField, Tooltip("Time for interpolation between barrel position and actual fire path of the projectile")]
		private float _interpolationDuration = 0.3f;
		[SerializeField]
		private Ease _interpolationEase = Ease.OutSine;

		private Vector3 _startOffset;
		private float _interpolationTime;

		private int _maxLiveTimeTicks = -1;

		// Projectile INTERFACE

		public override ProjectileData GetFireData(NetworkRunner runner, Vector3 firePosition, Vector3 fireDirection)
		{
			if (_maxLiveTimeTicks < 0)
			{
				int maxDistanceTicks = Mathf.RoundToInt((_maxDistance / _startSpeed) * runner.Simulation.Config.TickRate);
				int maxTimeTicks = Mathf.RoundToInt(_maxTime * runner.Simulation.Config.TickRate);

				// GetFireData is called on prefab directly, but it is safe to save
				// the value here as it does not change for different instances
				_maxLiveTimeTicks = maxDistanceTicks > 0 && maxTimeTicks > 0 ? Mathf.Min(maxDistanceTicks, maxTimeTicks) : (maxDistanceTicks > 0 ? maxDistanceTicks : maxTimeTicks);
			}

			return new ProjectileData()
			{
				FirePosition = firePosition,
				FireVelocity = fireDirection * _startSpeed,
			};
		}

		public override void OnFixedUpdate(ProjectileContext context, ref ProjectileData data)
		{
			if (context.Runner.Simulation.Tick >= data.FireTick + _maxLiveTimeTicks)
			{
				data.IsFinished = true;
			}
		}

		protected override void OnActivated(ProjectileContext context, ref ProjectileData data)
		{
			base.OnActivated(context, ref data);

			transform.position = context.BarrelTransform.position;
			transform.rotation = Quaternion.LookRotation(data.FireVelocity);

			_startOffset = context.BarrelTransform.position - data.FirePosition;
			_interpolationTime = 0f;
		}

		public override void OnRender(ProjectileContext context, ref ProjectileData data)
		{
			if (data.IsFinished == true)
			{
				SpawnImpactVisual(context, ref data);
				IsFinished = true;
				return;
			}

			var targetPosition = GetRenderPosition(context, ref data);
			float interpolationProgress = 0f;

			if (targetPosition != data.FirePosition)
			{
				// Do not start interpolation until projectile should actually move
				_interpolationTime += Time.deltaTime;
				interpolationProgress = Mathf.Clamp01(_interpolationTime / _interpolationDuration);
			}

			interpolationProgress = DOVirtual.EasedValue(0f, 1f, interpolationProgress, _interpolationEase);
			var offset = Vector3.Lerp(_startOffset, Vector3.zero, interpolationProgress);

			var previousPosition = transform.position;
			var nextPosition = targetPosition + offset;
			var direction = nextPosition - previousPosition;

			transform.position = nextPosition;

			if (direction != Vector3.zero)
			{
				transform.rotation = Quaternion.LookRotation(direction);
			}
		}

		// PROTECTED METHODS

		protected abstract Vector3 GetRenderPosition(ProjectileContext context, ref ProjectileData data);
	}
}
