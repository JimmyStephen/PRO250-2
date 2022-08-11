using Fusion;
using UnityEngine;

namespace Projectiles
{
	// Standalone (spawned) projectile that acts as a container for ProjectileData and updates 
	// standard Projectile script in a similar manner as ProjectileManager does for projectile data buffer
	// Note: Should be used only in special cases (e.g. very long living projectiles),
	// otherwise projectile data buffer is much better solution
	public sealed class DataStandaloneProjectile : StandaloneProjectile
	{
		// PUBLIC MEMBERS
		
		public bool IsPredicted => Object == null || Object.IsPredictedSpawn;
		public ProjectileData Data { get { return IsPredicted ? _data_Local : _data_Networked; } set { if (IsPredicted == true) _data_Local = value; else _data_Networked = value; } }
		
		// PRIVATE MEMBERS
		
		[SerializeField]
		private Projectile _projectileVisual;
		[SerializeField]
		private bool _fullProxyPrediction = false;
		
		[Networked]
		private ProjectileData _data_Networked { get; set; }
		private ProjectileData _data_Local;

		[Networked]
		private Vector3 _barrelOffset { get; set; }

		private Transform _dummyBarrelTransform;
		
		private ProjectileContext _projectileContext = new ProjectileContext();
		private RawInterpolator _interpolator;
		
		private bool _isActivated;
		
		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			// Object.IsProxy is not valid for predicted objects
			bool isProxy = IsPredicted == false && Object.IsProxy;

			if (isProxy == true)
			{
				_interpolator = GetInterpolator(nameof(_data_Networked));
			}

			// Object.InputAuthority is not valid for predicted objects
			var inputAuthority = IsPredicted == true ? PredictedInputAuthority : Object.InputAuthority;

			_projectileContext.Runner                = Runner;
			_projectileContext.Cache                 = Context.ObjectCache;
			_projectileContext.InputAuthority        = inputAuthority;
			_projectileContext.OwnerObjectInstanceID = GetOwnerObjectInstanceID(inputAuthority);
			_projectileContext.FloatTick             = Runner.Simulation.Tick;
			
			if (isProxy == true)
			{
				_dummyBarrelTransform.position = transform.position + _barrelOffset;
			}

			// Setting real weapon transform is not safe for standalone projectiles as that can get returned to cache, be destroyed etc.
			// This object is not moving, so it is good enough substitude to use dummy barrel transform child.
			_projectileContext.BarrelTransform = _dummyBarrelTransform;

			var data = _projectileVisual.GetFireData(Runner, transform.position, transform.forward);
			
			data.FireTick = Runner.Simulation.Tick;
			data.IsActive = true;
			
			Data = data;

			_projectileVisual.SetActive(false);
			_isActivated = false;
		}

		public override void FixedUpdateNetwork()
		{
			// Projectile calculations are processed only on input or state authority
			// unless full proxy prediction is turned on
			if (Object.IsProxy == true && _fullProxyPrediction == false)
				return;
			
			_projectileContext.FloatTick = Runner.Simulation.Tick;
			
			var data = Data;
			_projectileVisual.OnFixedUpdate(_projectileContext, ref data);
			Data = data;
			
			if (data.IsFinished == true)
			{
				Runner.Despawn(Object);
			}
		}

		public override void Render()
		{
			// Visual is not updated on dedicated server at all
			if (Runner.Mode == SimulationModes.Server)
				return;
			
			var data = Data;
			
			if (_isActivated == false)
			{
				_projectileVisual.SetActive(true);
				_projectileVisual.Activate(_projectileContext, ref data);
				_isActivated = true;
			}
			
			var simulation = Runner.Simulation;
			bool interpolate = IsPredicted == false && Object.IsProxy == true && _fullProxyPrediction == false;

			if (interpolate == true)
			{
				// For proxies we move projectile in snapshot interpolated time
				_projectileContext.FloatTick = simulation.InterpFrom.Tick + (simulation.InterpTo.Tick - simulation.InterpFrom.Tick) * simulation.InterpAlpha;
			}
			else
			{
				_projectileContext.FloatTick = simulation.Tick + simulation.StateAlpha;
			}

			bool interpolateProjectile = interpolate == true && _projectileVisual.NeedsInterpolationData;

			// Prepare interpolation data if needed
			ProjectileInterpolationData interpolationData = default;
			if (interpolateProjectile == true)
			{
				_interpolator.TryGetStruct(out ProjectileData fromData, out ProjectileData toData, out float alpha);

				interpolationData.From = fromData;
				interpolationData.To = toData;
				interpolationData.Alpha = alpha;
			}

			_projectileContext.Interpolate = interpolateProjectile;
			_projectileContext.InterpolationData = interpolationData;
			
			_projectileVisual.OnRender(_projectileContext, ref data);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (_isActivated == true)
			{
				_projectileVisual.Deactivate(_projectileContext);
				_isActivated = false;
			}

			_dummyBarrelTransform.localPosition = Vector3.zero;
		}
		
		// StandaloneProjectile INTERFACE

		public override void SetWeaponBarrelPosition(Vector3 position)
		{
			_dummyBarrelTransform.position = position;

			if (IsPredicted == false)
			{
				_barrelOffset = position - transform.position;
			}
		}

		// MONOBEHAVIOUR
		
		private void Awake()
		{
			if (_projectileVisual.transform == transform)
			{
				Debug.LogError("Projectile visual must be child of standalone projectile object");
			}

			_dummyBarrelTransform = new GameObject("DummyBarrelTransform").transform;
			_dummyBarrelTransform.parent = transform;
		}
		
		// PRIVATE METHODS
		
		private int GetOwnerObjectInstanceID(PlayerRef ownerRef)
		{
			var playerObject = Runner.GetPlayerObject(ownerRef);
			
			if (playerObject == null)
				return 0;
			
			var agent = playerObject.GetComponent<Player>().ActiveAgent;
			return agent != null ? agent.gameObject.GetInstanceID() : 0;
		}
	}
}
