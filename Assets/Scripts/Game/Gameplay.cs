﻿using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class Gameplay : ContextBehaviour
	{
		// PUBLIC MEMBERS

		[Networked, HideInInspector, Capacity(200)]
		public NetworkDictionary<PlayerRef, Player> Players { get; }


		// PRIVATE METHODS

		private SpawnPoint[] defaultSpawnpoints;
		private SpawnPoint[] _spawnPoints;
		private int _lastSpawnPoint = -1;

		private List<SpawnRequest> _spawnRequests = new List<SpawnRequest>();

        // PUBLIC METHODS

        //set the spawn points
        public void setSpawnpoints(SpawnPoint[] newSpawnpoints)
        {
			_spawnPoints = newSpawnpoints;
        }
		//reset spawnpoints
		public void resetSpawnPoints()
        {
			_spawnPoints = null;
        }

		public void Join(Player player)
		{
			if (Object.HasStateAuthority == false)
				return;

			var playerRef = player.Object.InputAuthority;

			if (Players.ContainsKey(playerRef) == true)
			{
				Debug.LogError($"Player {playerRef} already joined");
				return;
			}

			Players.Add(playerRef, player);

			OnPlayerJoined(player);
		}

		public void Leave(Player player)
		{
			if (Object.HasStateAuthority == false)
				return;

			if (Players.ContainsKey(player.Object.InputAuthority) == false)
				return;

			Players.Remove(player.Object.InputAuthority);

			OnPlayerLeft(player);
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			// Register to context
			Context.Gameplay = this;
		}

		public override void FixedUpdateNetwork()
		{
			if (Object.HasStateAuthority == false)
				return;

			int currentTick = Runner.Simulation.Tick;

			for (int i = _spawnRequests.Count - 1; i >= 0; i--)
			{
				var request = _spawnRequests[i];

				if (request.Tick > currentTick)
					continue;

				_spawnRequests.RemoveAt(i);

				if (request.Player == null || request.Player.Object == null)
					continue; // Player no longer valid

				if (Players.ContainsKey(request.Player.Object.InputAuthority) == false)
					continue; // Player left gameplay

				SpawnPlayerAgent(request.Player);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			// Clear from context
			Context.Gameplay = null;
		}

		// PROTECTED METHODS

		protected virtual void OnPlayerJoined(Player player)
		{
			SpawnPlayerAgent(player);
		}

		protected virtual void OnPlayerLeft(Player player)
		{
			DespawnPlayerAgent(player);
		}

		protected virtual void OnPlayerDeath(Player player)
		{
			AddSpawnRequest(player, 3f);
		}

		protected virtual void OnPlayerAgentSpawned(PlayerAgent agent)
		{
			agent.Health.SetImmortality(3f);
		}

		protected virtual void OnPlayerAgentDespawned(PlayerAgent agent)
		{
		}

		protected void SpawnPlayerAgent(Player player)
		{
			DespawnPlayerAgent(player);

			var agent = SpawnAgent(player.Object.InputAuthority, player.AgentPrefab) as PlayerAgent;
			player.AssignAgent(agent);

			agent.Health.FatalHitTaken += OnFatalHitTaken;
			OnPlayerAgentSpawned(agent);
		}

		protected void DespawnPlayerAgent(Player player)
		{
			if (player.ActiveAgent == null)
				return;

			player.ActiveAgent.Health.FatalHitTaken -= OnFatalHitTaken;

			OnPlayerAgentDespawned(player.ActiveAgent);

			DespawnAgent(player.ActiveAgent);
			player.ClearAgent();
		}

		protected void AddSpawnRequest(Player player, float spawnDelay)
		{
			int delayTicks = Mathf.RoundToInt(Runner.Simulation.Config.TickRate * spawnDelay);

			_spawnRequests.Add(new SpawnRequest()
			{
				Player = player,
				Tick = Runner.Simulation.Tick + delayTicks,
			});
		}

		// PRIVATE METHODS

		private void OnFatalHitTaken(HitData hitData)
		{
			var health = hitData.Target as Health;

			if (health == null)
				return;

			if (Players.TryGet(health.Object.InputAuthority, out Player player) == true)
			{
				OnPlayerDeath(player);
			}
		}

		bool firstRun = true;
		private Agent SpawnAgent(PlayerRef inputAuthority, Agent agentPrefab)
		{
            if (firstRun)
            {
				SpawnPoint[] temp = Runner.SimulationUnityScene.FindObjectsOfTypeInOrder<SpawnPoint>(true);
				List<SpawnPoint> temp2 = new List<SpawnPoint>();
				foreach(var v in temp)
                {
                    if (v.isActiveAndEnabled)
                    {
						temp2.Add(v);
                    }
                }
				defaultSpawnpoints = temp2.ToArray();
				_spawnPoints = defaultSpawnpoints;
				firstRun = false;
			}

			if (_spawnPoints == null || _spawnPoints.Length <= 0)
			{
				_spawnPoints = defaultSpawnpoints;
			}

			_lastSpawnPoint = (_lastSpawnPoint + 1) % _spawnPoints.Length;
			var spawnPoint = _spawnPoints[_lastSpawnPoint].transform;

			var agent = Runner.Spawn(agentPrefab, spawnPoint.position, spawnPoint.rotation, inputAuthority);
			return agent;
		}

		private void DespawnAgent(Agent agent)
		{
			if (agent == null)
				return;

			Runner.Despawn(agent.Object);
		}

		// HELPERS

		public struct SpawnRequest
		{
			public Player Player;
			public int Tick;
		}
	}
}