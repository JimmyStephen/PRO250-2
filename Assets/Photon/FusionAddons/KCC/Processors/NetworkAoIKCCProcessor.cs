namespace Fusion.KCC
{
	using UnityEngine;

	/// <summary>
	/// Default processor implementation with support of [Networked] properties and area of interest.
	/// Execution of methods is fully supported on 1) Prefabs, 2) Instances spawned with GameObject.Instantiate(), 3) Instances spawned with Runner.Spawn()
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(NetworkObject))]
	public abstract class NetworkAoIKCCProcessor : NetworkAreaOfInterestBehaviour, IKCCProcessor, IKCCProcessorProvider
	{
		// NetworkAoIKCCProcessor INTERFACE

		/// <summary>
		/// Processors with higher priority are executed earlier.
		/// </summary>
		public virtual float Priority => default;

		/// <summary>
		/// Used to filter <c>KCCProcessor</c> stage method calls. Executed on KCC input and state authority only. Returns <c>EKCCStages.All</c> by default.
		/// </summary>
		public virtual EKCCStages GetValidStages(KCC kcc, KCCData data)
		{
			return EKCCStages.All;
		}

		/// <summary>
		/// Dedicated stage to set all input properties (ground angle, base position, gravity, ...). Executed on KCC input and state authority only.
		/// </summary>
		public virtual void SetInputProperties(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Dedicated stage to calculate KCCData.DynamicVelocity. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void SetDynamicVelocity(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Dedicated stage to calculate KCCData.KinematicDirection. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void SetKinematicDirection(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Dedicated stage to calculate KCCData.KinematicTangent. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void SetKinematicTangent(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Dedicated stage to calculate KCCData.KinematicSpeed. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void SetKinematicSpeed(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Dedicated stage to calculate KCCData.KinematicVelocity. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void SetKinematicVelocity(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Dedicated stage to calculate properties after single physics query (for example kinematic velocity ground projection).
		/// This method can be called multiple times in a row if the KCC moves too fast (CCD is applied). Executed on KCC input and state authority only.
		/// </summary>
		public virtual void ProcessPhysicsQuery(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Called when a KCC starts interacting with the processor. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void OnEnter(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Called when a KCC stops interacting with the processor. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void OnExit(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Called when a KCC continues interacting with the processor. Executed on KCC input and state authority only.
		/// </summary>
		public virtual void OnStay(KCC kcc, KCCData data)
		{
		}

		/// <summary>
		/// Called when a KCC is interacting with the processor. Executed on KCC input/state authority with EKCCRenderBehavior.Interpolate and KCC proxy.
		/// </summary>
		public virtual void OnInterpolate(KCC kcc, KCCData data)
		{
		}

		// IKCCProcessor INTERFACE

		void IKCCProcessor.Enter(KCC kcc, KCCData data)
		{
			OnEnter(kcc, data);
		}

		void IKCCProcessor.Exit(KCC kcc, KCCData data)
		{
			OnExit(kcc, data);
		}

		void IKCCProcessor.Stay(KCC kcc, KCCData data)
		{
			OnStay(kcc, data);
		}

		void IKCCProcessor.Interpolate(KCC kcc, KCCData data)
		{
			OnInterpolate(kcc, data);
		}

		// IKCCProcessorProvider INTERFACE

		IKCCProcessor IKCCProcessorProvider.GetProcessor()
		{
			return this;
		}
	}
}
