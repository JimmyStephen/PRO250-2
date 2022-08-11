namespace Fusion.KCC
{
	using System;
	using System.Collections.Generic;
	using UnityEngine;

	/// <summary>
	/// Base settings for <c>KCC</c>, can be modified at runtime.
	/// </summary>
	[Serializable]
	public sealed partial class KCCSettings
	{
		// PUBLIC MEMBERS

		[Header("Networked")]

		[Tooltip("Defines KCC physics behavior.\n" +
		"None - Skips almost all execution including processors, collider is despawned.\n" +
		"Capsule - Full processing with capsule collider spawned.\n" +
		"Void - Skips internal physics query, collider is despawned, processors are executed.")]
		public EKCCShape Shape = EKCCShape.Capsule;

		[Tooltip("Sets collider isTrigger.")]
		public bool IsTrigger = false;

		[Tooltip("Sets collider radius.")]
		public float Radius = 0.35f;

		[Tooltip("Sets collider height.")]
		public float Height = 1.8f;

		[Tooltip("Defines additional radius extent for ground detection and processors tracking. Recommended range is 1-2% of radius.\n" +
		"Low values decreases stability and has potential performance impact when executing additional checks.\n" +
		"High values increases stability at the cost of increased sustained performance impact.")]
		public float Extent = 0.035f;

		[Tooltip("Mass used in calculations with dynamic forces.")]
		public float Mass = 1.0f;

		[Tooltip("Sets layer of collider game object.")]
		[KCCLayer]
		public int ColliderLayer = 0;

		[Tooltip("Layer mask the KCC collides with.")]
		public LayerMask CollisionLayerMask = 1;

		[Tooltip("Defines KCC render behavior for input/state authority.\n" +
		"None - Skips render completely. Useful when render update is perfectly synchronized with fixed update or debugging.\n" +
		"Predict - Full processing and physics query.\n" +
		"Interpolate - Interpolation between last two fixed updates.")]
		public EKCCRenderBehavior RenderBehavior = EKCCRenderBehavior.Predict;

		[Tooltip("Default KCC features.")]
		public EKCCFeatures Features = EKCCFeatures.All;

		[Header("Local")]

		[Tooltip("Used to skip collider creation on proxies.")]
		public bool SpawnColliderOnProxy = true;

		[Tooltip("Allows input authority to call Teleport RPC. Use with caution.")]
		public bool AllowClientTeleports = false;

		[Tooltip("Maximum ground check distance for snapping.")]
		public float GroundSnapDistance = 0.25f;

		[Tooltip("Extra snapping speed per second.")]
		public float GroundSnapSpeed = 4.0f;

		[Tooltip("Maximum obstacle height to step on it.")]
		public float StepHeight = 0.3f;

		[Tooltip("Multiplier of unapplied movement projected to step up. This helps traversing obstacles faster.")]
		public float StepSpeed = 1.0f;

		[Tooltip("Default processors, executed all the time.")]
		public List<KCCProcessor> Processors = new List<KCCProcessor>();

		// PUBLIC METHODS

		public void CopyFromOther(KCCSettings other)
		{
			Shape                = other.Shape;
			IsTrigger            = other.IsTrigger;
			Radius               = other.Radius;
			Height               = other.Height;
			Extent               = other.Extent;
			Mass                 = other.Mass;
			ColliderLayer        = other.ColliderLayer;
			CollisionLayerMask   = other.CollisionLayerMask;
			RenderBehavior       = other.RenderBehavior;
			Features             = other.Features;
			SpawnColliderOnProxy = other.SpawnColliderOnProxy;
			AllowClientTeleports = other.AllowClientTeleports;
			GroundSnapDistance   = other.GroundSnapDistance;
			GroundSnapSpeed      = other.GroundSnapSpeed;
			StepHeight           = other.StepHeight;
			StepSpeed            = other.StepSpeed;

			Processors.Clear();
			Processors.AddRange(other.Processors);

			CopyUserSettingsFromOther(other);
		}

		// PARTIAL METHODS

		partial void CopyUserSettingsFromOther(KCCSettings other);
	}
}
