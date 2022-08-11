namespace Fusion.KCC
{
	using System.Runtime.CompilerServices;
	using UnityEngine;

	public sealed unsafe class KCCNetworkProperties : KCCNetworkProperty<KCCNetworkContext>
	{
		// CONSTANTS

		private const int WORD_COUNT = 12;

		// PRIVATE MEMBERS

		private readonly float _positionReadAccuracy;
		private readonly float _positionWriteAccuracy;
		private readonly float _rotationReadAccuracy;
		private readonly float _rotationWriteAccuracy;

		// CONSTRUCTORS

		public KCCNetworkProperties(KCCNetworkContext context, float positionReadAccuracy, float rotationReadAccuracy) : base(context, WORD_COUNT)
		{
			_positionReadAccuracy  = positionReadAccuracy > 0.0f ? positionReadAccuracy        : 0.0f;
			_positionWriteAccuracy = positionReadAccuracy > 0.0f ? 1.0f / positionReadAccuracy : 0.0f;

			_rotationReadAccuracy  = rotationReadAccuracy > 0.0f ? rotationReadAccuracy        : 0.0f;
			_rotationWriteAccuracy = rotationReadAccuracy > 0.0f ? 1.0f / rotationReadAccuracy : 0.0f;
		}

		// KCCNetworkProperty INTERFACE

		public override void Read(int* ptr)
		{
			KCCData     data     = Context.Data;
			KCCSettings settings = Context.Settings;

			data.TargetPosition = ReadVector3(_positionReadAccuracy, ref ptr);
			data.LookPitch      = ReadFloat(_rotationReadAccuracy, ref ptr);
			data.LookYaw        = ReadFloat(_rotationReadAccuracy, ref ptr);

			int combinedSettings1 = *ptr; ++ptr;
			settings.Shape         = (EKCCShape)(combinedSettings1 & 0b11);
			settings.IsTrigger     = (combinedSettings1 & 0b100) == 0b100;
			settings.ColliderLayer = (combinedSettings1 & 0b11111000) >> 3;

			settings.CollisionLayerMask = *ptr; ++ptr;

			int combinedSettings2 = *ptr; ++ptr;
			settings.RenderBehavior = (EKCCRenderBehavior)(combinedSettings2 & 0b11);
			settings.Features       = (EKCCFeatures)((combinedSettings2 & 0b11111100) >> 2);

			settings.Radius = ReadFloat(0.0f, ref ptr);
			settings.Height = ReadFloat(0.0f, ref ptr);
			settings.Extent = ReadFloat(0.0f, ref ptr);
			settings.Mass   = ReadFloat(0.0f, ref ptr);
		}

		public override void Write(int* ptr)
		{
			KCCData     data     = Context.Data;
			KCCSettings settings = Context.Settings;

			WriteVector3(data.TargetPosition, _positionWriteAccuracy, ref ptr);
			WriteFloat(data.LookPitch, _rotationWriteAccuracy, ref ptr);
			WriteFloat(data.LookYaw, _rotationWriteAccuracy, ref ptr);

			int combinedSettings1 = (int)settings.Shape & 0b11;
			combinedSettings1 |= settings.IsTrigger == true ? 0b100 : 0;
			combinedSettings1 |= (settings.ColliderLayer << 3) & 0b11111000;
			*ptr = combinedSettings1; ++ptr;

			*ptr = settings.CollisionLayerMask; ++ptr;

			int combinedSettings2 = (int)settings.RenderBehavior & 0b11;
			combinedSettings2 |= ((int)settings.Features << 2) & 0b11111100;
			*ptr = combinedSettings2; ++ptr;

			WriteFloat(settings.Radius, 0.0f, ref ptr);
			WriteFloat(settings.Height, 0.0f, ref ptr);
			WriteFloat(settings.Extent, 0.0f, ref ptr);
			WriteFloat(settings.Mass, 0.0f, ref ptr);
		}

		public override void Interpolate(InterpolationData interpolationData)
		{
			KCCData     data     = Context.Data;
			KCCSettings settings = Context.Settings;

			int* ptrFrom = interpolationData.From;
			int* ptrTo   = interpolationData.To;

			Vector3 targetPositionFrom = ReadVector3(_positionReadAccuracy, ref ptrFrom);
			Vector3 targetPositionTo   = ReadVector3(_positionReadAccuracy, ref ptrTo);
			data.TargetPosition = Vector3.Lerp(targetPositionFrom, targetPositionTo, interpolationData.Alpha);

			float lookPitchFrom = ReadFloat(_rotationReadAccuracy, ref ptrFrom);
			float lookPitchTo   = ReadFloat(_rotationReadAccuracy, ref ptrTo);
			data.LookPitch = Mathf.Lerp(lookPitchFrom, lookPitchTo, interpolationData.Alpha);

			float lookYawFrom = ReadFloat(_rotationReadAccuracy, ref ptrFrom);
			float lookYawTo   = ReadFloat(_rotationReadAccuracy, ref ptrTo);
			data.LookYaw = KCCMathUtility.InterpolateRange(lookYawFrom, lookYawTo, -180.0f, 180.0f, interpolationData.Alpha);

			int* ptrAlpha = interpolationData.Alpha < 0.5f ? ptrFrom : ptrTo;

			int combinedSettings1 = *ptrAlpha; ++ptrAlpha;
			settings.Shape         = (EKCCShape)(combinedSettings1 & 0b11);
			settings.IsTrigger     = (combinedSettings1 & 0b100) == 0b100;
			settings.ColliderLayer = (combinedSettings1 & 0b11111000) >> 3;

			settings.CollisionLayerMask = *ptrAlpha; ++ptrAlpha;

			int combinedSettings2 = *ptrAlpha; ++ptrAlpha;
			settings.RenderBehavior = (EKCCRenderBehavior)(combinedSettings2 & 0b11);
			settings.Features       = (EKCCFeatures)((combinedSettings2 & 0b11111100) >> 2);

			settings.Radius = ReadFloat(0.0f, ref ptrAlpha);
			settings.Height = ReadFloat(0.0f, ref ptrAlpha);
			settings.Extent = ReadFloat(0.0f, ref ptrAlpha);
			settings.Mass   = ReadFloat(0.0f, ref ptrAlpha);

			// ptrFrom += 7;
			// ptrTo   += 7;
		}

		// PRIVATE METHODS

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float ReadFloat(float accuracy, ref int* ptr)
		{
			float value;

			if (accuracy <= 0.0f)
			{
				value = *(float*)ptr; ++ptr;
			}
			else
			{
				value = (*ptr) * accuracy; ++ptr;
			}

			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteFloat(float value, float accuracy, ref int* ptr)
		{
			if (accuracy <= 0.0f)
			{
				*(float*)ptr = value; ++ptr;
			}
			else
			{
				*ptr = value < 0.0f ? (int)((value * accuracy) - 0.5f) : (int)((value * accuracy) + 0.5f); ++ptr;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector3 ReadVector3(float accuracy, ref int* ptr)
		{
			Vector3 value;

			if (accuracy <= 0.0f)
			{
				value.x = *(float*)ptr; ++ptr;
				value.y = *(float*)ptr; ++ptr;
				value.z = *(float*)ptr; ++ptr;
			}
			else
			{
				value.x = (*ptr) * accuracy; ++ptr;
				value.y = (*ptr) * accuracy; ++ptr;
				value.z = (*ptr) * accuracy; ++ptr;
			}

			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteVector3(Vector3 value, float accuracy, ref int* ptr)
		{
			if (accuracy <= 0.0f)
			{
				*(float*)ptr = value.x; ++ptr;
				*(float*)ptr = value.y; ++ptr;
				*(float*)ptr = value.z; ++ptr;
			}
			else
			{
				*ptr = value.x < 0.0f ? (int)((value.x * accuracy) - 0.5f) : (int)((value.x * accuracy) + 0.5f); ++ptr;
				*ptr = value.y < 0.0f ? (int)((value.y * accuracy) - 0.5f) : (int)((value.y * accuracy) + 0.5f); ++ptr;
				*ptr = value.z < 0.0f ? (int)((value.z * accuracy) - 0.5f) : (int)((value.z * accuracy) + 0.5f); ++ptr;
			}
		}
	}
}
