using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class HomingProjectile : KinematicProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHomingPosition _homingPosition = EHomingPosition.Body;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		private float _maxSeekDistance = 50f;
		[SerializeField, Tooltip("Specifies max angle between projectile forward and direction to target. "
		                         + "If exceeded, projectile will continue forward or look for other targets.")]
		private float _minDot = 0.7f; // TODO: Change to angle
		[SerializeField]
		private float _distanceWeight = 1f;
		[SerializeField]
		private float _angleWeight = 1f;
		[SerializeField]
		private float _turnSpeed = 8f;
		[SerializeField, Tooltip("0 = Never recalculate")]
		private float _recalculateTargetAfterTime = 0f;
		[SerializeField, Range(0f, 1f)]
		private float _predictTargetPosition = 0f;

		// KinematicProjectile INTERFACE

		public override bool NeedsInterpolationData => true;

		public override ProjectileData GetFireData(NetworkRunner runner, Vector3 firePosition, Vector3 fireDirection)
		{
			var data = base.GetFireData(runner, firePosition, fireDirection);

			data.Homing.Target = FindTarget(runner, firePosition, fireDirection);
			data.Homing.Direction = fireDirection;
			data.Homing.Position = firePosition;

			return data;
		}

		public override void OnFixedUpdate(ProjectileContext context, ref ProjectileData data)
		{
			var previousPosition = data.Homing.Position;
			var nextPosition = data.Homing.Position + data.Homing.Direction * _startSpeed * context.Runner.DeltaTime;

			var direction = nextPosition - previousPosition;
			float distance = direction.magnitude;

			// Normalize
			direction /= distance;

			if (ProjectileUtility.ProjectileCast(context.Runner, context.InputAuthority, previousPosition, direction, distance, _hitMask, out LagCompensatedHit hit) == true)
			{
				HitUtility.ProcessHit(context.InputAuthority, direction, hit, _damage, _hitType);

				SpawnImpact(context, ref data, hit.Point, (hit.Normal + -direction) * 0.5f);

				data.Homing.Position = hit.Point;
				data.IsFinished = true;
			}
			else
			{
				TryRecalculateTarget(context.Runner, ref data, nextPosition, direction);
				UpdateDirection(context.Runner, ref data);

				data.Homing.Position = nextPosition;
			}

			base.OnFixedUpdate(context, ref data);
		}

		protected override Vector3 GetRenderPosition(ProjectileContext context, ref ProjectileData data)
		{
			if (context.Interpolate == true)
			{
				var from = context.InterpolationData.From.Homing.Position;
				var to = context.InterpolationData.To.Homing.Position;
				return Vector3.Lerp(from, to, context.InterpolationData.Alpha);
			}

			float deltaFromLastFrame = (context.FloatTick % 1) * context.Runner.DeltaTime;
			return data.Homing.Position + data.Homing.Direction * _startSpeed * deltaFromLastFrame;
		}

		// PRIVATE MEMBERS

		private NetworkId FindTarget(NetworkRunner runner, Vector3 firePosition, Vector3 fireDirection)
		{
			var targets = ListPool.Get<IHitTarget>(64);

			HitUtility.GetAllTargets(runner, targets);

			float bestValue = float.MinValue;
			IHitTarget bestTarget = default;

			float maxSqrDistance = _maxSeekDistance * _maxSeekDistance;

			for (int i = 0; i < targets.Count; i++)
			{
				var target = targets[i];

				Vector3 targetPosition = GetTargetPosition(target);

				var direction = targetPosition - firePosition;
				if (direction.sqrMagnitude > maxSqrDistance)
					continue;

				float distance = direction.magnitude;
				direction /= distance; // Normalize

				float dot = Vector3.Dot(fireDirection, direction);

				if (dot < _minDot)
					continue;

				float value = dot * 90f * _angleWeight + distance * -_distanceWeight;

				if (value > bestValue)
				{
					bestValue = value;
					bestTarget = target;
				}
			}

			ListPool.Return(targets);

			return bestTarget is NetworkBehaviour behaviour ? behaviour.Object.Id : default;
		}

		private void TryRecalculateTarget(NetworkRunner runner, ref ProjectileData data, Vector3 position, Vector3 direction)
		{
			if (_recalculateTargetAfterTime <= 0f)
				return;

			int recalculateTicks = Mathf.RoundToInt(_recalculateTargetAfterTime * runner.Simulation.Config.TickRate);
			int elapsedTicks = runner.Simulation.Tick - data.FireTick;

			if (elapsedTicks % recalculateTicks == 0)
			{
				data.Homing.Target = FindTarget(runner, position, direction);
			}
		}

		private void UpdateDirection(NetworkRunner runner, ref ProjectileData data)
		{
			var targetObject = data.Homing.Target.IsValid == true ? runner.FindObject(data.Homing.Target) : null;
			if (targetObject == null)
				return; // No target, continue in current direction

			var targetPosition = GetTargetPosition(targetObject.GetComponent<IHitTarget>());

			var newDirection = (targetPosition - data.Homing.Position);
			float distance = newDirection.magnitude;

			newDirection /= distance; // Normalize

			if (Vector3.Dot(data.Homing.Direction, newDirection) < _minDot)
			{
				// Loose target
				data.Homing.Target = default;
				data.Homing.TargetPosition = default;
				return;
			}

			if (_predictTargetPosition > 0f)
			{
				var previousTargetPosition = data.Homing.TargetPosition;
				data.Homing.TargetPosition = targetPosition;

				if (previousTargetPosition != Vector3.zero)
				{
					var targetVelocity = (targetPosition - previousTargetPosition) * runner.Simulation.Config.TickRate;
					float timeToTarget = distance / _startSpeed;

					var predictedTargetPosition = targetPosition + (targetVelocity * timeToTarget * _predictTargetPosition);
					newDirection = (predictedTargetPosition - data.Homing.Position).normalized;
				}
			}

			float deltaTime = runner.Simulation.DeltaTime;
			data.Homing.Direction = (data.Homing.Direction + newDirection * deltaTime * _turnSpeed).normalized;
		}

		private Vector3 GetTargetPosition(IHitTarget target)
		{
			switch (_homingPosition)
			{
				case EHomingPosition.Head:
					return target.HeadPivot.position;
				case EHomingPosition.Ground:
					return target.GroundPivot.position;
				default:
					return target.BodyPivot.position;
			}
		}

		// HELPERS

		public enum EHomingPosition
		{
			Body = 1,
			Head,
			Ground,
		}
	}
}
