using DG.Tweening;
using Fusion;
using UnityEngine;

namespace Projectiles.UI
{
	[OrderBefore(typeof(HitboxManager))]
	public class SimpleMoveComponent : NetworkBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Vector3 _offset = new Vector3(0f, 0f, 10f);
		[SerializeField]
		private float _speed = 10f;
		[SerializeField]
		private Ease _ease = Ease.InOutSine;

		[Networked]
		private int _startTick { get; set; }
		[Networked]
		private Vector3 _startPosition { get; set; }
		[Networked]
		private Vector3 _targetPosition { get; set; }

		private float _distance;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			_startPosition = transform.position;
			_targetPosition = _startPosition + transform.rotation * _offset;
			_distance = _offset.magnitude;
		}

		public override void FixedUpdateNetwork()
		{
			UpdatePosition(Runner.Simulation.Tick, 0f);
		}

		public override void Render()
		{
			UpdatePosition(Runner.Simulation.Tick, Runner.Simulation.StateAlpha);
		}

		// PRIVATE METHODS

		private void UpdatePosition(int tick, float tickAlpha)
		{
			float elapsedTime = (tick - _startTick + tickAlpha) * Runner.DeltaTime;
			float totalDistance = _speed * elapsedTime;

			float currentDistance = totalDistance % (_distance * 2f);

			if (currentDistance > _distance)
			{
				// Returning
				float progress = (currentDistance - _distance) / _distance;
				transform.position = Vector3.Lerp(_targetPosition, _startPosition, DOVirtual.EasedValue(0f, 1f, progress, _ease));
			}
			else
			{
				float progress = currentDistance / _distance;
				transform.position = Vector3.Lerp(_startPosition, _targetPosition, DOVirtual.EasedValue(0f, 1f, progress, _ease));
			}
		}
	}
}