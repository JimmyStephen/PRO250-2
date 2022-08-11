using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	// Weapon action is a set of components that represent one weapon action
	// E.g. one weapon action will be a standard fire, second will be an alternative fire
	[DisallowMultipleComponent]
	public sealed class WeaponAction : NetworkBehaviour
	{
		// PUBLIC MEMBERS

		public Transform             BarrelTransform => _barrelTransform;
		public List<WeaponComponent> Components      => _components;

		public string                Description     => _description;

		// PRIVATE MEMBERS

		[SerializeField]
		private Transform _barrelTransform;
		[SerializeField]
		private string _description;
		[SerializeField, Tooltip("Can be used in special cases where sharing of same component between multiple weapon actions is needed (e.g. shared magazine component")]
		private WeaponComponent[] _sharedComponents;

		[Networked]
		private int _lastFireTick { get; set; }

		private int _lastVisibleFireTick;

		private List<WeaponComponent> _components = new(16);

		// PUBLIC METHODS

		public void Initialize(Weapon weapon, byte id)
		{
			_components.Clear();

			transform.GetComponents(_components);

			for (int i = 0; i < _sharedComponents.Length; i++)
			{
				var component = _sharedComponents[i];

				Assert.Check(_components.Contains(component) == false, $"Component {component.name} is already present in weapon components and should not be present in shared components");
				_components.Add(component);
			}

			_components.Sort((a, b) => b.Priority - a.Priority);

			for (int i = 0; i < _components.Count; i++)
			{
				var component = _components[i];

				component.Weapon = weapon;
				component.WeaponActionId = id;
				component.BarrelTransform = _barrelTransform;
			}
		}

		public void ProcessInput(WeaponContext context, bool weaponIsBusy)
		{
			WeaponDesires desires = default;

			for (int j = 0; j < _components.Count; j++)
			{
				_components[j].ProcessInput(context, ref desires, weaponIsBusy);
			}

			for (int j = 0; j < _components.Count; j++)
			{
				_components[j].OnFixedUpdate(context, desires);
			}
		}

		public void OnRender(WeaponContext context)
		{
			WeaponDesires desires = default;

			// Fire is shown only if it happened 0.5 seconds in the past or less (relevant mainly with interest management)
			desires.HasFired = _lastVisibleFireTick < _lastFireTick && _lastFireTick > Runner.Simulation.Tick - (int)(Runner.Simulation.Config.TickRate * 0.5f);

			for (int j = 0; j < _components.Count; j++)
			{
				_components[j].OnRender(context, ref desires);
			}

			_lastVisibleFireTick = _lastFireTick;
		}

		public bool IsBusy()
		{
			for (int j = 0; j < _components.Count; j++)
			{
				if (_components[j].IsBusy == true)
					return true;
			}

			return false;
		}

		public void HasFired()
		{
			_lastFireTick = Runner.Simulation.Tick;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			// Sync fire tick
			_lastVisibleFireTick = _lastFireTick;
		}
	}
}
