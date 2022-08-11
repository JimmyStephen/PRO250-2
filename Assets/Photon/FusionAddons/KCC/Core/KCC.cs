namespace Fusion.KCC
{
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEngine.Profiling;
	using UnityEngine.SceneManagement;

	/// <summary>
	/// Kinematic character controller component.
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Rigidbody))]
	[OrderBefore(typeof(HitboxManager))]
	public sealed partial class KCC : NetworkAreaOfInterestBehaviour, IBeforeAllTicks, IAfterAllTicks, IAfterTick
	{
		// CONSTANTS

		private const int CACHE_SIZE   = 64;
		private const int HISTORY_SIZE = 180;

		// PUBLIC MEMBERS

		/// <summary>
		/// Controls whether the <c>KCC</c> is driven by Unity (FixedUpdate, Update) or Fusion (FixedUpdateNetwork, Render).
		/// </summary>
		public EKCCDriver Driver => _driver;

		/// <summary>
		/// Used for debugging - logs, drawings.
		/// </summary>
		public KCCDebug Debug => _debug;

		/// <summary>
		/// Used for tracking.
		/// </summary>
		public KCCStatistics Statistics => _statistics;

		/// <summary>
		/// Returns <c>FixedData</c> if in fixed update, otherwise <c>RenderData</c>.
		/// </summary>
		public KCCData Data => IsInFixedUpdate == true ? _fixedData : _renderData;

		/// <summary>
		/// Returns <c>KCCData</c> instance used for calculations in fixed update.
		/// </summary>
		public KCCData FixedData => _fixedData;

		/// <summary>
		/// Returns <c>KCCData</c> instance used for calculations in render update.
		/// </summary>
		public KCCData RenderData => _renderData;

		/// <summary>
		/// Basic <c>KCC</c> settings. These settings are reset to default when <c>Initialize()</c> or <c>Deinitialize()</c> is called.
		/// </summary>
		public KCCSettings Settings => _settings;

		/// <summary>
		/// Reference to <c>KCC</c> collider. Can be null if <c>Settings.Shape</c> is set to <c>EKCCShape.None</c>.
		/// </summary>
		public CapsuleCollider Collider => _collider.Collider;

		/// <summary>
		/// Current stage the <c>KCC</c> is executing.
		/// </summary>
		public EKCCStage ActiveStage => _activeStage;

		/// <summary>
		/// Features the <c>KCC</c> is executing during update.
		/// </summary>
		public EKCCFeatures ActiveFeatures => _activeFeatures;

		/// <summary>
		/// Controls whether update methods are driven by default Unity/Fusion methods or called manually using <c>ManualFixedUpdate()</c> and <c>ManualUpdate()</c>.
		/// </summary>
		public bool HasManualUpdate => _hasManualUpdate;

		/// <summary>
		/// <c>True</c> if the <c>KCC</c> has input authority (compatible with any <c>Driver</c>).
		/// </summary>
		public bool HasInputAuthority => _hasInputAuthority;

		/// <summary>
		/// <c>True</c> if the <c>KCC</c> has state authority (compatible with any <c>Driver</c>).
		/// </summary>
		public bool HasStateAuthority => _hasStateAuthority;

		/// <summary>
		/// <c>True</c> if the <c>KCC</c> has input or state authority (compatible with any <c>Driver</c>).
		/// </summary>
		public bool HasAnyAuthority => _hasInputAuthority == true || _hasStateAuthority == true;

		/// <summary>
		/// <c>True</c> if the <c>KCC</c> doesn't have input and state authority (compatible with any <c>Driver</c>).
		/// </summary>
		public bool IsProxy => _hasInputAuthority == false && _hasStateAuthority == false;

		/// <summary>
		/// <c>True</c> if the <c>KCC</c> is in fixed update. This can be used to skip logic in render.
		/// </summary>
		public bool IsInFixedUpdate => _isFixed == true || (_driver == EKCCDriver.Fusion && Runner.Stage != default) || (_driver == EKCCDriver.Unity && Time.inFixedTimeStep == true);

		/// <summary>
		/// Number of colliders (including triggers) the <c>KCC</c> is currently touching.
		/// </summary>
		public int PhysicsContacts => _trackOverlapInfo.AllHitCount;

		/// <summary>
		/// Render position difference on input authority compared to state authority.
		/// </summary>
		public Vector3 PredictionError => _predictionError;

		/// <summary>
		/// Locally executed processors. This list is cleared in Initialize().
		/// </summary>
		[NonSerialized]
		public List<IKCCProcessor> LocalProcessors = new List<IKCCProcessor>();

		/// <summary>
		/// Called at the end of Initialize().
		/// </summary>
		public event Action<KCC> OnInitialize;

		/// <summary>
		/// Called at the start of Deinitialize().
		/// </summary>
		public event Action<KCC> OnDeinitialize;

		/// <summary>
		/// Called when a collision with networked object starts. This callback is invoked in both fixed and render update on input and state authority.
		/// </summary>
		public event Action<KCC, KCCCollision> OnCollisionEnter;

		/// <summary>
		/// Called when a collision with networked object ends. This callback is invoked in both fixed and render update on input and state authority.
		/// </summary>
		public event Action<KCC, KCCCollision> OnCollisionExit;

		/// <summary>
		/// Custom collision resolver callback. Use this to apply extra filtering.
		/// </summary>
		public Func<KCC, Collider, bool> ResolveCollision;

		// PRIVATE MEMBERS

		[SerializeField]
		private KCCSettings     _settings = new KCCSettings();

		private EKCCDriver      _driver;
		private Transform       _transform;
		private Rigidbody       _rigidbody;
		private bool            _isFixed;
		private bool            _isSpawned;
		private bool            _isInitialized;
		private bool            _hasManualUpdate;
		private bool            _hasInputAuthority;
		private bool            _hasStateAuthority;
		private KCCUpdater      _updater;
		private KCCDebug        _debug                  = new KCCDebug();
		private KCCStatistics   _statistics             = new KCCStatistics();
		private KCCCollider     _collider               = new KCCCollider();
		private KCCData         _fixedData              = new KCCData();
		private KCCData         _renderData             = new KCCData();
		private KCCData[]       _historyData            = new KCCData[HISTORY_SIZE];
		private KCCData         _transientData          = new KCCData();
		private KCCSettings     _defaultSettings        = new KCCSettings();
		private KCCSettings     _runtimeSettings        = new KCCSettings();
		private KCCOverlapInfo  _extendedOverlapInfo    = new KCCOverlapInfo(CACHE_SIZE);
		private KCCOverlapInfo  _sharedOverlapInfo      = new KCCOverlapInfo(CACHE_SIZE);
		private KCCOverlapInfo  _trackOverlapInfo       = new KCCOverlapInfo(CACHE_SIZE);
		private KCCRaycastInfo  _raycastInfo            = new KCCRaycastInfo(CACHE_SIZE);
		private List<Collider>  _childColliders         = new List<Collider>();
		private RaycastHit[]    _raycastHits            = new RaycastHit[CACHE_SIZE];
		private Collider[]      _hitColliders           = new Collider[CACHE_SIZE];
		private Collider[]      _addColliders           = new Collider[CACHE_SIZE];
		private Collider[]      _removeColliders        = new Collider[CACHE_SIZE];
		private KCCCollision[]  _removeCollisions       = new KCCCollision[CACHE_SIZE];
		private KCCResolver     _resolver               = new KCCResolver(CACHE_SIZE);
		private EKCCStage       _activeStage            = EKCCStage.None;
		private EKCCFeatures    _activeFeatures         = EKCCFeatures.None;
		private IKCCProcessor[] _stageProcessors        = new IKCCProcessor[CACHE_SIZE];
		private int             _stageProcessorIndex    = 0;
		private IKCCProcessor[] _cachedProcessors       = new IKCCProcessor[CACHE_SIZE];
		private EKCCStages[]    _cachedProcessorStages  = new EKCCStages[CACHE_SIZE];
		private int             _cachedProcessorCount   = 0;
		private float           _lastRenderTime;
		private Vector3         _lastRenderPosition;
		private Vector3         _predictionError;

		private static readonly Action<IKCCProcessor, KCC, KCCData> _setInputProperties    = (processor, kcc, data) => processor.SetInputProperties(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _setDynamicVelocity    = (processor, kcc, data) => processor.SetDynamicVelocity(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _setKinematicDirection = (processor, kcc, data) => processor.SetKinematicDirection(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _setKinematicTangent   = (processor, kcc, data) => processor.SetKinematicTangent(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _setKinematicSpeed     = (processor, kcc, data) => processor.SetKinematicSpeed(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _setKinematicVelocity  = (processor, kcc, data) => processor.SetKinematicVelocity(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _processPhysicsQuery   = (processor, kcc, data) => processor.ProcessPhysicsQuery(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _stay                  = (processor, kcc, data) => processor.Stay(kcc, data);
		private static readonly Action<IKCCProcessor, KCC, KCCData> _interpolate           = (processor, kcc, data) => processor.Interpolate(kcc, data);

		// PUBLIC METHODS

		/// <summary>
		/// Set non-interpolated world space input direction. Vector with magnitude greater than 1.0f is normalized.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetInputDirection(Vector3 direction)
		{
			if (HasAnyAuthority == false)
				return;

			if (direction.sqrMagnitude > 1.0f)
			{
				direction.Normalize();
			}

			_renderData.InputDirection = direction;

			if (IsInFixedUpdate == true)
			{
				_fixedData.InputDirection = direction;
			}
		}

		/// <summary>
		/// Add pitch and yaw look rotation. Resulting values are clamped to &lt;-90, 90&gt; (pitch) and &lt;-180, 180&gt; (yaw).
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddLookRotation(float pitchDelta, float yawDelta)
		{
			if (HasAnyAuthority == false)
				return;

			KCCData data = _renderData;

			if (pitchDelta != 0.0f)
			{
				data.LookPitch = Mathf.Clamp(data.LookPitch + pitchDelta, -90.0f, 90.0f);
			}

			if (yawDelta != 0.0f)
			{
				float lookYaw = data.LookYaw + yawDelta;
				while (lookYaw > 180.0f)
				{
					lookYaw -= 360.0f;
				}
				while (lookYaw < -180.0f)
				{
					lookYaw += 360.0f;
				}

				data.LookYaw = lookYaw;
			}

			if (IsInFixedUpdate == true)
			{
				data = _fixedData;

				if (pitchDelta != 0.0f)
				{
					data.LookPitch = Mathf.Clamp(data.LookPitch + pitchDelta, -90.0f, 90.0f);
				}

				if (yawDelta != 0.0f)
				{
					float lookYaw = data.LookYaw + yawDelta;
					while (lookYaw > 180.0f)
					{
						lookYaw -= 360.0f;
					}
					while (lookYaw < -180.0f)
					{
						lookYaw += 360.0f;
					}

					data.LookYaw = lookYaw;
				}
			}

			SynchronizeTransform(data, false, true);
		}

		/// <summary>
		/// Add pitch (x) and yaw (y) look rotation. Resulting values are clamped to &lt;-90, 90&gt; (pitch) and &lt;-180, 180&gt; (yaw).
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddLookRotation(Vector2 lookRotationDelta)
		{
			AddLookRotation(lookRotationDelta.x, lookRotationDelta.y);
		}

		/// <summary>
		/// Set pitch and yaw look rotation. Values are clamped to &lt;-90, 90&gt; (pitch) and &lt;-180, 180&gt; (yaw).
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetLookRotation(float pitch, float yaw)
		{
			if (HasAnyAuthority == false)
				return;

			KCCUtility.ClampLookRotationAngles(ref pitch, ref yaw);

			KCCData data = _renderData;

			data.LookPitch = pitch;
			data.LookYaw   = yaw;

			if (IsInFixedUpdate == true)
			{
				data = _fixedData;

				data.LookPitch = pitch;
				data.LookYaw   = yaw;
			}

			SynchronizeTransform(data, false, true);
		}

		/// <summary>
		/// Set pitch and yaw look rotation. Values are clamped to &lt;-90, 90&gt; (pitch) and &lt;-180, 180&gt; (yaw).
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetLookRotation(Vector2 lookRotation)
		{
			SetLookRotation(lookRotation.x, lookRotation.y);
		}

		/// <summary>
		/// Set pitch and yaw look rotation. Roll is ignored (not supported). Values are clamped to &lt;-90, 90&gt; (pitch) and &lt;-180, 180&gt; (yaw).
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetLookRotation(Quaternion lookRotation, bool preservePitch = false, bool preserveYaw = false)
		{
			if (HasAnyAuthority == false)
				return;

			KCCData data = _renderData;

			KCCUtility.GetLookRotationAngles(lookRotation, out float pitch, out float yaw);

			if (preservePitch == false) { data.LookPitch = pitch; }
			if (preserveYaw   == false) { data.LookYaw   = yaw;   }

			if (IsInFixedUpdate == true)
			{
				data = _fixedData;

				if (preservePitch == false) { data.LookPitch = pitch; }
				if (preserveYaw   == false) { data.LookYaw   = yaw;   }
			}

			SynchronizeTransform(data, false, true);
		}

		/// <summary>
		/// Add jump impulse, which should be propagated by processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void Jump(Vector3 impulse)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.JumpImpulse += impulse;

			if (IsInFixedUpdate == true)
			{
				_fixedData.JumpImpulse += impulse;
			}
		}

		/// <summary>
		/// Add velocity from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddExternalVelocity(Vector3 velocity)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalVelocity += velocity;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalVelocity += velocity;
			}
		}

		/// <summary>
		/// Set velocity from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetExternalVelocity(Vector3 velocity)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalVelocity = velocity;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalVelocity = velocity;
			}

			_transientData.ExternalVelocity = default;
		}

		/// <summary>
		/// Add acceleration from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddExternalAcceleration(Vector3 acceleration)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalAcceleration += acceleration;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalAcceleration += acceleration;
			}
		}

		/// <summary>
		/// Set acceleration from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetExternalAcceleration(Vector3 acceleration)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalAcceleration = acceleration;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalAcceleration = acceleration;
			}

			_transientData.ExternalAcceleration = default;
		}

		/// <summary>
		/// Add impulse from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddExternalImpulse(Vector3 impulse)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalImpulse += impulse;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalImpulse += impulse;
			}
		}

		/// <summary>
		/// Set impulse from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetExternalImpulse(Vector3 impulse)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalImpulse = impulse;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalImpulse = impulse;
			}

			_transientData.ExternalImpulse = default;
		}

		/// <summary>
		/// Add force from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddExternalForce(Vector3 force)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalForce += force;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalForce += force;
			}
		}

		/// <summary>
		/// Set force from external sources. Should propagate in processors to <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetExternalForce(Vector3 force)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.ExternalForce = force;

			if (IsInFixedUpdate == true)
			{
				_fixedData.ExternalForce = force;
			}

			_transientData.ExternalForce = default;
		}

		/// <summary>
		/// Set <c>KCCData.DynamicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetDynamicVelocity(Vector3 velocity)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.DynamicVelocity = velocity;

			if (IsInFixedUpdate == true)
			{
				_fixedData.DynamicVelocity = velocity;
			}
		}

		/// <summary>
		/// Set <c>KCCData.KinematicVelocity</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetKinematicVelocity(Vector3 velocity)
		{
			if (HasAnyAuthority == false)
				return;

			_renderData.KinematicVelocity = velocity;

			if (IsInFixedUpdate == true)
			{
				_fixedData.KinematicVelocity = velocity;
			}
		}

		/// <summary>
		/// Set <c>KCCData.BasePosition</c>, <c>KCCData.TargetPosition</c> and immediately synchronize Transform.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetPosition(Vector3 position)
		{
			if (HasAnyAuthority == false)
				return;

			KCCData data = _renderData;

			data.BasePosition       = position;
			data.DesiredPosition    = position;
			data.TargetPosition     = position;
			data.HasTeleported      = true;
			data.IsSteppingUp       = false;
			data.IsSnappingToGround = false;

			if (IsInFixedUpdate == true)
			{
				data = _fixedData;

				data.BasePosition       = position;
				data.DesiredPosition    = position;
				data.TargetPosition     = position;
				data.HasTeleported      = true;
				data.IsSteppingUp       = false;
				data.IsSnappingToGround = false;
			}

			SynchronizeTransform(data, true, false);
		}

		/// <summary>
		/// Teleport to a specific position with look rotation and immediately synchronize Transform.
		/// This RPC is for input authority only, state authority should use <c>SetPosition()</c> and <c>SetLookRotation()</c> instead.
		/// <c>KCCSettings.AllowClientTeleports</c> must be set to <c>true</c> for this to work.
		/// </summary>
		[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
		public void TeleportRPC(Vector3 position, float lookPitch, float lookYaw)
		{
			if (_settings.AllowClientTeleports == false)
				throw new InvalidOperationException();

			KCCUtility.ClampLookRotationAngles(ref lookPitch, ref lookYaw);

			_renderData.BasePosition       = position;
			_renderData.DesiredPosition    = position;
			_renderData.TargetPosition     = position;
			_renderData.HasTeleported      = true;
			_renderData.IsSteppingUp       = false;
			_renderData.IsSnappingToGround = false;
			_renderData.LookPitch          = lookPitch;
			_renderData.LookYaw            = lookYaw;

			_fixedData.BasePosition       = position;
			_fixedData.DesiredPosition    = position;
			_fixedData.TargetPosition     = position;
			_fixedData.HasTeleported      = true;
			_fixedData.IsSteppingUp       = false;
			_fixedData.IsSnappingToGround = false;
			_fixedData.LookPitch          = lookPitch;
			_fixedData.LookYaw            = lookYaw;

			SynchronizeTransform(_fixedData, true, true);
		}

		/// <summary>
		/// Immediately synchronize Transform and Rigidbody based on current state.
		/// </summary>
		public void SynchronizeTransform(bool synchronizePosition, bool synchronizeRotation)
		{
			SynchronizeTransform(Data, synchronizePosition, synchronizeRotation);
		}

		/// <summary>
		/// Update <c>Shape</c>, <c>Radius</c> (optional), <c>Height</c> (optional) in settings and immediately synchronize with Collider.
	    /// <list type="bullet">
	    /// <item><description>None - Skips almost all execution including processors, collider is despawned.</description></item>
	    /// <item><description>Capsule - Full processing with capsule collider spawned.</description></item>
	    /// <item><description>Void - Skips internal physics query, collider is despawned, processors are executed.</description></item>
	    /// </list>
		/// </summary>
		public void SetShape(EKCCShape shape, float radius = 0.0f, float height = 0.0f)
		{
			if (HasAnyAuthority == false)
				return;

			_settings.Shape = shape;

			if (radius > 0.0f) { _settings.Radius = radius; }
			if (height > 0.0f) { _settings.Height = height; }

			RefreshCollider();
		}

		/// <summary>
		/// Update <c>IsTrigger</c> flag in settings and immediately synchronize with Collider.
		/// </summary>
		public void SetTrigger(bool isTrigger)
		{
			if (HasAnyAuthority == false)
				return;

			_settings.IsTrigger = isTrigger;

			RefreshCollider();
		}

		/// <summary>
		/// Update <c>Radius</c> in settings and immediately synchronize with Collider.
		/// </summary>
		public void SetRadius(float radius)
		{
			if (radius <= 0.0f)
				return;
			if (HasAnyAuthority == false)
				return;

			_settings.Radius = radius;

			RefreshCollider();
		}

		/// <summary>
		/// Update <c>Height</c> in settings and immediately synchronize with Collider.
		/// </summary>
		public void SetHeight(float height)
		{
			if (height <= 0.0f)
				return;
			if (HasAnyAuthority == false)
				return;

			_settings.Height = height;

			RefreshCollider();
		}

		/// <summary>
		/// Update <c>Mass</c> in settings.
		/// </summary>
		public void SetMass(float mass)
		{
			if (mass <= 0.0f)
				return;
			if (HasAnyAuthority == false)
				return;

			_settings.Mass = mass;
		}

		/// <summary>
		/// Update <c>ColliderLayer</c> in settings and immediately synchronize with Collider.
		/// </summary>
		public void SetLayer(int layer)
		{
			if (HasAnyAuthority == false)
				return;

			_settings.ColliderLayer = layer;

			RefreshCollider();
		}

		/// <summary>
		/// Update <c>CollisionLayerMask</c> in settings.
		/// </summary>
		public void SetLayerMask(LayerMask layerMask)
		{
			if (HasAnyAuthority == false)
				return;

			_settings.CollisionLayerMask = layerMask;
		}

		/// <summary>
		/// Add/remove networked collider to/from custom ignore list. The object must have <c>NetworkObject</c> component to correctly synchronize over network.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void SetIgnoreCollider(Collider ignoreCollider, bool ignore)
		{
			if (ignoreCollider == null)
				return;
			if (HasAnyAuthority == false)
				return;

			KCCData data = Data;

			if (ignore == true)
			{
				if (data.Ignores.HasCollider(ignoreCollider) == true)
					return;

				NetworkObject networkObject = ignoreCollider.GetComponentNoAlloc<NetworkObject>();
				if (networkObject == null)
				{
					UnityEngine.Debug.LogError($"Collider {ignoreCollider.name} doesn't have {nameof(NetworkObject)} component! Ignoring.", ignoreCollider.gameObject);
					return;
				}

				Collider checkCollider = ignoreCollider.gameObject.GetComponentNoAlloc<Collider>();
				if (object.ReferenceEquals(checkCollider, ignoreCollider) == false)
				{
					UnityEngine.Debug.LogError($"Object {ignoreCollider.name} has multiple {nameof(Collider)} components, this is not allowed for ignored colliders! Ignoring.", ignoreCollider.gameObject);
					return;
				}

				data.Ignores.Add(networkObject, ignoreCollider, false);
			}
			else
			{
				data.Ignores.Remove(ignoreCollider);
			}
		}

		/// <summary>
		/// Refresh child colliders list, used for collision filtering.
		/// Child colliders are ignored completely, triggers are treated as valid collision.
		/// </summary>
		public void RefreshChildColliders()
		{
			_childColliders.Clear();

			GetComponentsInChildren(true, _childColliders);

			int currentIndex = 0;
			int lastIndex    = _childColliders.Count - 1;

			while (currentIndex <= lastIndex)
			{
				Collider childCollider = _childColliders[currentIndex];
				if (childCollider.isTrigger == true || childCollider == _collider.Collider)
				{
					_childColliders[currentIndex] = _childColliders[lastIndex];
					_childColliders.RemoveAt(lastIndex);

					--lastIndex;
				}
				else
				{
					++currentIndex;
				}
			}
		}

		/// <summary>
		/// Check if the <c>KCC</c> has registered custom modifier (interaction provider) of type T in <c>KCCData.Modifiers</c>.
		/// </summary>
		public bool HasModifier<T>() where T : class
		{
			return Data.Modifiers.HasProvider<T>() == true;
		}

		/// <summary>
		/// Check if the <c>KCC</c> has registered custom modifier (interaction provider) in <c>KCCData.Modifiers</c>.
		/// </summary>
		public bool HasModifier<T>(T provider) where T : Component, IKCCInteractionProvider
		{
			if (provider == null)
				return false;

			return Data.Modifiers.HasProvider(provider) == true;
		}

		/// <summary>
		/// Returns any registered custom modifier (interaction provider) of type T from <c>KCCData.Modifiers</c>.
		/// </summary>
		public T GetModifier<T>() where T : class
		{
			return Data.Modifiers.GetProvider<T>();
		}

		/// <summary>
		/// Register custom modifier (interaction provider) to <c>KCCData.Modifiers</c>.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void AddModifier<T>(T provider) where T : Component, IKCCInteractionProvider
		{
			if (provider == null)
				return;
			if (HasAnyAuthority == false)
				return;

			KCCData data = Data;

			if (data.Modifiers.HasProvider(provider) == true)
				return;

			NetworkObject networkObject = provider.GetComponentNoAlloc<NetworkObject>();
			if (networkObject == null)
			{
				UnityEngine.Debug.LogError($"Interaction provider {provider.name} doesn't have {nameof(NetworkObject)} component! Ignoring.", provider.gameObject);
				return;
			}

			IKCCInteractionProvider checkProvider = provider.gameObject.GetComponentNoAlloc<IKCCInteractionProvider>();
			if (object.ReferenceEquals(checkProvider, provider) == false)
			{
				UnityEngine.Debug.LogError($"Object {provider.name} has multiple {nameof(IKCCInteractionProvider)} components, this is not allowed for custom modifiers! Ignoring.", provider.gameObject);
				return;
			}

			KCCModifier modifier = data.Modifiers.Add(networkObject, provider);
			if (modifier.Processor != null)
			{
				OnProcessorAdded(data, modifier.Processor);
			}
		}

		/// <summary>
		/// Unregister custom modifier (interaction provider) from <c>KCCData.Modifiers</c>. Removed processor won't execute any pending stage method.
		/// Changes done in render will vanish with next fixed update.
		/// </summary>
		public void RemoveModifier<T>(T provider) where T : Component, IKCCInteractionProvider
		{
			if (provider == null)
				return;
			if (HasAnyAuthority == false)
				return;

			KCCData data = Data;

			KCCModifier modifier = data.Modifiers.Find(provider);
			if (modifier != null)
			{
				IKCCProcessor processor = modifier.Processor;

				data.Modifiers.Remove(modifier);

				if (processor != null)
				{
					OnProcessorRemoved(data, processor);
				}
			}
		}

		/// <summary>
		/// Check if the <c>KCC</c> has registered any interaction provider of type T.
		/// </summary>
		public bool HasInteraction<T>() where T : class
		{
			KCCData data = Data;

			if (data.Modifiers.HasProvider<T>() == true)
				return true;
			if (data.Collisions.HasProvider<T>() == true)
				return true;

			return false;
		}

		/// <summary>
		/// Check if the <c>KCC</c> has registered interaction provider.
		/// <param name="provider">IKCCInteractionProvider instance.</param>
		/// </summary>
		public bool HasInteraction<T>(T provider) where T : Component, IKCCInteractionProvider
		{
			if (provider == null)
				return false;

			KCCData data = Data;

			if (data.Modifiers.HasProvider(provider) == true)
				return true;
			if (data.Collisions.HasProvider(provider) == true)
				return true;

			return false;
		}

		/// <summary>
		/// Returns any registered interaction provider of type T.
		/// </summary>
		public T GetInteraction<T>() where T : class
		{
			T provider;

			KCCData data = Data;

			provider = data.Modifiers.GetProvider<T>();
			if (object.ReferenceEquals(provider, null) == false)
				return provider;

			provider = data.Collisions.GetProvider<T>();
			if (object.ReferenceEquals(provider, null) == false)
				return provider;

			return null;
		}

		/// <summary>
		/// Check if the KCC has registered any processor of type T.
		/// </summary>
		public bool HasProcessor<T>() where T : class
		{
			KCCData data = Data;

			if (data.Modifiers.HasProcessor<T>() == true)
				return true;
			if (data.Collisions.HasProcessor<T>() == true)
				return true;

			List<IKCCProcessor> localProcessors = LocalProcessors;
			for (int i = 0, count = localProcessors.Count; i < count; ++i)
			{
				if (localProcessors[i] is T)
					return true;
			}

			List<KCCProcessor> settingsProcessors = _settings.Processors;
			for (int i = 0, count = settingsProcessors.Count; i < count; ++i)
			{
				if (settingsProcessors[i] is T)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Check if the KCC has registered processor.
		/// </summary>
		/// <param name="processor">IKCCProcessor instance.</param>
		public bool HasProcessor<T>(T processor) where T : Component, IKCCProcessor
		{
			if (processor == null)
				return false;

			KCCData data = Data;

			if (data.Modifiers.HasProcessor(processor) == true)
				return true;
			if (data.Collisions.HasProcessor(processor) == true)
				return true;

			List<IKCCProcessor> localProcessors = LocalProcessors;
			for (int i = 0, count = localProcessors.Count; i < count; ++i)
			{
				if (object.ReferenceEquals(localProcessors[i], processor) == true)
					return true;
			}

			List<KCCProcessor> settingsProcessors = _settings.Processors;
			for (int i = 0, count = settingsProcessors.Count; i < count; ++i)
			{
				if (object.ReferenceEquals(settingsProcessors[i], processor) == true)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns any registered processor of type T.
		/// </summary>
		public T GetProcessor<T>() where T : class
		{
			T processor;

			KCCData data = Data;

			processor = data.Modifiers.GetProcessor<T>();
			if (object.ReferenceEquals(processor, null) == false)
				return processor;

			processor = data.Collisions.GetProcessor<T>();
			if (object.ReferenceEquals(processor, null) == false)
				return processor;


			List<IKCCProcessor> localProcessors = LocalProcessors;
			for (int i = 0, count = localProcessors.Count; i < count; ++i)
			{
				if (localProcessors[i] is T localProcessor)
					return localProcessor;
			}

			List<KCCProcessor> settingsProcessors = _settings.Processors;
			for (int i = 0, count = settingsProcessors.Count; i < count; ++i)
			{
				if (settingsProcessors[i] is T settingsProcessor)
					return settingsProcessor;
			}

			return null;
		}

		/// <summary>
		/// Check if the processor is pending active stage execution.
		/// </summary>
		public bool HasPendingProcessor(IKCCProcessor processor)
		{
			if (_activeStage == EKCCStage.None)
				throw new InvalidOperationException("Querying processor execution is allowed only during stage execution!");

			IKCCProcessor[] stageProcessors = _stageProcessors;

			for (int i = _stageProcessorIndex + 1, count = _cachedProcessorCount; i < count; ++i)
			{
				if (stageProcessors[i] == processor)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Check if any processor of type <c>T</c> is pending active stage execution.
		/// </summary>
		public bool HasPendingProcessor<T>() where T : class
		{
			if (_activeStage == EKCCStage.None)
				throw new InvalidOperationException("Querying processor execution is allowed only during stage execution!");

			IKCCProcessor[] stageProcessors = _stageProcessors;

			for (int i = _stageProcessorIndex + 1, count = _cachedProcessorCount; i < count; ++i)
			{
				if (stageProcessors[i] is T)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Check if the processor has executed in active stage.
		/// </summary>
		public bool HasExecutedProcessor(IKCCProcessor processor)
		{
			if (_activeStage == EKCCStage.None)
				throw new InvalidOperationException("Querying processor execution is allowed only during stage execution!");

			IKCCProcessor[] stageProcessors = _stageProcessors;

			for (int i = _stageProcessorIndex; i >= 0; --i)
			{
				if (stageProcessors[i] == processor)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Check if any processor of type <c>T</c> has executed in active stage.
		/// </summary>
		public bool HasExecutedProcessor<T>() where T : class
		{
			if (_activeStage == EKCCStage.None)
				throw new InvalidOperationException("Querying processor execution is allowed only during stage execution!");

			IKCCProcessor[] stageProcessors = _stageProcessors;

			for (int i = _stageProcessorIndex; i >= 0; --i)
			{
				if (stageProcessors[i] is T)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Suppress execution of pending processor for active stage.
		/// </summary>
		public void SuppressProcessor(IKCCProcessor processor)
		{
			if (_activeStage == EKCCStage.None)
				throw new InvalidOperationException("Suppressing processor execution is allowed only during stage execution!");

			IKCCProcessor[] stageProcessors = _stageProcessors;

			for (int i = _stageProcessorIndex + 1, count = _cachedProcessorCount; i < count; ++i)
			{
				if (stageProcessors[i] == processor)
				{
					stageProcessors[i] = null;
					break;
				}
			}
		}

		/// <summary>
		/// Suppress execution of pending processors of type <c>T</c> for active stage.
		/// </summary>
		public void SuppressProcessors<T>() where T : class
		{
			if (_activeStage == EKCCStage.None)
				throw new InvalidOperationException("Suppressing processor execution is allowed only during stage execution!");

			IKCCProcessor[] stageProcessors = _stageProcessors;

			for (int i = _stageProcessorIndex + 1, count = _cachedProcessorCount; i < count; ++i)
			{
				if (stageProcessors[i] is T)
				{
					stageProcessors[i] = null;
				}
			}
		}

		/// <summary>
		/// Suppress execution of a specific KCC feature. Valid only from SetInputProperties stage.
		/// </summary>
		public void SuppressFeature(EKCCFeature feature)
		{
			if (_activeStage != EKCCStage.SetInputProperties)
				throw new InvalidOperationException("Suppressing features is allowed only during SetInputProperties stage!");

			_activeFeatures = (EKCCFeatures)((int)_activeFeatures & ~(1 << (int)feature));
		}

		/// <summary>
		/// Returns fixed data for specific tick in history. Valid only for input or state authority. Default history size is 180 ticks.
		/// </summary>
		public KCCData GetHistory(int tick)
		{
			if (tick < 0)
				return null;

			KCCData data = _historyData[tick % HISTORY_SIZE];
			if (data != null && data.Tick == tick)
				return data;

			return null;
		}

		/// <summary>
		/// Controls whether update methods are driven by default Unity/Fusion methods or called manually using <c>ManualFixedUpdate()</c> and <c>ManualUpdate()</c>.
		/// </summary>
		public void SetManualUpdate(bool hasManualUpdate)
		{
			_hasManualUpdate = hasManualUpdate;

			RefreshUpdater();
		}

		/// <summary>
		/// Explicit initialization with custom driver. Initialization from <c>Start()</c> and <c>Spawned()</c> will be ignored.
		/// </summary>
		public void Initialize(EKCCDriver driver)
		{
			if (driver == EKCCDriver.Fusion && _isSpawned == false)
				throw new InvalidOperationException("KCC cannot be explicitly initialized with Fusion driver before KCC.Spawned()!");

			if (_isInitialized == false)
			{
				_defaultSettings.CopyFromOther(_settings);
				_isInitialized = true;
			}

			if (_driver == driver)
				return;

			bool hasManualUpdate = _hasManualUpdate;

			SetDefaults();

			_driver = driver;

			_fixedData = new KCCData();
			_fixedData.Frame           = Time.frameCount;
			_fixedData.Alpha           = 1.0f;
			_fixedData.BasePosition    = _transform.position;
			_fixedData.DesiredPosition = _transform.position;
			_fixedData.TargetPosition  = _transform.position;

			if (_driver == EKCCDriver.Unity)
			{
				_fixedData.Tick      = Mathf.RoundToInt(Time.fixedUnscaledTime / Time.fixedUnscaledDeltaTime);
				_fixedData.Time      = Time.fixedTime;
				_fixedData.DeltaTime = Time.fixedDeltaTime;

				_hasInputAuthority = true;
				_hasStateAuthority = true;
			}
			else if (_driver == EKCCDriver.Fusion)
			{
				_fixedData.Tick      = (int)Runner.Simulation.Tick;
				_fixedData.Time      = Runner.SimulationTime;
				_fixedData.DeltaTime = Runner.Simulation.DeltaTime;

				_hasInputAuthority = Object.HasInputAuthority;
				_hasStateAuthority = Object.HasStateAuthority;
			}
			else
			{
				throw new NotSupportedException(_driver.ToString());
			}

			KCCUtility.GetLookRotationAngles(_transform.rotation, out float lookPitch, out float lookYaw);

			_fixedData.LookPitch = lookPitch;
			_fixedData.LookYaw   = lookYaw;

			if (_driver == EKCCDriver.Fusion && HasStateAuthority == false)
			{
				ReadNetworkData();
				SynchronizeTransform(_fixedData, true, true);
			}

			_renderData = new KCCData();
			_renderData.CopyFromOther(_fixedData);

			SetManualUpdate(hasManualUpdate);

			RefreshCollider();
			RefreshChildColliders();

			if (OnInitialize != null)
			{
				try { OnInitialize(this); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
			}

			if (IsInFixedUpdate == true)
			{
				_renderData.CopyFromOther(_fixedData);
			}
			else
			{
				_fixedData.CopyFromOther(_renderData);
			}

			if (_driver == EKCCDriver.Fusion && HasStateAuthority == true)
			{
				WriteNetworkData();
			}
		}

		/// <summary>
		/// Explicit deinitialization. Should be called before the object is returned to custom pool.
		/// </summary>
		public void Deinitialize()
		{
			_isInitialized = default;

			if (OnDeinitialize != null)
			{
				try { OnDeinitialize(this); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
			}

			RemoveAllCollisions(_fixedData);
			RemoveAllModifiers(_fixedData);

			SetDefaults();
		}

		/// <summary>
		/// Manual fixed update execution, <c>SetManualUpdate(true)</c> must be called prior usage.
		/// </summary>
		public void ManualFixedUpdate()
		{
			if (_driver == EKCCDriver.None)
				return;
			if (_hasManualUpdate == false)
				throw new InvalidOperationException("Manual update is not set!");

			Profiler.BeginSample("KCC.FixedUpdate");
			OnFixedUpdateInternal();
			Profiler.EndSample();
		}

		/// <summary>
		/// Manual render update execution, <c>SetManualUpdate(true)</c> must be called prior usage.
		/// </summary>
		public void ManualRenderUpdate()
		{
			if (_driver == EKCCDriver.None)
				return;
			if (_hasManualUpdate == false)
				throw new InvalidOperationException("Manual update is not set!");

			Profiler.BeginSample("KCC.RenderUpdate");
			OnRenderUpdateInternal();
			Profiler.EndSample();
		}

		/// <summary>
		/// Explicit interpolation on demand. Implicit interpolation in render update is skipped.
		/// </summary>
		public void Interpolate()
		{
			if (_driver == EKCCDriver.None)
				return;

			KCCData data = Data;

			Profiler.BeginSample("KCC.Interpolate");
			InterpolateNetworkData();
			CacheProcessors(data);
			ProcessStage(EKCCStage.Interpolate, data, _interpolate);
			SynchronizeTransform(data, true, true);
			Profiler.EndSample();
		}

		/// <summary>
		/// Enumerate currently tracked colliders.
		/// </summary>
		public IEnumerable<Collider> GetTrackedColliders()
		{
			for (int i = 0; i < _trackOverlapInfo.AllHitCount; ++i)
			{
				yield return _trackOverlapInfo.AllHits[i].Collider;
			}
		}

		// MonoBehaviour INTERFACE

		private void Awake()
		{
			_transform = transform;
			_rigidbody = GetComponent<Rigidbody>();

			if (_rigidbody == null)
				throw new NullReferenceException($"GameObject {name} has missing Rigidbody component!");

			_rigidbody.isKinematic   = true;
			_rigidbody.useGravity    = false;
			_rigidbody.interpolation = RigidbodyInterpolation.None;
			_rigidbody.constraints   = RigidbodyConstraints.FreezeAll;
		}

		private void OnDestroy()
		{
			_isFixed   = false;
			_isSpawned = false;

			Deinitialize();

			OnInitialize     = null;
			OnDeinitialize   = null;
			OnCollisionEnter = null;
			OnCollisionExit  = null;
			ResolveCollision = null;
		}

		// SimulationBehaviour INTERFACE

		public override int? DynamicWordCount => GetNetworkDataWordCount();

		public override void Spawned()
		{
			_isFixed   = true;
			_isSpawned = true;

			if (_driver != EKCCDriver.Fusion)
			{
				Initialize(EKCCDriver.Fusion);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_isFixed   = false;
			_isSpawned = false;

			Deinitialize();

			OnInitialize     = null;
			OnDeinitialize   = null;
			OnCollisionEnter = null;
			OnCollisionExit  = null;
			ResolveCollision = null;
		}

		public override void FixedUpdateNetwork()
		{
			if (_driver != EKCCDriver.Fusion)
				return;
			if (_hasManualUpdate == true)
				return;

			Profiler.BeginSample("KCC.FixedUpdate");
			OnFixedUpdateInternal();
			Profiler.EndSample();
		}

		public override void Render()
		{
			_isFixed = false;

			if (_driver != EKCCDriver.Fusion)
				return;
			if (_hasManualUpdate == true)
				return;

			Profiler.BeginSample("KCC.RenderUpdate");
			OnRenderUpdateInternal();
			Profiler.EndSample();
		}

		// IBeforeAllTicks INTERFACE

		void IBeforeAllTicks.BeforeAllTicks(bool resimulation, int tickCount)
		{
			_isFixed = false;

			if (resimulation == false)
				return;
			if (_driver != EKCCDriver.Fusion)
				return;

			Profiler.BeginSample("KCC.BeforeAllTicks");

			_hasInputAuthority = Object.HasInputAuthority;
			_hasStateAuthority = Object.HasStateAuthority;

			KCCData historyData = null;

			if (_hasInputAuthority == true)
			{
				int historyTick = Runner.Simulation.Tick;

				historyData = _historyData[historyTick % HISTORY_SIZE];
				if (historyData != null && historyData.Tick == historyTick)
				{
					_fixedData.CopyFromOther(historyData);
					_fixedData.Frame = Time.frameCount;
				}
			}

			ReadNetworkData();

			if (historyData != null)
			{
				RestoreHistoryData(historyData);
			}

			RefreshCollider();
			SynchronizeTransform(_fixedData, true, true);

			Profiler.EndSample();
		}

		// AfterAllTicks INTERFACE

		void IAfterAllTicks.AfterAllTicks(bool resimulation, int tickCount)
		{
			_isFixed = false;

			if (resimulation == true)
				return;
			if (_driver != EKCCDriver.Fusion)
				return;

			Profiler.BeginSample("KCC.AfterAllTicks");

			if (HasStateAuthority == true)
			{
				WriteNetworkData();
			}

			Profiler.EndSample();
		}

		// IAfterTick INTERFACE

		void IAfterTick.AfterTick()
		{
			if (_driver != EKCCDriver.Fusion)
				return;
			if (HasAnyAuthority == false)
				return;

			PublishFixedData();
		}

		// PRIVATE METHODS

		private void OnFixedUpdateInternal()
		{
			_isFixed = false;

			if (IsInFixedUpdate == false)
				throw new InvalidOperationException();

			if (_driver == EKCCDriver.Fusion)
			{
				_hasInputAuthority = _isSpawned == true && Object.HasInputAuthority == true;
				_hasStateAuthority = _isSpawned == true && Object.HasStateAuthority == true;
			}

			_debug.Reset();
			_statistics.Reset();

			_fixedData.Frame = Time.frameCount;
			_fixedData.Alpha = 1.0f;

			RefreshCollider();

			if (HasAnyAuthority == false)
			{
				_debug.FixedUpdate(this);
				return;
			}

			if (_driver == EKCCDriver.Unity)
			{
				_fixedData.Tick              = Mathf.RoundToInt(Time.fixedUnscaledTime / Time.fixedUnscaledDeltaTime);
				_fixedData.Time              = Time.fixedTime;
				_fixedData.DeltaTime         = Time.fixedDeltaTime;
				_fixedData.UnscaledDeltaTime = _fixedData.DeltaTime;
			}
			else if (_driver == EKCCDriver.Fusion)
			{
				_fixedData.Tick              = (int)Runner.Simulation.Tick;
				_fixedData.Time              = Runner.SimulationTime;
				_fixedData.DeltaTime         = Runner.Simulation.DeltaTime;
				_fixedData.UnscaledDeltaTime = _fixedData.DeltaTime;
			}
			else
			{
				throw new NotSupportedException(_driver.ToString());
			}

			StoreTransientData(_transientData, _fixedData);

			_isFixed = true;

			Move(_fixedData);

			_isFixed = false;

			RestoreTransientData(_transientData, _fixedData);

			if (_driver == EKCCDriver.Unity)
			{
				PublishFixedData();
			}

			SynchronizeTransform(_fixedData, true, true);

			_debug.FixedUpdate(this);
		}

		private void OnRenderUpdateInternal()
		{
			_isFixed = false;

			if (IsInFixedUpdate == true)
				throw new InvalidOperationException();

			_debug.Reset();
			_statistics.Reset();

			_renderData.Frame = Time.frameCount;

			if (HasAnyAuthority == false)
			{
				InterpolateNetworkData();
				CacheProcessors(_renderData);
				ProcessStage(EKCCStage.Interpolate, _renderData, _interpolate);
				SynchronizeTransform(_renderData, true, true);

				_debug.RenderUpdate(this);
				return;
			}

			float previousTime = _renderData.Time;

			if (_driver == EKCCDriver.Unity)
			{
				_renderData.Tick              = Mathf.RoundToInt(Time.fixedUnscaledTime / Time.fixedUnscaledDeltaTime);
				_renderData.Alpha             = Mathf.Clamp01((Time.time - _fixedData.Time) / Time.fixedDeltaTime);
				_renderData.Time              = Time.time;
				_renderData.DeltaTime         = _renderData.Time - previousTime;
				_renderData.UnscaledDeltaTime = _renderData.DeltaTime;

				if (_settings.RenderBehavior == EKCCRenderBehavior.Interpolate)
				{
					_renderData.Tick -= 1;
					_renderData.Time -= Time.fixedDeltaTime;
				}
			}
			else if (_driver == EKCCDriver.Fusion)
			{
				_renderData.Tick              = (int)Runner.Simulation.Tick;
				_renderData.Alpha             = Runner.Simulation.StateAlpha;
				_renderData.Time              = Runner.SimulationTime + Runner.Simulation.StateAlpha * Runner.DeltaTime;
				_renderData.DeltaTime         = _renderData.Time - previousTime;
				_renderData.UnscaledDeltaTime = _renderData.DeltaTime;

				if (_settings.RenderBehavior == EKCCRenderBehavior.Interpolate)
				{
					_renderData.Tick -= 1;
					_renderData.Time -= Runner.DeltaTime;
				}
			}
			else
			{
				throw new NotSupportedException(_driver.ToString());
			}

			UpdatePredictionCorrection();

			if (_debug.ShowPath == true)
			{
				if (_renderData.Frame == _fixedData.Frame)
				{
					UnityEngine.Debug.DrawLine(_fixedData.TargetPosition, _renderData.TargetPosition, Color.blue, _debug.DisplayTime);
				}
				else
				{
					UnityEngine.Debug.DrawLine(_lastRenderPosition, _renderData.TargetPosition, Color.magenta, _debug.DisplayTime);
				}
			}

			if (_settings.RenderBehavior == EKCCRenderBehavior.Predict)
			{
				if (_renderData.DeltaTime < 0.00005f)
				{
					Vector3 extrapolationVelocity = _renderData.DesiredVelocity;
					if (_renderData.RealVelocity.sqrMagnitude <= extrapolationVelocity.sqrMagnitude)
					{
						extrapolationVelocity = _renderData.RealVelocity;
					}

					_renderData.BasePosition    = _renderData.TargetPosition;
					_renderData.DesiredPosition = _renderData.BasePosition + extrapolationVelocity * _renderData.DeltaTime;
					_renderData.TargetPosition  = _renderData.DesiredPosition;
				}
				else
				{
					StoreTransientData(_transientData, _renderData);

					Move(_renderData);

					RestoreTransientData(_transientData, _renderData);
				}
			}
			else if (_settings.RenderBehavior == EKCCRenderBehavior.Interpolate)
			{
				_activeStage    = EKCCStage.None;
				_activeFeatures = _settings.Features;

				CacheProcessors(_renderData);
				SetInputProperties(_renderData);

				KCCData currentFixedData = _fixedData;
				if (currentFixedData.HasTeleported == false)
				{
					KCCData previousFixedData = GetHistory(currentFixedData.Tick - 1);
					if (previousFixedData != null)
					{
						float alpha = _renderData.Alpha;

						_renderData.BasePosition    = Vector3.Lerp(previousFixedData.BasePosition, currentFixedData.BasePosition, alpha) + _predictionError;
						_renderData.DesiredPosition = Vector3.Lerp(previousFixedData.DesiredPosition, currentFixedData.DesiredPosition, alpha) + _predictionError;
						_renderData.TargetPosition  = Vector3.Lerp(previousFixedData.TargetPosition, currentFixedData.TargetPosition, alpha) + _predictionError;
						_renderData.LookPitch       = Mathf.Lerp(previousFixedData.LookPitch, currentFixedData.LookPitch, alpha);
						_renderData.LookYaw         = KCCMathUtility.InterpolateRange(previousFixedData.LookYaw, currentFixedData.LookYaw, -180.0f, 180.0f, alpha);
						_renderData.RealSpeed       = Mathf.Lerp(previousFixedData.RealSpeed, currentFixedData.RealSpeed, alpha);
						_renderData.RealVelocity    = Vector3.Lerp(previousFixedData.RealVelocity, currentFixedData.RealVelocity, alpha);
					}
				}

				ProcessStage(EKCCStage.Interpolate, _renderData, _interpolate);
			}

			SynchronizeTransform(_renderData, true, true);

			_lastRenderPosition = _renderData.TargetPosition;
			_lastRenderTime     = _renderData.Time;

			_debug.RenderUpdate(this);
		}

		private void UpdatePredictionCorrection()
		{
			if (_activeFeatures.Has(EKCCFeature.PredictionCorrection) == false)
			{
				_predictionError = default;
				return;
			}

			if (_renderData.Frame == _fixedData.Frame)
			{
				KCCData current = GetHistory(_renderData.Tick);
				if (current != null && _lastRenderTime <= current.Time)
				{
					for (int i = 0; i < 5; ++i)
					{
						KCCData previous = GetHistory(current.Tick - 1);
						if (previous == null)
							break;

						if (_lastRenderTime >= previous.Time)
						{
							float alpha = (_lastRenderTime - previous.Time) / (current.Time - previous.Time);
							Vector3 expectedRenderPosition = Vector3.Lerp(previous.TargetPosition, current.TargetPosition, alpha);

							if (_debug.ShowPath == true)
							{
								UnityEngine.Debug.DrawLine(expectedRenderPosition, _lastRenderPosition, Color.yellow, _debug.DisplayTime);
							}

							_predictionError = _lastRenderPosition - expectedRenderPosition;
							if (_predictionError.sqrMagnitude >= 4.0f)
							{
								_predictionError = default;
							}

							_predictionError = Vector3.Lerp(_predictionError, Vector3.zero, 20.0f * Time.deltaTime);

							_renderData.BasePosition    += _predictionError;
							_renderData.DesiredPosition += _predictionError;
							_renderData.TargetPosition  += _predictionError;

							break;
						}

						current = previous;
					}
				}
			}
			else
			{
				_renderData.BasePosition    -= _predictionError;
				_renderData.DesiredPosition -= _predictionError;
				_renderData.TargetPosition  -= _predictionError;

				_predictionError = Vector3.Lerp(_predictionError, Vector3.zero, 30.0f * Time.deltaTime);

				_renderData.BasePosition    += _predictionError;
				_renderData.DesiredPosition += _predictionError;
				_renderData.TargetPosition  += _predictionError;
			}
		}

		private void Move(KCCData data)
		{
			_activeStage    = EKCCStage.None;
			_activeFeatures = _settings.Features;

			float   baseTime            = data.Time;
			float   baseDeltaTime       = data.DeltaTime;
			float   pendingDeltaTime    = Mathf.Clamp01(baseDeltaTime);
			Vector3 basePosition        = data.TargetPosition;
			Vector3 desiredPosition     = data.TargetPosition;
			bool    wasGrounded         = data.IsGrounded;
			bool    wasSteppingUp       = data.IsSteppingUp;
			bool    wasSnappingToGround = data.IsSnappingToGround;

			data.BasePosition    = basePosition;
			data.DesiredPosition = desiredPosition;

			if (_settings.Shape == EKCCShape.None)
			{
				RemoveAllCollisions(data);
				return;
			}

			CacheProcessors(data);

			SetInputProperties(data);

			basePosition = data.BasePosition;

			ProcessStage(EKCCStage.SetDynamicVelocity,    data, _setDynamicVelocity);
			ProcessStage(EKCCStage.SetKinematicDirection, data, _setKinematicDirection);
			ProcessStage(EKCCStage.SetKinematicTangent,   data, _setKinematicTangent);
			ProcessStage(EKCCStage.SetKinematicSpeed,     data, _setKinematicSpeed);
			ProcessStage(EKCCStage.SetKinematicVelocity,  data, _setKinematicVelocity);

			desiredPosition = data.BasePosition + data.DesiredVelocity * pendingDeltaTime;

			if (data.HasTeleported == false)
			{
				data.TargetPosition = data.BasePosition;
			}

			bool  hasFinished           = false;
			float maxDeltaMagnitude     = _settings.Radius * 0.85f;
			float optimalDeltaMagnitude = _settings.Radius * 0.75f;

			while (hasFinished == false && data.HasTeleported == false)
			{
				data.BasePosition = data.TargetPosition;

				float   deltaTimeDelta = pendingDeltaTime;
				Vector3 positionDelta  = data.DesiredVelocity * pendingDeltaTime;

				float positionDeltaMagnitude = positionDelta.magnitude;
				if (positionDeltaMagnitude > maxDeltaMagnitude)
				{
					float deltaRatio = optimalDeltaMagnitude / positionDeltaMagnitude;

					positionDelta  *= deltaRatio;
					deltaTimeDelta *= deltaRatio;
				}
				else
				{
					hasFinished = true;
				}

				pendingDeltaTime = Mathf.Max(0.0f, pendingDeltaTime - deltaTimeDelta);

				data.Time            = baseTime - pendingDeltaTime;
				data.DeltaTime       = deltaTimeDelta;
				data.DesiredPosition = data.BasePosition + positionDelta;
				data.TargetPosition  = data.DesiredPosition;

				ProcessPhysicsQuery(data);
				UpdateCollisions(data);
			}

			data.Time                = baseTime;
			data.DeltaTime           = baseDeltaTime;
			data.BasePosition        = basePosition;
			data.DesiredPosition     = desiredPosition;
			data.WasGrounded         = wasGrounded;
			data.WasSteppingUp       = wasSteppingUp;
			data.WasSnappingToGround = wasSnappingToGround;

			if (data.HasTeleported == false)
			{
				data.RealVelocity = (data.TargetPosition - data.BasePosition) / data.DeltaTime;
				data.RealSpeed    = data.RealVelocity.magnitude;
			}

			ProcessStage(EKCCStage.Stay, data, _stay);

			_activeStage = EKCCStage.None;
		}

		private void SetInputProperties(KCCData data)
		{
			data.Gravity        = Physics.gravity;
			data.HasJumped      = default;
			data.HasTeleported  = default;
			data.MaxGroundAngle = 75.0f;
			data.MaxWallAngle   = 5.0f;

			ProcessStage(EKCCStage.SetInputProperties, data, _setInputProperties);
		}

		private void ProcessPhysicsQuery(KCCData data)
		{
			data.WasGrounded         = data.IsGrounded;
			data.WasSteppingUp       = data.IsSteppingUp;
			data.WasSnappingToGround = data.IsSnappingToGround;

			data.IsGrounded          = default;
			data.IsSteppingUp        = default;
			data.IsSnappingToGround  = default;
			data.GroundNormal        = default;
			data.GroundTangent       = default;
			data.GroundPosition      = default;
			data.GroundDistance      = default;
			data.GroundAngle         = default;

			_trackOverlapInfo.Reset(false);

			if (_settings.CollisionLayerMask != 0 && _collider.IsSpawned == true)
			{
				OverlapCapsule(_extendedOverlapInfo, data, data.TargetPosition, _settings.Radius, _settings.Height, _settings.Radius, _settings.CollisionLayerMask, QueryTriggerInteraction.Collide);

				_extendedOverlapInfo.ToggleConvexMeshes(false);

				data.TargetPosition = DepenetrateColliders(_extendedOverlapInfo, data, data.BasePosition, data.TargetPosition, data.HasJumped == false, 3);

				if (data.HasJumped == true)
				{
					data.IsGrounded = false;
				}

				if (data.IsGrounded == true)
				{
					CalculateGroundProperties(data);
				}

				CheckTriggersPenetration(_extendedOverlapInfo, data);

				if (data.HasJumped == false)
				{
					TryStepUp(_extendedOverlapInfo, data);
				}

				if (data.IsGrounded == false && data.WasGrounded == true && data.HasJumped == false && data.IsSteppingUp == false && data.WasSteppingUp == false)
				{
					TrySnapToGround(data);
				}

				_extendedOverlapInfo.ToggleConvexMeshes(true);

				if (_extendedOverlapInfo.AllWithinExtent() == true)
				{
					_trackOverlapInfo.CopyFromOther(_extendedOverlapInfo);
				}
				else
				{
					OverlapCapsule(_trackOverlapInfo, data, data.TargetPosition, _settings.Radius, _settings.Height, _settings.Extent, _settings.CollisionLayerMask, QueryTriggerInteraction.Collide);
				}
			}

			ProcessStage(EKCCStage.ProcessPhysicsQuery, data, _processPhysicsQuery);
		}

		private Vector3 DepenetrateColliders(KCCOverlapInfo overlapInfo, KCCData data, Vector3 basePosition, Vector3 targetPosition, bool probeGrounding, int maxSubSteps)
		{
			if (overlapInfo.ColliderHitCount == 0)
				return targetPosition;

			if (overlapInfo.ColliderHitCount == 1)
				return DepenetrateSingle(overlapInfo, data, basePosition, targetPosition, probeGrounding);

			return DepenetrateMultiple(overlapInfo, data, basePosition, targetPosition, probeGrounding, maxSubSteps);
		}

		private Vector3 DepenetrateSingle(KCCOverlapInfo overlapInfo, KCCData data, Vector3 basePosition, Vector3 targetPosition, bool probeGrounding)
		{
			bool    hasGroundDot   = default;
			float   minGroundDot   = default;
			Vector3 groundNormal   = Vector3.up;
			float   groundDistance = default;

			KCCOverlapHit hit = overlapInfo.ColliderHits[0];

			hit.HasPenetration = Physics.ComputePenetration(_collider.Collider, targetPosition, Quaternion.identity, hit.Collider, hit.Transform.position, hit.Transform.rotation, out Vector3 direction, out float distance);
			if (hit.HasPenetration == true)
			{
				hit.IsWithinExtent = true;

				hasGroundDot = true;
				minGroundDot = Mathf.Cos(Mathf.Clamp(data.MaxGroundAngle, 0.0f, 90.0f) * Mathf.Deg2Rad);

				float directionUpDot = Vector3.Dot(direction, Vector3.up);
				if (directionUpDot >= minGroundDot)
				{
					hit.IsGround = true;

					data.IsGrounded = true;

					groundNormal = direction;
				}
				else
				{
					float maxWallDot = Mathf.Cos(Mathf.Clamp(90.0f - data.MaxWallAngle, 0.0f, 90.0f) * Mathf.Deg2Rad);
					if (directionUpDot >= -maxWallDot)
					{
						if (directionUpDot <= maxWallDot)
						{
							hit.IsWall = true;
						}
						else
						{
							hit.IsSlope = true;
						}
					}

					if (directionUpDot > 0.0f && distance >= 0.000001f && data.DynamicVelocity.y <= 0.0f)
					{
						Vector3 positionDelta = targetPosition - basePosition;

						float movementDot = Vector3.Dot(positionDelta.OnlyXZ(), direction.OnlyXZ());
						if (movementDot < 0.0f)
						{
							KCCPhysicsUtility.ProjectVerticalPenetration(ref direction, ref distance);
						}
					}
				}

				targetPosition += direction * distance;
			}

			if (probeGrounding == true && data.IsGrounded == false)
			{
				if (hasGroundDot == false)
				{
					minGroundDot = Mathf.Cos(Mathf.Clamp(data.MaxGroundAngle, 0.0f, 90.0f) * Mathf.Deg2Rad);
				}

				bool isGrounded = KCCPhysicsUtility.CheckGround(_collider.Collider, targetPosition, hit.Collider, hit.Transform, _settings.Radius, _settings.Height, _settings.Extent, minGroundDot, out Vector3 checkGroundNormal, out float checkGroundDistance, out bool isWithinExtent);
				if (isGrounded == true)
				{
					groundNormal   = checkGroundNormal;
					groundDistance = checkGroundDistance;

					data.IsGrounded = true;
				}

				if (hit.HasPenetration == false)
				{
					hit.IsGround       |= isGrounded;
					hit.IsWithinExtent |= isWithinExtent;
				}
			}

			if (data.IsGrounded == true)
			{
				data.GroundNormal   = groundNormal;
				data.GroundAngle    = Vector3.Angle(groundNormal, Vector3.up);
				data.GroundPosition = targetPosition + new Vector3(0.0f, _settings.Radius, 0.0f) - groundNormal * (_settings.Radius + groundDistance);
				data.GroundDistance = groundDistance;
			}

			return targetPosition;
		}

		private Vector3 DepenetrateMultiple(KCCOverlapInfo overlapInfo, KCCData data, Vector3 basePosition, Vector3 targetPosition, bool probeGrounding, int maxSubSteps)
		{
			float   minGroundDot        = Mathf.Cos(Mathf.Clamp(data.MaxGroundAngle, 0.0f, 90.0f) * Mathf.Deg2Rad);
			float   maxWallDot          = Mathf.Cos(Mathf.Clamp(90.0f - data.MaxWallAngle, 0.0f, 90.0f) * Mathf.Deg2Rad);
			int     groundColliders     = default;
			float   groundDistance      = default;
			float   maxGroundDot        = default;
			Vector3 maxGroundNormal     = default;
			Vector3 averageGroundNormal = default;
			Vector3 positionDelta       = targetPosition - basePosition;
			Vector3 positionDeltaXZ     = positionDelta.OnlyXZ();

			_resolver.Reset();

			for (int i = 0; i < overlapInfo.ColliderHitCount; ++i)
			{
				KCCOverlapHit hit = overlapInfo.ColliderHits[i];

				hit.HasPenetration = Physics.ComputePenetration(_collider.Collider, targetPosition, Quaternion.identity, hit.Collider, hit.Transform.position, hit.Transform.rotation, out Vector3 direction, out float distance);
				if (hit.HasPenetration == false)
					continue;

				hit.IsWithinExtent = true;

				float directionUpDot = Vector3.Dot(direction, Vector3.up);
				if (directionUpDot >= minGroundDot)
				{
					hit.IsGround = true;

					data.IsGrounded = true;

					++groundColliders;

					if (directionUpDot >= maxGroundDot)
					{
						maxGroundDot    = directionUpDot;
						maxGroundNormal = direction;
					}

					averageGroundNormal += direction * directionUpDot;
				}
				else
				{
					if (directionUpDot >= -maxWallDot)
					{
						if (directionUpDot <= maxWallDot)
						{
							hit.IsWall = true;
						}
						else
						{
							hit.IsSlope = true;
						}
					}

					if (directionUpDot > 0.0f && distance >= 0.000001f && data.DynamicVelocity.y <= 0.0f)
					{
						float movementDot = Vector3.Dot(positionDeltaXZ, direction.OnlyXZ());
						if (movementDot < 0.0f)
						{
							KCCPhysicsUtility.ProjectVerticalPenetration(ref direction, ref distance);
						}
					}
				}

				_resolver.AddCorrection(direction, distance);
			}

			int remainingSubSteps = Mathf.Max(0, maxSubSteps);

			float multiplier = 1.0f - Mathf.Min(remainingSubSteps, 2) * 0.25f;

			if (_resolver.Size == 2)
			{
				_resolver.GetCorrection(0, out Vector3 direction0);
				_resolver.GetCorrection(1, out Vector3 direction1);

				if (Vector3.Dot(direction0, direction1) >= 0.0f)
				{
					targetPosition += _resolver.CalculateMinMax() * multiplier;
				}
				else
				{
					targetPosition += _resolver.CalculateBinary() * multiplier;
				}
			}
			else
			{
				targetPosition += _resolver.CalculateGradientDescent(12, 0.0001f) * multiplier;
			}

			while (remainingSubSteps > 0)
			{
				--remainingSubSteps;

				_resolver.Reset();

				for (int i = 0; i < overlapInfo.ColliderHitCount; ++i)
				{
					KCCOverlapHit hit = overlapInfo.ColliderHits[i];

					bool hasPenetration = Physics.ComputePenetration(_collider.Collider, targetPosition, Quaternion.identity, hit.Collider, hit.Transform.position, hit.Transform.rotation, out Vector3 direction, out float distance);
					if (hasPenetration == false)
						continue;

					float directionUpDot = Vector3.Dot(direction, Vector3.up);

					if (hit.HasPenetration == false)
					{
						if (directionUpDot >= minGroundDot)
						{
							hit.IsGround = true;

							data.IsGrounded = true;

							++groundColliders;

							if (directionUpDot >= maxGroundDot)
							{
								maxGroundDot    = directionUpDot;
								maxGroundNormal = direction;
							}

							averageGroundNormal += direction * directionUpDot;
						}
						else
						{
							if (directionUpDot >= -maxWallDot)
							{
								if (directionUpDot <= maxWallDot)
								{
									hit.IsWall = true;
								}
								else
								{
									hit.IsSlope = true;
								}
							}
						}
					}

					hit.HasPenetration = true;
					hit.IsWithinExtent = true;

					if (directionUpDot < minGroundDot)
					{
						if (directionUpDot > 0.0f && distance >= 0.000001f && data.DynamicVelocity.y <= 0.0f)
						{
							float movementDot = Vector3.Dot(positionDeltaXZ, direction.OnlyXZ());
							if (movementDot < 0.0f)
							{
								KCCPhysicsUtility.ProjectVerticalPenetration(ref direction, ref distance);
							}
						}
					}

					_resolver.AddCorrection(direction, distance);
				}

				if (_resolver.Size == 0)
					break;

				if (remainingSubSteps == 0)
				{
					if (_resolver.Size == 2)
					{
						_resolver.GetCorrection(0, out Vector3 direction0);
						_resolver.GetCorrection(1, out Vector3 direction1);

						if (Vector3.Dot(direction0, direction1) >= 0.0f)
						{
							targetPosition += _resolver.CalculateGradientDescent(12, 0.0001f);
						}
						else
						{
							targetPosition += _resolver.CalculateBinary();
						}
					}
					else
					{
						targetPosition += _resolver.CalculateGradientDescent(12, 0.0001f);
					}
				}
				else if (remainingSubSteps == 1)
				{
					targetPosition += _resolver.CalculateMinMax() * 0.75f;
				}
				else
				{
					targetPosition += _resolver.CalculateMinMax() * 0.5f;
				}
			}

			if (probeGrounding == true && data.IsGrounded == false)
			{
				Vector3 closestGroundNormal   = Vector3.up;
				float   closestGroundDistance = 1000.0f;

				for (int i = 0; i < overlapInfo.ColliderHitCount; ++i)
				{
					KCCOverlapHit hit = overlapInfo.ColliderHits[i];

					bool isGrounded = KCCPhysicsUtility.CheckGround(_collider.Collider, targetPosition, hit.Collider, hit.Transform, _settings.Radius, _settings.Height, _settings.Extent, minGroundDot, out Vector3 checkGroundNormal, out float checkGroundDistance, out bool isWithinExtent);
					if (isGrounded == true)
					{
						data.IsGrounded = true;

						if (checkGroundDistance < closestGroundDistance)
						{
							closestGroundNormal   = checkGroundNormal;
							closestGroundDistance = checkGroundDistance;
						}
					}

					hit.IsGround       |= isGrounded;
					hit.IsWithinExtent |= isWithinExtent;
				}

				if (data.IsGrounded == true)
				{
					maxGroundNormal     = closestGroundNormal;
					averageGroundNormal = closestGroundNormal;
					groundDistance      = closestGroundDistance;
					groundColliders     = 1;
				}
			}

			if (data.IsGrounded == true)
			{
				if (groundColliders <= 1)
				{
					averageGroundNormal = maxGroundNormal;
				}
				else
				{
					averageGroundNormal.Normalize();
				}

				data.GroundNormal   = averageGroundNormal;
				data.GroundAngle    = Vector3.Angle(data.GroundNormal, Vector3.up);
				data.GroundPosition = targetPosition + new Vector3(0.0f, _settings.Radius, 0.0f) - data.GroundNormal * (_settings.Radius + groundDistance);
				data.GroundDistance = groundDistance;
			}

			return targetPosition;
		}

		private void CheckTriggersPenetration(KCCOverlapInfo overlapInfo, KCCData data)
		{
			for (int i = 0; i < overlapInfo.TriggerHitCount; ++i)
			{
				KCCOverlapHit hit = overlapInfo.TriggerHits[i];

				hit.HasPenetration = Physics.ComputePenetration(_collider.Collider, data.TargetPosition, Quaternion.identity, hit.Collider, hit.Transform.position, hit.Transform.rotation, out Vector3 direction, out float distance);
				hit.IsWithinExtent = hit.HasPenetration;
			}
		}

		private void TryStepUp(KCCOverlapInfo overlapInfo, KCCData data)
		{
			if (_activeFeatures.Has(EKCCFeature.StepUp) == false)
				return;
			if (_settings.StepHeight <= 0.0f)
				return;

			if (data.WasSteppingUp == true)
			{
				if (IsTouchingSlopeOrWall(overlapInfo) == false)
				{
					data.IsSteppingUp = false;
					return;
				}

				data.IsSteppingUp = true;
			}
			else
			{
				if (data.IsGrounded == false || data.GroundDistance > 0.001f)
				{
					data.IsSteppingUp = false;
					return;
				}

				if (IsTouchingSlopeOrWall(overlapInfo) == true)
				{
					data.IsSteppingUp = true;
				}
			}

			if (data.IsSteppingUp == true)
			{
				Vector3 basePosition    = data.BasePosition;
				Vector3 desiredPosition = data.DesiredPosition;
				Vector3 targetPosition  = data.TargetPosition;

				Vector3 desiredDelta     = desiredPosition - basePosition;
				Vector3 desiredDirection = Vector3.Normalize(desiredDelta);

				if (desiredDirection.IsZero() == true)
				{
					data.IsSteppingUp = false;
					return;
				}

				if (Vector3.Dot(desiredDirection, Vector3.down) >= 0.9f)
				{
					data.IsSteppingUp = false;
					return;
				}

				Vector3 correctionDirection = Vector3.Normalize(targetPosition - desiredPosition);

				if (Vector3.Dot(desiredDirection, correctionDirection) >= 0.0f)
				{
					data.IsSteppingUp = false;
					return;
				}

				if (correctionDirection.IsZero() == false)
				{
					Ray   ray   = new Ray(basePosition - desiredDelta * 2.0f, desiredDirection);
					Plane plane = new Plane(correctionDirection, targetPosition);

					if (plane.Raycast(ray, out float distance) == true)
					{
						targetPosition = ray.GetPoint(distance);
					}
				}

				Vector3 checkPosition = targetPosition + new Vector3(0.0f, _settings.StepHeight, 0.0f);

				if (OverlapCapsule(_sharedOverlapInfo, data, checkPosition, _settings.Radius, _settings.Height, 0.0f, _settings.CollisionLayerMask, QueryTriggerInteraction.Ignore) == true)
				{
					data.IsSteppingUp = false;
					return;
				}

				Vector3 desiredCheckDirectionXZ    = Vector3.Normalize(desiredDirection.OnlyXZ());
				Vector3 correctionCheckDirectionXZ = Vector3.Normalize(-correctionDirection.OnlyXZ());

				if (Vector3.Dot(desiredCheckDirectionXZ, correctionCheckDirectionXZ) < 0.1f)
				{
					data.IsSteppingUp = false;
					return;
				}

				Vector3 combinedCheckDirectionXZ = Vector3.Normalize(desiredCheckDirectionXZ + correctionCheckDirectionXZ);

				checkPosition += combinedCheckDirectionXZ * _settings.Radius;

				if (OverlapCapsule(_sharedOverlapInfo, data, checkPosition, _settings.Radius, _settings.Height, 0.0f, _settings.CollisionLayerMask, QueryTriggerInteraction.Ignore) == true)
				{
					data.IsSteppingUp = false;
					return;
				}

				float checkRadius   = _settings.Radius - _settings.Extent;
				float maxStepHeight = _settings.StepHeight;

				if (SphereCast(_raycastInfo, data, checkPosition + new Vector3(0.0f, checkRadius, 0.0f), Vector3.down, maxStepHeight, checkRadius, _settings.CollisionLayerMask, QueryTriggerInteraction.Ignore) == true)
				{
					Vector3 highestPoint = new Vector3(0.0f, float.MinValue, 0.0f);

					for (int i = 0, count = _raycastInfo.HitCount; i < count; ++i)
					{
						RaycastHit raycastHit = _raycastInfo.Hits[i];
						if (raycastHit.point.y > highestPoint.y)
						{
							highestPoint = raycastHit.point;
						}
					}

					maxStepHeight = Mathf.Clamp(maxStepHeight - (checkPosition.y - highestPoint.y) - _settings.Extent, 0.0f, _settings.StepHeight);
				}

				float desiredDistance   = Vector3.Distance(basePosition, desiredPosition);
				float travelledDistance = Vector3.Distance(basePosition, targetPosition);
				float remainingDistance = Mathf.Clamp((desiredDistance - travelledDistance) * _settings.StepSpeed, 0.0f, maxStepHeight);

				remainingDistance *= Mathf.Clamp01(Vector3.Dot(desiredDirection, -correctionDirection));

				data.TargetPosition = targetPosition + new Vector3(0.0f, remainingDistance, 0.0f);

				data.IsGrounded     = true;
				data.GroundNormal   = Vector3.up;
				data.GroundDistance = _settings.Extent;
				data.GroundPosition = data.TargetPosition;
				data.GroundTangent  = data.TransformDirection;
			}

			static bool IsTouchingSlopeOrWall(KCCOverlapInfo overlapInfo)
			{
				for (int i = 0; i < overlapInfo.ColliderHitCount; ++i)
				{
					KCCOverlapHit hit = overlapInfo.ColliderHits[i];

					if (hit.IsWithinExtent == true && (hit.IsSlope == true || hit.IsWall == true))
						return true;
				}

				return false;
			}
		}

		private void TrySnapToGround(KCCData data)
		{
			if (_activeFeatures.Has(EKCCFeature.SnapToGround) == false)
				return;
			if (_settings.GroundSnapDistance <= 0.0f)
				return;
			if (data.DynamicVelocity.y > 0.0f)
				return;

			float maxPenetrationDistance  = _settings.GroundSnapDistance;
			float maxStepPenetrationDelta = _settings.Radius * 0.25f;
			int   penetrationSteps        = Mathf.CeilToInt(maxPenetrationDistance / maxStepPenetrationDelta);
			float penetrationDelta        = maxPenetrationDistance / penetrationSteps;

			OverlapCapsule(_sharedOverlapInfo, data, data.TargetPosition - new Vector3(0.0f, _settings.GroundSnapDistance, 0.0f), _settings.Radius, _settings.Height + _settings.GroundSnapDistance, _settings.Radius, _settings.CollisionLayerMask, QueryTriggerInteraction.Ignore);

			if (_sharedOverlapInfo.ColliderHitCount == 0)
				return;

			_sharedOverlapInfo.ToggleConvexMeshes(false);

			Vector3 targetGroundedPosition   = data.TargetPosition;
			Vector3 penetrationPositionDelta = new Vector3(0.0f, -penetrationDelta, 0.0f);

			for (int i = 0; i < penetrationSteps; ++i)
			{
				targetGroundedPosition = DepenetrateColliders(_sharedOverlapInfo, data, targetGroundedPosition, targetGroundedPosition + penetrationPositionDelta, false, 0);

				if (data.IsGrounded == true)
				{
					float   maxSnapDelta   = _settings.GroundSnapSpeed * data.DeltaTime;
					Vector3 positionOffset = targetGroundedPosition - data.TargetPosition;
					Vector3 targetSnappedPosition;

					if (data.WasSnappingToGround == false)
					{
						maxSnapDelta *= 0.5f;
					}

					if (positionOffset.sqrMagnitude <= maxSnapDelta * maxSnapDelta)
					{
						targetSnappedPosition = targetGroundedPosition;
					}
					else
					{
						targetSnappedPosition = data.TargetPosition + positionOffset.normalized * maxSnapDelta;
					}

					if (_debug.ShowGroundSnapping == true && _debug.UseFixedData == IsInFixedUpdate)
					{
						UnityEngine.Debug.DrawLine(data.TargetPosition, data.TargetPosition + Vector3.up, Color.cyan, _debug.DisplayTime);
						UnityEngine.Debug.DrawLine(data.TargetPosition, targetGroundedPosition, Color.blue, _debug.DisplayTime);
						UnityEngine.Debug.DrawLine(data.TargetPosition, targetSnappedPosition, Color.red, _debug.DisplayTime);
					}

					data.TargetPosition     = targetSnappedPosition;
					data.GroundDistance     = Mathf.Max(0.0f, targetSnappedPosition.y - targetGroundedPosition.y);
					data.IsSnappingToGround = true;

					CalculateGroundProperties(data);

					break;
				}
			}

			_sharedOverlapInfo.ToggleConvexMeshes(true);
		}

		private static void CalculateGroundProperties(KCCData data)
		{
			if (KCCPhysicsUtility.ProjectOnGround(data.GroundNormal, data.GroundNormal.OnlyXZ(), out Vector3 projectedGroundNormal) == true)
			{
				data.GroundTangent = projectedGroundNormal.normalized;
				return;
			}

			if (KCCPhysicsUtility.ProjectOnGround(data.GroundNormal, data.DesiredVelocity.OnlyXZ(), out Vector3 projectedDesiredVelocity) == true)
			{
				data.GroundTangent = projectedDesiredVelocity.normalized;
				return;
			}

			data.GroundTangent = data.TransformDirection;
		}

		private bool OverlapCapsule(KCCOverlapInfo overlapInfo, KCCData data, Vector3 position, float radius, float height, float extent, LayerMask layerMask, QueryTriggerInteraction triggerInteraction)
		{
			overlapInfo.Reset(false);

			overlapInfo.Position           = position;
			overlapInfo.Radius             = radius;
			overlapInfo.Height             = height;
			overlapInfo.Extent             = extent;
			overlapInfo.LayerMask          = layerMask;
			overlapInfo.TriggerInteraction = triggerInteraction;

			Vector3 positionUp   = position + new Vector3(0.0f, height - radius, 0.0f);
			Vector3 positionDown = position + new Vector3(0.0f, radius, 0.0f);

			Collider   hitCollider;
			Collider[] hitColliders     = _hitColliders;
			int        hitColliderCount = GetPhysicsScene().OverlapCapsule(positionDown, positionUp, radius + extent, hitColliders, layerMask, triggerInteraction);

			for (int i = 0; i < hitColliderCount; ++i)
			{
				hitCollider = hitColliders[i];

				if (ResolvePhysicsQueryCollision(data, hitCollider) == true)
				{
					overlapInfo.AddHit(hitCollider);
				}
			}

			++_statistics.OverlapQueries;

			return overlapInfo.AllHitCount > 0;
		}

		private bool Raycast(KCCRaycastInfo raycastInfo, KCCData data, Vector3 origin, Vector3 direction, float maxDistance, LayerMask layerMask, QueryTriggerInteraction triggerInteraction)
		{
			raycastInfo.Reset(false);

			raycastInfo.Origin             = origin;
			raycastInfo.Direction          = direction;
			raycastInfo.MaxDistance        = maxDistance;
			raycastInfo.LayerMask          = layerMask;
			raycastInfo.TriggerInteraction = triggerInteraction;

			RaycastHit   raycastHit;
			RaycastHit[] raycastHits     = _raycastHits;
			int          raycastHitCount = GetPhysicsScene().Raycast(origin, direction, raycastHits, maxDistance, layerMask, triggerInteraction);

			for (int i = 0; i < raycastHitCount; ++i)
			{
				raycastHit = raycastHits[i];

				if (ResolvePhysicsQueryCollision(data, raycastHit.collider) == true)
				{
					raycastInfo.AddHit(raycastHit);
				}
			}

			++_statistics.RaycastQueries;

			return raycastInfo.HitCount > 0;
		}

		private bool SphereCast(KCCRaycastInfo raycastInfo, KCCData data, Vector3 origin, Vector3 direction, float maxDistance, float radius, LayerMask layerMask, QueryTriggerInteraction triggerInteraction)
		{
			raycastInfo.Reset(false);

			raycastInfo.Origin             = origin;
			raycastInfo.Direction          = direction;
			raycastInfo.MaxDistance        = maxDistance;
			raycastInfo.Radius             = radius;
			raycastInfo.LayerMask          = layerMask;
			raycastInfo.TriggerInteraction = triggerInteraction;

			RaycastHit   raycastHit;
			RaycastHit[] raycastHits     = _raycastHits;
			int          raycastHitCount = GetPhysicsScene().SphereCast(origin, radius, direction, raycastHits, maxDistance, layerMask, triggerInteraction);

			for (int i = 0; i < raycastHitCount; ++i)
			{
				raycastHit = raycastHits[i];

				if (ResolvePhysicsQueryCollision(data, raycastHit.collider) == true)
				{
					raycastInfo.AddHit(raycastHit);
				}
			}

			++_statistics.ShapecastQueries;

			return raycastInfo.HitCount > 0;
		}

		private void UpdateCollisions(KCCData data)
		{
			int addCollisionsCount    = 0;
			int removeCollisionsCount = 0;

			List<KCCCollision> collisions = data.Collisions.All;
			for (int i = 0, count = collisions.Count; i < count; ++i)
			{
				KCCCollision collision = collisions[i];

				_removeColliders[removeCollisionsCount]  = collision.Collider;
				_removeCollisions[removeCollisionsCount] = collision;

				++removeCollisionsCount;
			}

			KCCOverlapHit[] trackHits = _trackOverlapInfo.AllHits;
			for (int i = 0, count = _trackOverlapInfo.AllHitCount; i < count; ++i)
			{
				Collider trackCollider      = trackHits[i].Collider;
				bool     trackColliderFound = false;

				for (int j = 0; j < removeCollisionsCount; ++j)
				{
					if (object.ReferenceEquals(_removeColliders[j], trackCollider) == true)
					{
						trackColliderFound = true;

						--removeCollisionsCount;

						_removeColliders[j]  = _removeColliders[removeCollisionsCount];
						_removeCollisions[j] = _removeCollisions[removeCollisionsCount];

						break;
					}
				}

				if (trackColliderFound == false)
				{
					_addColliders[addCollisionsCount] = trackCollider;
					++addCollisionsCount;
				}
			}

			for (int i = 0; i < removeCollisionsCount; ++i)
			{
				RemoveCollision(data, _removeCollisions[i]);
			}

			for (int i = 0; i < addCollisionsCount; ++i)
			{
				AddCollision(data, _addColliders[i]);
			}
		}

		private void AddCollision(KCCData data, Collider collisionCollider)
		{
			GameObject collisionObject = collisionCollider.gameObject;

			NetworkObject networkObject = collisionObject.GetComponentNoAlloc<NetworkObject>();
			if (networkObject == null)
				return;

			IKCCInteractionProvider interactionProvider = collisionObject.GetComponentNoAlloc<IKCCInteractionProvider>();

			KCCCollision collision = data.Collisions.Add(networkObject, interactionProvider, collisionCollider);
			if (collision.Processor != null)
			{
				OnProcessorAdded(data, collision.Processor);
			}

			if (OnCollisionEnter != null)
			{
				try { OnCollisionEnter(this, collision); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
			}
		}

		private void RemoveCollision(KCCData data, KCCCollision collision)
		{
			if (OnCollisionExit != null)
			{
				try { OnCollisionExit(this, collision); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
			}

			if (collision.Processor != null)
			{
				OnProcessorRemoved(data, collision.Processor);
			}

			data.Collisions.Remove(collision);
		}

		private void RemoveAllCollisions(KCCData data)
		{
			List<KCCCollision> collisions = data.Collisions.All;
			while (collisions.Count > 0)
			{
				RemoveCollision(data, collisions[collisions.Count - 1]);
			}
		}

		private void RemoveModifier(KCCData data, KCCModifier modifier)
		{
			IKCCProcessor processor = modifier.Processor;

			if (data.Modifiers.Remove(modifier) == true)
			{
				if (processor != null)
				{
					OnProcessorRemoved(data, processor);
				}
			}
		}

		private void RemoveAllModifiers(KCCData data)
		{
			List<KCCModifier> modifiers = data.Modifiers.All;
			while (modifiers.Count > 0)
			{
				RemoveModifier(data, modifiers[modifiers.Count - 1]);
			}
		}

		private void OnProcessorAdded(KCCData data, IKCCProcessor processor)
		{
			try { processor.Enter(this, data); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
		}

		private void OnProcessorRemoved(KCCData data, IKCCProcessor processor)
		{
			if (_activeStage != EKCCStage.None)
			{
				SuppressProcessor(processor);
			}

			IKCCProcessor[] cachedProcessors = _cachedProcessors;

			for (int i = 0, count = _cachedProcessorCount; i < count; ++i)
			{
				if (cachedProcessors[i] == processor)
				{
					cachedProcessors[i] = null;
					break;
				}
			}

			try { processor.Exit(this, data); } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
		}

		private void SynchronizeTransform(KCCData data, bool synchronizePosition, bool synchronizeRotation)
		{
			if (synchronizePosition == true)
			{
				_rigidbody.position = data.TargetPosition;

				if (synchronizeRotation == true)
				{
					_transform.SetPositionAndRotation(data.TargetPosition, data.TransformRotation);
				}
				else
				{
					_transform.position = data.TargetPosition;
				}
			}
			else
			{
				if (synchronizeRotation == true)
				{
					_transform.rotation = data.TransformRotation;
				}
			}
		}

		private PhysicsScene GetPhysicsScene()
		{
			if(_driver == EKCCDriver.Fusion)
				return Runner.GetPhysicsScene();

			 Scene activeScene = SceneManager.GetActiveScene();
			 if (activeScene.IsValid() == true)
			 {
				PhysicsScene physicsScene = activeScene.GetPhysicsScene();
				if (physicsScene.IsValid() == true)
					return physicsScene;
			 }

			 return Physics.defaultPhysicsScene;
		}

		private bool ResolvePhysicsQueryCollision(KCCData data, Collider hitCollider)
		{
			if (hitCollider == _collider.Collider)
				return false;

			for (int i = 0, count = _childColliders.Count; i < count; ++i)
			{
				if (hitCollider == _childColliders[i])
					return false;
			}

			List<KCCIgnore> ignores = data.Ignores.All;
			for (int i = 0, count = ignores.Count; i < count; ++i)
			{
				if (hitCollider == ignores[i].Collider)
					return false;
			}

			if (ResolveCollision != null)
			{
				try
				{
					return ResolveCollision(this, hitCollider);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}

			return true;
		}

		private void RefreshCollider()
		{
			if (_settings.Shape == EKCCShape.None || _settings.Shape == EKCCShape.Void || (_settings.SpawnColliderOnProxy == false && HasAnyAuthority == false))
			{
				_collider.Destroy();
				return;
			}

			_settings.Radius = Mathf.Max(0.01f, _settings.Radius);
			_settings.Height = Mathf.Max(_settings.Radius * 2.0f, _settings.Height);

			_collider.Update(_transform, _settings);
		}

		private void RefreshUpdater()
		{
			if (_driver == EKCCDriver.Unity && _hasManualUpdate == false)
			{
				if (_updater == null)
				{
					_updater = gameObject.AddComponent<KCCUpdater>();
					_updater.Initialize(OnFixedUpdateInternal, OnRenderUpdateInternal);
				}
			}
			else
			{
				DestroyUpdater();
			}
		}

		private void DestroyUpdater()
		{
			if (_updater != null)
			{
				_updater.Deinitialize();
				Destroy(_updater);
			}

			_updater = null;
		}

		private void SetDefaults()
		{
			DestroyUpdater();

			LocalProcessors.Clear();

			_debug.SetDefaults();

			_fixedData.Clear();
			_renderData.Clear();
			_historyData.Clear();
			_transientData.Clear();
			_extendedOverlapInfo.Reset(true);
			_sharedOverlapInfo.Reset(true);
			_trackOverlapInfo.Reset(true);
			_raycastInfo.Reset(true);
			_childColliders.Clear();
			_raycastHits.Clear();
			_hitColliders.Clear();
			_addColliders.Clear();
			_removeColliders.Clear();
			_removeCollisions.Clear();
			_stageProcessors.Clear();
			_cachedProcessors.Clear();
			_cachedProcessorStages.Clear();

			_cachedProcessorCount = default;

			_collider.Destroy();

			_rigidbody.isKinematic   = true;
			_rigidbody.useGravity    = false;
			_rigidbody.interpolation = RigidbodyInterpolation.None;
			_rigidbody.constraints   = RigidbodyConstraints.FreezeAll;

			_settings.CopyFromOther(_defaultSettings);

			_driver             = EKCCDriver.None;
			_activeStage        = EKCCStage.None;
			_activeFeatures     = EKCCFeatures.None;
			_hasManualUpdate    = default;
			_hasInputAuthority  = default;
			_hasStateAuthority  = default;
			_lastRenderTime     = default;
			_lastRenderPosition = default;
			_predictionError    = default;
		}

		private void ProcessStage(EKCCStage stage, KCCData data, Action<IKCCProcessor, KCC, KCCData> method)
		{
			_activeStage = stage;

			bool traceProcessors = _debug.TraceStage == stage;
			if (traceProcessors == true)
			{
				_debug.ProcessorsStack.Clear();
			}

			Array.Copy(_cachedProcessors, _stageProcessors, _cachedProcessorCount);

			for (_stageProcessorIndex = 0; _stageProcessorIndex < _cachedProcessorCount; ++_stageProcessorIndex)
			{
				if (stage != EKCCStage.Stay && stage != EKCCStage.Interpolate)
				{
					if (_cachedProcessorStages[_stageProcessorIndex].Has(stage) == false)
						continue;
				}

				IKCCProcessor processor = _stageProcessors[_stageProcessorIndex];
				if (object.ReferenceEquals(processor, null) == true)
					continue;

				try
				{
					method(processor, this, data);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}

				if (traceProcessors == true)
				{
					_debug.ProcessorsStack.Add(processor);
				}
			}

			_activeStage = EKCCStage.None;
		}

		private void CacheProcessors(KCCData data)
		{
			_cachedProcessorCount = 0;

			List<KCCProcessor> settingsProcessors = _settings.Processors;
			for (int i = 0, processorCount = settingsProcessors.Count; i < processorCount; ++i)
			{
				IKCCProcessor processor = settingsProcessors[i];
				if (processor != null)
				{
					_cachedProcessors[_cachedProcessorCount] = processor;
					++_cachedProcessorCount;
				}
			}

			List<IKCCProcessor> localProcessors = LocalProcessors;
			for (int i = 0, processorCount = localProcessors.Count; i < processorCount; ++i)
			{
				IKCCProcessor processor = localProcessors[i];
				if (processor != null)
				{
					_cachedProcessors[_cachedProcessorCount] = processor;
					++_cachedProcessorCount;
				}
			}

			List<KCCModifier> modifiers = data.Modifiers.All;
			for (int i = 0, modifierCount = modifiers.Count; i < modifierCount; ++i)
			{
				IKCCProcessor processor = modifiers[i].Processor;
				if (processor != null)
				{
					_cachedProcessors[_cachedProcessorCount] = processor;
					++_cachedProcessorCount;
				}
			}

			List<KCCCollision> collisions = data.Collisions.All;
			for (int i = 0, count = collisions.Count; i < count; ++i)
			{
				IKCCProcessor processor = collisions[i].Processor;
				if (processor != null)
				{
					_cachedProcessors[_cachedProcessorCount] = processor;
					++_cachedProcessorCount;
				}
			}

			SortProcessors(_cachedProcessors, _cachedProcessorCount);

			for (int i = 0; i < _cachedProcessorCount; ++i)
			{
				_cachedProcessorStages[i] = _cachedProcessors[i].GetValidStages(this, data);
			}
		}

		private void PublishFixedData()
		{
			_renderData.CopyFromOther(_fixedData);

			KCCData historyData = _historyData[_fixedData.Tick % HISTORY_SIZE];
			if (historyData == null)
			{
				historyData = new KCCData();
				_historyData[_fixedData.Tick % HISTORY_SIZE] = historyData;
			}

			historyData.CopyFromOther(_fixedData);
		}

		private static void StoreTransientData(KCCData transientData, KCCData stateData)
		{
			transientData.ExternalVelocity     = stateData.ExternalVelocity;
			transientData.ExternalAcceleration = stateData.ExternalAcceleration;
			transientData.ExternalImpulse      = stateData.ExternalImpulse;
			transientData.ExternalForce        = stateData.ExternalForce;
			transientData.JumpImpulse          = stateData.JumpImpulse;
		}

		private static void RestoreTransientData(KCCData transientData, KCCData stateData)
		{
			stateData.ExternalVelocity     -= transientData.ExternalVelocity;
			stateData.ExternalAcceleration -= transientData.ExternalAcceleration;
			stateData.ExternalImpulse      -= transientData.ExternalImpulse;
			stateData.ExternalForce        -= transientData.ExternalForce;
			stateData.JumpImpulse          -= transientData.JumpImpulse;
		}

		private static void SortProcessors(IKCCProcessor[] processors, int count)
		{
			if (count <= 1)
				return;

			bool          isSorted = false;
			int           leftIndex;
			int           rightIndex;
			IKCCProcessor leftProcessor;
			IKCCProcessor rightProcessor;

			while (isSorted == false)
			{
				isSorted = true;

				leftIndex     = 0;
				rightIndex    = 1;
				leftProcessor = processors[leftIndex];

				while (rightIndex < count)
				{
					rightProcessor = processors[rightIndex];

					if (leftProcessor.Priority >= rightProcessor.Priority)
					{
						leftProcessor = rightProcessor;
					}
					else
					{
						processors[leftIndex]  = rightProcessor;
						processors[rightIndex] = leftProcessor;

						isSorted = false;
					}

					++leftIndex;
					++rightIndex;
				}
			}
		}
	}
}
