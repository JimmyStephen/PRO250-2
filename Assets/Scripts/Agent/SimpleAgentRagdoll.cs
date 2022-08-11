using Fusion;
using Fusion.KCC;
using UnityEngine;

namespace Projectiles
{
	[RequireComponent(typeof(Health), typeof(KCC), typeof(NetworkRigidbody))]
	public class SimpleAgentRagdoll : SimulationBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Collider _ragdollCollider;
		[SerializeField]
		private float _hitImpulse = 100f;

		private Health _health;
		private KCC _kcc;
		private NetworkRigidbody _networkRigidbody;

		private bool _ragdollEnabled = true;

		// SimulationBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			EnableRagdoll(_health.IsAlive == false);
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_health = GetComponent<Health>();
			_kcc = GetComponent<KCC>();
			_networkRigidbody = GetComponent<NetworkRigidbody>();

			EnableRagdoll(false, true);
		}

		protected void OnEnable()
		{
			_health.FatalHitTaken += OnFatalHit;
		}

		protected void OnDisable()
		{
			_health.FatalHitTaken -= OnFatalHit;
		}

		// PRIVATE METHODS

		private void OnFatalHit(HitData hit)
		{
			if (Runner.IsServer == false)
				return;

			EnableRagdoll(true);
			_networkRigidbody.Rigidbody.AddForceAtPosition(hit.Direction * _hitImpulse, hit.Position);
		}

		private void EnableRagdoll(bool value, bool force = false)
		{
			if (value == _ragdollEnabled && force == false)
				return;

			_networkRigidbody.enabled = value;

			// Clear NetworkRigidbody
			_networkRigidbody.InterpolationTarget.localPosition = Vector3.zero;
			_networkRigidbody.InterpolationTarget.localRotation = Quaternion.identity;

			if (_networkRigidbody.Object != null)
			{
				_networkRigidbody.TeleportToPositionRotation(transform.position, transform.rotation);
			}

			_ragdollCollider.SetActive(value);

			// Do not update KCC (IBeforeAllTicks etc.). We are also checking enabled flag when manually updating KCC from Agent
			_kcc.enabled = !value;

			if (_kcc.Collider != null)
			{
				_kcc.Collider.enabled = !value;
			}

			var rigidbody = _networkRigidbody.Rigidbody != null ? _networkRigidbody.Rigidbody : GetComponent<Rigidbody>();

			rigidbody.velocity = Vector3.zero;
			rigidbody.angularVelocity = Vector3.zero;

			// KCC is touching properties on the same rigidbody, so we need to reset them as well
			rigidbody.isKinematic = !value;
			rigidbody.useGravity = value;
			rigidbody.constraints = value == true ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeAll;

			_ragdollEnabled = value;
		}
	}
}
