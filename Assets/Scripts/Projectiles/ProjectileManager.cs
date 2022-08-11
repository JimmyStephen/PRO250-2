using System.Runtime.InteropServices;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	[StructLayout(LayoutKind.Explicit)]
	public struct ProjectileData : INetworkStruct
	{
		public bool    IsActive          { get { return _state.IsBitSet(0); } set { _state.SetBit(0, value); } }
		public bool    IsFinished        { get { return _state.IsBitSet(1); } set { _state.SetBit(1, value); } }

		[FieldOffset(0)]
		private byte   _state;

		[FieldOffset(1)]
		public byte    PrefabId;
		[FieldOffset(2)]
		public byte    WeaponAction;
		[FieldOffset(3)]
		public int     FireTick;
		[FieldOffset(7)]
		public Vector3 FirePosition;
		[FieldOffset(19)]
		public Vector3 FireVelocity;
		[FieldOffset(31)]
		public Vector3 ImpactPosition;
		[FieldOffset(43)]
		public Vector3 ImpactNormal;

		// Custom projectile data

		[FieldOffset(55)]
		public HomingData    Homing;
		[FieldOffset(55)]
		public KinematicData Kinematic;

		public struct HomingData : INetworkStruct
		{
			public NetworkId Target;
			public Vector3   TargetPosition; // Used for position prediction
			public Vector3   Position;
			public Vector3   Direction;
		}

		public struct KinematicData : INetworkStruct
		{
			public NetworkBool HasStopped;
			public Vector3     FinishedPosition;
			public int         StartTick;
			public byte        BounceCount;
		}
	}

	// Example of simpler ProjectileData struct that would be probably sufficient for many projects.
	// It is significantly smaller and allows usage of Accuracy attribute to save some bandwidth.
	// public struct ProjectileData : INetworkStruct
	// {
	// 	public bool    IsActive          { get { return _state.IsBitSet(0); } set { _state.SetBit(0, value); } }
	// 	public bool    IsFinished        { get { return _state.IsBitSet(1); } set { _state.SetBit(1, value); } }
	//
	// 	private byte   _state;
	//
	// 	public byte    PrefabId;
	// 	public byte    WeaponAction;
	// 	public int     FireTick;
	// 	public Vector3 FirePosition;
	// 	public Vector3 FireVelocity;
	// 	[Networked, Accuracy(0.01f)]
	// 	public Vector3 ImpactPosition { get; set; }
	// 	[Networked, Accuracy(0.01f)]
	// 	public Vector3 ImpactNormal { get; set; }
	// }

	public struct ProjectileInterpolationData
	{
		public ProjectileData From;
		public ProjectileData To;
		public float Alpha;
	}

	public class ProjectileContext
	{
		public NetworkRunner Runner;
		public ObjectCache   Cache;
		public PlayerRef     InputAuthority;
		public int           OwnerObjectInstanceID;

		// Barrel transform represents position from which projectile visuals should fly out
		// (actual projectile fire calculations are usually done from different point, for example camera)
		public Transform     BarrelTransform;

		public float         FloatTick;
		public bool          Interpolate;
		public ProjectileInterpolationData InterpolationData;
	}

	public interface IProjectileManager
	{
		public void AddProjectile(Projectile projectilePrefab, Vector3 firePosition, Vector3 direction, byte weaponAction = 0);
		public StandaloneProjectile AddProjectile(StandaloneProjectile projectilePrefab, Vector3 firePosition, Vector3 direction, NetworkObjectPredictionKey? predictionKey, byte weaponAction = 0);
	}

	public class ProjectileManager : ContextBehaviour, IProjectileManager
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private bool _fullProxyPrediction = false;
		[SerializeField]
		private Projectile[] _projectilePrefabs;

		[Networked, Capacity(96)]
		private NetworkArray<ProjectileData> _projectiles { get; }
		[Networked]
		private int _projectileCount { get; set; }

		private Projectile[] _visibleProjectiles = new Projectile[128];
		private int _visibleProjectileCount;

		private ProjectileContext _projectileContext;

		private RawInterpolator _projectilesInterpolator;

		// PUBLIC MEMBERS

		public void AddProjectile(Projectile projectilePrefab, Vector3 firePosition, Vector3 direction, byte weaponAction = 0)
		{
			var fireData = projectilePrefab.GetFireData(Runner, firePosition, direction);
			AddProjectile(projectilePrefab, fireData, weaponAction);
		}
		
		public void AddProjectile(Projectile projectilePrefab, ProjectileData data, byte weaponAction = 0)
		{
			int prefabIndex = _projectilePrefabs.IndexOf(projectilePrefab);

			if (prefabIndex < 0)
			{
				Debug.LogError($"Projectile {projectilePrefab} not found. Add it in ProjectileManager prefab array.");
				return;
			}

			data.PrefabId     = (byte)prefabIndex;
			data.FireTick     = Runner.Simulation.Tick;
			data.IsActive     = true;
			data.WeaponAction = weaponAction;

			int projectileIndex = _projectileCount % _projectiles.Length;

			var previousData = _projectiles[projectileIndex];
			if (previousData.IsActive == true && previousData.IsFinished == false)
			{
				Debug.LogError("No space for another projectile - projectile buffer should be increased or projectile lives too long");
			}

			_projectiles.Set(projectileIndex, data);

			_projectileCount++;
		}
		
		public StandaloneProjectile AddProjectile(StandaloneProjectile projectilePrefab, Vector3 firePosition, Vector3 direction, NetworkObjectPredictionKey? predictionKey, byte weaponAction = 0)
		{
			// StandaloneProjectiles is just spawned, nothing else is needed

			var projectile = Runner.Spawn(projectilePrefab, firePosition, Quaternion.LookRotation(direction), Object.InputAuthority, BeforeStandaloneProjectileSpawned, predictionKey);

			void BeforeStandaloneProjectileSpawned(NetworkRunner runner, NetworkObject spawnedObject)
			{
				if (Object.HasStateAuthority == true)
					return;

				var projectile = spawnedObject.GetComponent<StandaloneProjectile>();
				projectile.PredictedInputAuthority = Object.InputAuthority;
			}

			return projectile;
		}

		public void OnSpawned()
		{
			_visibleProjectileCount = _projectileCount;

			_projectileContext = new ProjectileContext()
			{
				Runner                = Runner,
				Cache                 = Context.ObjectCache,
				InputAuthority        = Object.InputAuthority,
				OwnerObjectInstanceID = gameObject.GetInstanceID(),
			};

			_projectilesInterpolator = GetInterpolator(nameof(_projectiles));
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			for (int i = 0; i < _visibleProjectiles.Length; i++)
			{
				var projectile = _visibleProjectiles[i];
				if (projectile != null)
				{
					DestroyProjectile(projectile);
					_visibleProjectiles[i] = null;
				}
			}
		}

		public void OnFixedUpdate()
		{
			// Projectile calculations are processed only on input or state authority
			// unless full proxy prediction is turned on
			if (Object.IsProxy == true && _fullProxyPrediction == false)
				return;

			_projectileContext.FloatTick = Runner.Simulation.Tick;

			for (int i = 0; i < _projectiles.Length; i++)
			{
				var projectileData = _projectiles[i];

				if (projectileData.IsActive == false)
					continue;
				if (projectileData.IsFinished == true)
					continue;

				var prefab = _projectilePrefabs[projectileData.PrefabId];
				prefab.OnFixedUpdate(_projectileContext, ref projectileData);

				_projectiles.Set(i, projectileData);
			}
		}

		public void OnRender(Transform[] weaponBarrelTransforms)
		{
			// Visuals are not spawned on dedicated server at all
			if (Runner.Mode == SimulationModes.Server)
				return;

			// Projectiles are not updated on hidden clients in multi-peer mode
			// (disabled by RunnerVisibilityNode on same object)
			if (enabled == false)
				return;

			RenderProjectiles(weaponBarrelTransforms);
		}

		// PRIVATE MEMBERS
		
		private void RenderProjectiles(Transform[] weaponBarrelTransforms)
		{
			_projectilesInterpolator.TryGetArray(_projectiles, out var fromProjectiles, out var toProjectiles, out float interpolationAlpha);

			var simulation = Runner.Simulation;
			bool interpolate = Object.IsProxy == true && _fullProxyPrediction == false;

			if (interpolate == true)
			{
				// For proxies we move projectile in snapshot interpolated time
				_projectileContext.FloatTick = simulation.InterpFrom.Tick + (simulation.InterpTo.Tick - simulation.InterpFrom.Tick) * simulation.InterpAlpha;
			}
			else
			{
				_projectileContext.FloatTick = simulation.Tick + simulation.StateAlpha;
			}

			int barrelTransformCount = weaponBarrelTransforms.Length;
			int bufferLength = _projectiles.Length;

			// Our predicted projectiles were not confirmed by the server, let's discard them
			for (int i = _projectileCount; i < _visibleProjectileCount; i++)
			{
				var projectile = _visibleProjectiles[i % bufferLength];
				if (projectile != null)
				{
					// We are not destroying projectile immediately,
					// projectile can decide itself how to react
					projectile.Discard();
				}
			}

			int minFireTick = Runner.Simulation.Tick - (int)(Runner.Simulation.Config.TickRate * 0.5f);

			// Let's spawn missing projectile gameobjects
			for (int i = _visibleProjectileCount; i < _projectileCount; i++)
			{
				int index = i % bufferLength;
				var projectileData = _projectiles[index];

				// Projectile is long time finished, do not spawn visuals for it
				// Note: We cannot check just IsFinished, because some projectiles are finished
				// immediately in one tick but the visuals could be longer running
				if (projectileData.IsFinished == true && projectileData.FireTick < minFireTick)
					continue;

				if (_visibleProjectiles[index] != null)
				{
					Debug.LogError("No space for another projectile gameobject - projectile buffer should be increased or projectile lives too long");
					DestroyProjectile(_visibleProjectiles[index]);
				}

				byte weaponAction = projectileData.WeaponAction;
				if (weaponAction >= barrelTransformCount)
				{
					Debug.LogError($"Create: Barrel transform with index {weaponAction} not present");
					weaponAction = 0;
				}

				_projectileContext.BarrelTransform = weaponBarrelTransforms[weaponAction];
				_visibleProjectiles[index] = CreateProjectile(_projectileContext, ref projectileData);
			}

			// Update all visible projectiles
			for (int i = 0; i < bufferLength; i++)
			{
				var projectile = _visibleProjectiles[i];
				if (projectile == null)
					continue;

				var data = _projectiles[i];

				if (data.PrefabId != projectile.PrefabId)
				{
					Debug.LogError($"Incorrect spawned prefab. Should be {data.PrefabId}, actual {projectile.PrefabId}. This should not happen.");
					DestroyProjectile(projectile);
					_visibleProjectiles[i] = null;
					continue;
				}

				bool interpolateProjectile = interpolate == true && projectile.NeedsInterpolationData;

				// Prepare interpolation data if needed
				ProjectileInterpolationData interpolationData = default;
				if (interpolateProjectile == true)
				{
					interpolationData.From = fromProjectiles.Get(i);
					interpolationData.To = toProjectiles.Get(i);
					interpolationData.Alpha = interpolationAlpha;
				}

				_projectileContext.Interpolate = interpolateProjectile;
				_projectileContext.InterpolationData = interpolationData;

				// If barrel transform is not available anymore (e.g. weapon was switched before projectile finished)
				// let's use at least some dummy (first) one. Doesn't matter at this point much.
				int barrelTransformIndex = data.WeaponAction < barrelTransformCount ? data.WeaponAction : 0;
				_projectileContext.BarrelTransform = weaponBarrelTransforms[barrelTransformIndex];

				projectile.OnRender(_projectileContext, ref data);

				if (projectile.IsFinished == true)
				{
					DestroyProjectile(projectile);
					_visibleProjectiles[i] = null;
				}
			}

			_visibleProjectileCount = _projectileCount;
		}

		private Projectile CreateProjectile(ProjectileContext context, ref ProjectileData data)
		{
			var projectile = Context.ObjectCache.Get(_projectilePrefabs[data.PrefabId]);
			Runner.MoveToRunnerScene(projectile);

			projectile.Activate(context, ref data);

			return projectile;
		}

		private void DestroyProjectile(Projectile projectile)
		{
			projectile.Deactivate(_projectileContext);

			Context.ObjectCache.Return(projectile.gameObject);
		}

		private void LogMessage(string message)
		{
			Debug.Log($"{Runner.name} (tick: {Runner.Simulation.Tick}, frame: {Time.frameCount}) - {message}");
		}
	}
}
