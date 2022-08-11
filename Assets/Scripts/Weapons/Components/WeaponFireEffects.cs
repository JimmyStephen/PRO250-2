using System;
using UnityEngine;

namespace Projectiles
{
	public class WeaponFireEffects : WeaponComponent
	{
		// PRIVATE MEMBERS

		[Header("Muzzle")]
		[SerializeField]
		private GameObject _fireParticle;
		[SerializeField]
		private float _fireParticleReturnTime = 1f;

		[Header("Sound")]
		[SerializeField]
		private Transform _fireAudioEffectsRoot;
		[SerializeField]
		private AudioSetup _fireSound;

		[Header("Camera")]
		[SerializeField]
		private ShakeSetup _cameraShakePosition;
		[SerializeField]
		private ShakeSetup _cameraShakeRotation;

		[Header("Kickback")]
		[SerializeField]
		private Transform _kickbackTransform;
		[SerializeField]
		private Kickback _positionKickback = new Kickback(0.06f, 60f, 20f);
		[SerializeField]
		private Kickback _rotationKickback = new Kickback(5f, 30f, 20f);

		private Vector3 _kickbackInitialPosition;
		private Quaternion _kickbackInitialRotation;

		private AudioEffect[] _fireAudioEffects;

		// WeaponComponent INTERFACE

		public override void ProcessInput(WeaponContext context, ref WeaponDesires desires, bool weaponBusy)
		{
		}

		public override void OnFixedUpdate(WeaponContext context, WeaponDesires desires)
		{
		}

		public override void OnRender(WeaponContext context, ref WeaponDesires desires)
		{
			UpdateKickback(desires.HasFired);

			if (desires.HasFired == false)
				return;

			if (_fireParticle != null)
			{
				var fireParticle = Context.ObjectCache.Get(_fireParticle);
				Context.ObjectCache.ReturnDeferred(fireParticle, _fireParticleReturnTime);
				Runner.MoveToRunnerScene(fireParticle);

				if (fireParticle.gameObject.layer != Weapon.gameObject.layer)
				{
					fireParticle.SetLayer(Weapon.gameObject.layer, true);
				}

				fireParticle.transform.SetParent(BarrelTransform, false);
			}

			_fireAudioEffects.PlaySound(_fireSound, EForceBehaviour.ForceAny);

			if (Context.ObservedPlayerRef == Object.InputAuthority)
			{
				var cameraShake = Context.Camera.ShakeEffect;
				cameraShake.Play(_cameraShakePosition, EShakeForce.ReplaceSame);
				cameraShake.Play(_cameraShakeRotation, EShakeForce.ReplaceSame);
			}
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			if (_fireAudioEffectsRoot != null)
			{
				_fireAudioEffects = _fireAudioEffectsRoot.GetComponentsInChildren<AudioEffect>(true);
			}

			if (_kickbackTransform != null)
			{
				_kickbackInitialPosition = _kickbackTransform.localPosition;
				_kickbackInitialRotation = _kickbackTransform.localRotation;
			}
		}

		// PRIVATE METHODS

		private void UpdateKickback(bool hasFired)
		{
			if (_kickbackTransform == null)
				return;

			// Apply kickback only if active (other weapon actions might apply kickback on the same transform)
			if (_positionKickback.Current <= 0f && _rotationKickback.Current <= 0f && hasFired == false)
				return;

			var weaponTransform = Weapon.transform;

			_positionKickback.UpdateKickback(hasFired);
			_kickbackTransform.localPosition = _kickbackInitialPosition + new Vector3(0f, 0f, -_positionKickback.Current);

			_rotationKickback.UpdateKickback(hasFired, 0.1f);
			_kickbackTransform.localRotation = _kickbackInitialRotation;
			_kickbackTransform.RotateAround(weaponTransform.position, weaponTransform.right, -_rotationKickback.Current);
		}

		// HELPERS

		[Serializable]
		private class Kickback
		{
			// PUBLIC MEMBERS

			public float Current => _current;

			// PRIVATE MEMBERS

			[SerializeField]
			private float _fireKickback = 0.06f;
			[SerializeField]
			private float _speed = 60f;
			[SerializeField]
			private float _returnSpeed = 20f;

			private float _current;
			private float _target;

			// CONSTRUCTOR

			public Kickback(float fireKickback, float speed, float returnSpeed)
			{
				_fireKickback = fireKickback;
				_speed = speed;
				_returnSpeed = returnSpeed;
			}

			// PUBLIC METHODS

			public void UpdateKickback(bool hasFired, float zeroThreshold = 0.001f)
			{
				if (_speed <= 0f)
					return;

				if (hasFired == true)
				{
					_target += _fireKickback;
				}
				else if (_target > 0f)
				{
					_target = Mathf.Lerp(_target, 0f, Time.deltaTime * _returnSpeed);

					if (_target < zeroThreshold)
					{
						// Stop lerping
						_target = 0f;
					}
				}

				_current = Mathf.Lerp(_current, _target, Time.deltaTime * _speed);

				if (_current < zeroThreshold)
				{
					_current = 0f;
				}
			}
		}
	}
}
