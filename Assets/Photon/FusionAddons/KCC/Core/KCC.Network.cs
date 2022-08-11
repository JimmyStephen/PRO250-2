namespace Fusion.KCC
{
	using System.Collections.Generic;
	using UnityEngine;

	public class KCCNetworkContext
	{
		public KCC         KCC;
		public KCCData     Data;
		public KCCSettings Settings;
	}

	public partial class KCC
	{
		// PRIVATE MEMBERS

		private KCCNetworkContext     _networkContext;
		private IKCCNetworkProperty[] _networkProperties;
		private float                 _positionReadAccuracy;
		private float                 _positionWriteAccuracy;
		private float                 _rotationReadAccuracy;
		private float                 _rotationWriteAccuracy;

		// NetworkAreaOfInterestBehaviour INTERFACE

		public override int PositionWordOffset => 0;

		// PRIVATE METHODS

		private int GetNetworkDataWordCount()
		{
			InitializeNetworkProperties();

			int wordCount = 0;

			for (int i = 0, count = _networkProperties.Length; i < count; ++i)
			{
				IKCCNetworkProperty property = _networkProperties[i];
				wordCount += property.WordCount;
			}

			return wordCount;
		}

		private unsafe void ReadNetworkData()
		{
			_networkContext.Data = _fixedData;

			int* ptr = Ptr;

			for (int i = 0, count = _networkProperties.Length; i < count; ++i)
			{
				IKCCNetworkProperty property = _networkProperties[i];
				property.Read(ptr);
				ptr += property.WordCount;
			}
		}

		private unsafe void WriteNetworkData()
		{
			_networkContext.Data = _fixedData;

			int* ptr = Ptr;

			for (int i = 0, count = _networkProperties.Length; i < count; ++i)
			{
				IKCCNetworkProperty property = _networkProperties[i];
				property.Write(ptr);
				ptr += property.WordCount;
			}
		}

		private unsafe void InterpolateNetworkData()
		{
			if (_driver != EKCCDriver.Fusion)
				return;
			if (GetInterpolationData(out InterpolationData interpolationData) == false)
				return;

			int   ticks = interpolationData.ToTick - interpolationData.FromTick;
			float tick  = interpolationData.FromTick + interpolationData.Alpha * ticks;

			// Store base Ptr for later use.

			int* basePtrFrom = interpolationData.From;
			int* basePtrTo   = interpolationData.To;

			// We start with fixed data which has a state from server or interpolated state from last frame.

			_networkContext.Data = _fixedData;

			// Set general properties.

			_fixedData.Frame             = Time.frameCount;
			_fixedData.Tick              = Mathf.RoundToInt(tick);
			_fixedData.Alpha             = interpolationData.Alpha;
			_fixedData.DeltaTime         = Runner.DeltaTime;
			_fixedData.UnscaledDeltaTime = _fixedData.DeltaTime;
			_fixedData.Time              = tick * _fixedData.DeltaTime;

			// Interpolate all networked properties.

			for (int i = 0, count = _networkProperties.Length; i < count; ++i)
			{
				IKCCNetworkProperty property = _networkProperties[i];
				property.Interpolate(interpolationData);
				interpolationData.From += property.WordCount;
				interpolationData.To   += property.WordCount;
			}

			// Teleport detection.

			if (ticks > 0)
			{
				Vector3 fromPosition = KCCNetworkUtility.ReadVector3(basePtrFrom, _positionReadAccuracy);
				Vector3 toPosition   = KCCNetworkUtility.ReadVector3(basePtrTo,   _positionReadAccuracy);

				Vector3 positionDifference = toPosition - fromPosition;
				if (positionDifference.sqrMagnitude > ticks * ticks)
				{
					_fixedData.TargetPosition = toPosition;
					_fixedData.RealVelocity   = Vector3.zero;
					_fixedData.RealSpeed      = 0.0f;
				}
				else
				{
					_fixedData.RealVelocity = positionDifference / (_fixedData.DeltaTime * ticks);
					_fixedData.RealSpeed    = _fixedData.RealVelocity.magnitude;
				}
			}

			// User interpolation and post-processing.

			InterpolateUserNetworkData(interpolationData);

			// Flipping fixed data to render data.

			_renderData.CopyFromOther(_fixedData);
		}

		private void RestoreHistoryData(KCCData historyData)
		{
			// Simulate position and rotation quantization on locally stored history data (full precision).
			// If the value equals to fixed data (state received from server), we can restore full precision values from history.

			Vector3 quantizedPosition = GetQuantizedPosition(historyData.TargetPosition);
			if (_fixedData.TargetPosition.IsEqual(quantizedPosition) == true)
			{
				_fixedData.BasePosition    = historyData.BasePosition;
				_fixedData.DesiredPosition = historyData.DesiredPosition;
				_fixedData.TargetPosition  = historyData.TargetPosition;
			}

			float quantizedPitch = GetQuantizedRotation(historyData.LookPitch);
			if (_fixedData.LookPitch == quantizedPitch)
			{
				_fixedData.LookPitch = historyData.LookPitch;
			}

			float quantizedYaw = GetQuantizedRotation(historyData.LookYaw);
			if (_fixedData.LookYaw == quantizedYaw)
			{
				_fixedData.LookYaw = historyData.LookYaw;
			}

			// Some values can be synchronized from user code.
			// We have to ensure these properties are in correct state with other properties.

			if (_fixedData.IsGrounded == true)
			{
				// Reset IsGrounded to history state, otherwise using GroundNormal and other ground related properties leads to undefined behavior and NaN propagation.
				// This has effect only if IsGrounded is synchronized over network.
				_fixedData.IsGrounded = historyData.IsGrounded;
			}

			// User history data restoration.

			RestoreUserHistoryData(historyData);
		}

		partial void InitializeUserNetworkProperties(KCCNetworkContext networkContext, List<IKCCNetworkProperty> networkProperties);
		partial void InterpolateUserNetworkData(InterpolationData interpolationData);
		partial void RestoreUserHistoryData(KCCData historyData);

		// PRIVATE METHODS

		private void InitializeNetworkProperties()
		{
			if (_networkContext != null)
				return;

			_networkContext = new KCCNetworkContext();
			_networkContext.KCC      = this;
			_networkContext.Settings = _settings;

			_positionReadAccuracy  = new Accuracy(AccuracyDefaults.POSITION).Value;
			_positionWriteAccuracy = _positionReadAccuracy > 0.0f ? 1.0f / _positionReadAccuracy : 0.0f;
			_rotationReadAccuracy  = new Accuracy(AccuracyDefaults.ROTATION).Value;
			_rotationWriteAccuracy = _rotationReadAccuracy > 0.0f ? 1.0f / _rotationReadAccuracy : 0.0f;

			List<IKCCNetworkProperty> properties = new List<IKCCNetworkProperty>(32)
			{
				new KCCNetworkProperties(_networkContext, _positionReadAccuracy, _rotationReadAccuracy),
				new KCCNetworkCollisions(_networkContext, 8),
				new KCCNetworkModifiers (_networkContext, 8),
				new KCCNetworkIgnores   (_networkContext, 8),
			};

			InitializeUserNetworkProperties(_networkContext, properties);

			_networkProperties = properties.ToArray();
		}

		private Vector3 GetQuantizedPosition(Vector3 position)
		{
			if (_positionReadAccuracy <= 0.0f || _positionWriteAccuracy <= 0.0f)
				return position;

			Vector3 quantizedPosition;

			quantizedPosition.x = (position.x < 0.0f ? (int)((position.x * _positionWriteAccuracy) - 0.5f) : (int)((position.x * _positionWriteAccuracy) + 0.5f)) * _positionReadAccuracy;
			quantizedPosition.y = (position.y < 0.0f ? (int)((position.y * _positionWriteAccuracy) - 0.5f) : (int)((position.y * _positionWriteAccuracy) + 0.5f)) * _positionReadAccuracy;
			quantizedPosition.z = (position.z < 0.0f ? (int)((position.z * _positionWriteAccuracy) - 0.5f) : (int)((position.z * _positionWriteAccuracy) + 0.5f)) * _positionReadAccuracy;

			return quantizedPosition;
		}

		private float GetQuantizedRotation(float angle)
		{
			if (_rotationReadAccuracy <= 0.0f || _rotationWriteAccuracy <= 0.0f)
				return angle;

			return (angle < 0.0f ? (int)((angle * _rotationWriteAccuracy) - 0.5f) : (int)((angle * _rotationWriteAccuracy) + 0.5f)) * _rotationReadAccuracy;
		}
	}
}
