using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class AdvancedKinematicProjectile : KinematicProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float            _damage = 10f;
		[SerializeField]
		private EHitType         _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask        _hitMask;
		[SerializeField]
		private float            _gravity = 20f;
		[SerializeField]
		private bool             _spawnImpactObjectOnTimeout;

		[Header("Bounce")]
		[SerializeField]
		private bool             _canBounce = false;
		[SerializeField]
		private LayerMask        _bounceMask;
		[SerializeField]
		private float            _bounceObjectRadius = 0.1f;
		[SerializeField]
		private float            _bounceVelocityMultiplierStart = 0.5f;
		[SerializeField]
		private float            _bounceVelocityMultiplierEnd = 0.8f;
		[SerializeField, Tooltip("Number of bounces between velocity multiplier start and end")]
		private int              _bounceVelocityScale = 8;
		[SerializeField]
		private float            _stopSpeed = 2f;
		[SerializeField]
		private AudioEffect      _bounceSound;

		private float _maxBounceVolume;
		private int _visibleBounceCount;

		// KinematicProjectile INTERFACE

		public override void OnFixedUpdate(ProjectileContext context, ref ProjectileData data)
		{
			base.OnFixedUpdate(context, ref data);

			if (data.IsFinished == true && _spawnImpactObjectOnTimeout == true)
			{
				var position = data.Kinematic.HasStopped == true ? data.Kinematic.FinishedPosition : GetMovePosition(context.Runner, ref data, context.Runner.Simulation.Tick);
				SpawnImpact(context, ref data, position, Vector3.up);
			}

			if (data.IsFinished == true || data.Kinematic.HasStopped == true)
				return;

			var previousPosition = GetMovePosition(context.Runner, ref data, context.Runner.Simulation.Tick - 1);
			var nextPosition = GetMovePosition(context.Runner, ref data, context.Runner.Simulation.Tick);

			var direction = nextPosition - previousPosition;
			float distance = direction.magnitude;

			if (distance <= 0f)
				return;

			// Normalize
			direction /= distance;

			if (_length > 0f)
			{
				float elapsedDistanceSqr = (previousPosition - data.FirePosition).sqrMagnitude;
				float projectileLength = elapsedDistanceSqr > _length * _length ? _length : Mathf.Sqrt(elapsedDistanceSqr);

				previousPosition -= direction * projectileLength;
				distance += projectileLength;
			}

			if (ProjectileUtility.ProjectileCast(context.Runner, context.InputAuthority, previousPosition - direction * _bounceObjectRadius, direction, distance + 2 * _bounceObjectRadius, _hitMask, out LagCompensatedHit hit) == true)
			{
				bool doBounce = _canBounce;

				if (_canBounce == true && hit.GameObject != null)
				{
					// Check bounce layer
					int hitLayer = hit.GameObject.layer;
					doBounce = ((1 << hitLayer) & _bounceMask) != 0;
				}

				if (doBounce == true)
				{
					ProcessBounce(context, ref data, hit, direction, distance);
				}
				else
				{
					HitUtility.ProcessHit(context.InputAuthority, direction, hit, _damage, _hitType);
					SpawnImpact(context, ref data, hit.Point, (hit.Normal + -direction) * 0.5f);

					data.IsFinished = true;
				}
			}
		}

		protected override void OnActivated(ProjectileContext context, ref ProjectileData data)
		{
			base.OnActivated(context, ref data);

			// Sync visible bounces
			_visibleBounceCount = _canBounce == true ? data.Kinematic.BounceCount : 0;
		}

		public override void OnRender(ProjectileContext context, ref ProjectileData data)
		{
			base.OnRender(context, ref data);

			if (_canBounce == true && data.Kinematic.BounceCount != _visibleBounceCount)
			{
				OnBounceRender(ref data);
				_visibleBounceCount = data.Kinematic.BounceCount;
			}
		}

		protected override Vector3 GetRenderPosition(ProjectileContext context, ref ProjectileData data)
		{
			var moveData = data;
			if (context.Interpolate == true)
			{
				var interpolationData = context.InterpolationData;

				// Choose correct interpolation data (matters mainly for bouncing as values are changing after bounce)
				moveData = context.FloatTick < interpolationData.To.Kinematic.StartTick ? interpolationData.From : interpolationData.To;
			}

			// If projectile has stopped return finished position but not until we are at the stop time (StartTick acts as stop tick here)
			if (moveData.Kinematic.HasStopped == true && moveData.Kinematic.StartTick <= context.FloatTick)
				return moveData.Kinematic.FinishedPosition;

			return GetMovePosition(context.Runner, ref moveData, context.FloatTick);
		}

		// MONOBEHAVIOUR

		protected override void Awake()
		{
			base.Awake();

			_maxBounceVolume = _bounceSound != null ? _bounceSound.DefaultSetup.Volume : 0f;
		}

		// PRIVATE METHODS

		private Vector3 GetMovePosition(NetworkRunner runner, ref ProjectileData data, float currentTick)
		{
			int startTick = data.Kinematic.StartTick > 0 ? data.Kinematic.StartTick : data.FireTick;
			float time = (currentTick - startTick) * runner.DeltaTime;

			if (time <= 0f)
				return data.FirePosition;

			return data.FirePosition + data.FireVelocity * time + new Vector3(0f, -_gravity, 0f) * time * time * 0.5f;
		}

		private void ProcessBounce(ProjectileContext context, ref ProjectileData data, LagCompensatedHit hit, Vector3 direction, float distance)
		{
			var reflectedDirection = Vector3.Reflect(direction, hit.Normal);

			// Stop bouncing when the velocity is small enough
			if (distance < _stopSpeed * context.Runner.DeltaTime)
			{
				// Stop the projectile but do not destroy it yet (wait for timeout)
				data.Kinematic.HasStopped = true;
				data.Kinematic.StartTick = context.Runner.Simulation.Tick;

				data.Kinematic.FinishedPosition = hit.Point + Vector3.Project(hit.Normal * _bounceObjectRadius, reflectedDirection);
				return;
			}

			float bounceMultiplier = _bounceVelocityMultiplierStart;

			if (_bounceVelocityMultiplierStart != _bounceVelocityMultiplierEnd)
			{
				bounceMultiplier = Mathf.Lerp(_bounceVelocityMultiplierStart, _bounceVelocityMultiplierEnd, data.Kinematic.BounceCount / (float)_bounceVelocityScale);
			}

			float distanceToHit = Vector3.Distance(hit.Point, transform.position);
			float progressToHit = distanceToHit / distance;

			data.FirePosition = hit.Point + reflectedDirection * _bounceObjectRadius;
			data.FireVelocity = reflectedDirection * data.FireVelocity.magnitude * bounceMultiplier;

			// Simple trick to better align position with ticks. More precise solution would be to remember
			// alpha between ticks (when the bounce happened) but it is good enough here.
			data.Kinematic.StartTick = progressToHit > 0.5f ? context.Runner.Simulation.Tick : context.Runner.Simulation.Tick - 1;

			data.Kinematic.BounceCount++;
		}

		private void OnBounceRender(ref ProjectileData data)
		{
			if (_bounceSound == null)
				return;

			var soundSetup = _bounceSound.DefaultSetup;
			soundSetup.Volume = Mathf.Lerp(0f, _maxBounceVolume, data.FireVelocity.magnitude / 10f);

			_bounceSound.Play(soundSetup, EForceBehaviour.ForceAny);
		}
	}
}
