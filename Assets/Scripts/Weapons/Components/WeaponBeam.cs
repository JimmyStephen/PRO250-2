using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class WeaponBeam : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		private float _maxDistance = 50f;
		[SerializeField]
		private float _beamRadius = 0.2f;
		[SerializeField, Tooltip("Number of raycast rays fired. First is always in center, other are spread around in the radius distance.")]
		private int _raycastAmount = 5;

		[Header("Beam Visuals")]
		[SerializeField]
		private GameObject _beamStart;
		[SerializeField]
		private GameObject _beamEnd;
		[SerializeField]
		private LineRenderer _beam;
		[SerializeField]
		private float _beamEndOffset = 0.5f;
		[SerializeField]
		private bool _updateBeamMaterial;
		[SerializeField]
		private float _textureScale = 3f;
		[SerializeField]
		private float _textureScrollSpeed = -8f;

		[Header("Camera Effect")]
		[SerializeField]
		private ShakeSetup _cameraShakePosition;
		[SerializeField]
		private ShakeSetup _cameraShakeRotation;

		[Networked]
		private float _beamDistance { get; set; }

		// WeaponComponent INTERFACE

		public override void ProcessInput(WeaponContext context, ref WeaponDesires desires, bool weaponBusy)
		{
			if (desires.Fire == true && desires.AmmoAvailable == true)
			{
				desires.HasFired = true;
			}
		}

		public override void OnFixedUpdate(WeaponContext context, WeaponDesires desires)
		{
			bool beamActive = desires.AmmoAvailable == true && context.Input.IsSet(EInputButtons.Fire);

			if (beamActive == false)
			{
				_beamDistance = 0f;
				return;
			}

			if (ProjectileUtility.CircleCast(Runner, Object.InputAuthority, context.FirePosition, context.FireDirection, _maxDistance, _beamRadius, _raycastAmount, _hitMask, out LagCompensatedHit hit) == true)
			{
				_beamDistance = hit.Distance;

				if (desires.HasFired == true)
				{
					HitUtility.ProcessHit(Object.InputAuthority, context.FireDirection, hit, _damage, _hitType);
				}
			}
			else
			{
				_beamDistance = _maxDistance;
			}
		}

		public override void OnRender(WeaponContext context, ref WeaponDesires desires)
		{
			UpdateBeam(context, _beamDistance);

			if (_beamDistance > 0f && Context.ObservedPlayerRef == Object.InputAuthority)
			{
				var cameraShake = Context.Camera.ShakeEffect;
				cameraShake.Play(_cameraShakePosition, EShakeForce.ReplaceSame);
				cameraShake.Play(_cameraShakeRotation, EShakeForce.ReplaceSame);
			}
		}

		// PRIVATE MEMBERS

		private void UpdateBeam(WeaponContext context, float distance)
		{
			bool beamActive = distance > 0f;

			_beamStart.SetActiveSafe(beamActive);
			_beamEnd.SetActiveSafe(beamActive);
			_beam.gameObject.SetActiveSafe(beamActive);

			if (beamActive == false)
				return;

			var startPosition = _beamStart.transform.position;
			var targetPosition = context.FirePosition + context.FireDirection * distance;

			var visualDirection = targetPosition - startPosition;
			float visualDistance = visualDirection.magnitude;

			visualDirection /= visualDistance; // Normalize

			if (_beamEndOffset > 0f)
			{
				// Adjust target position
				visualDistance = visualDistance > _beamEndOffset ? visualDistance - _beamEndOffset : 0f;
				targetPosition = startPosition + visualDirection * visualDistance;
			}

			_beamEnd.transform.SetPositionAndRotation(targetPosition, Quaternion.LookRotation(-visualDirection));

			_beam.SetPosition(0, startPosition);
			_beam.SetPosition(1, targetPosition);

			if (_updateBeamMaterial == true)
			{
				var beamMaterial = _beam.material;

				beamMaterial.mainTextureScale = new Vector2(visualDistance / _textureScale, 1f);
				beamMaterial.mainTextureOffset += new Vector2(Time.deltaTime * _textureScrollSpeed, 0f);
			}
		}
	}
}
