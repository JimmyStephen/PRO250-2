namespace Projectiles
{
	using System.Collections.Generic;
	using UnityEngine;
	using Fusion;

	[RequireComponent(typeof(NetworkRunner))]
	[RequireComponent(typeof(NetworkEvents))]
	public sealed class GameManager : SimulationBehaviour, IPlayerJoined, IPlayerLeft
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Gameplay _gameplayPrefab;
		[SerializeField]
		private Player _playerPrefab;

		private Dictionary<PlayerRef, Player> _players = new Dictionary<PlayerRef, Player>(32);
		private bool _gameplaySpawned;

		// IPlayerJoined INTERFACE

		void IPlayerJoined.PlayerJoined(PlayerRef playerRef)
		{
			if (Runner.IsServer == false)
				return;

			if (_gameplaySpawned == false)
			{
				Runner.Spawn(_gameplayPrefab);
				_gameplaySpawned = true;
			}

			var player = Runner.Spawn(_playerPrefab, inputAuthority: playerRef);
			_players.Add(playerRef, player);

			Runner.SetPlayerObject(playerRef, player.Object);
		}

		// IPlayerLeft INTERFACE

		void IPlayerLeft.PlayerLeft(PlayerRef playerRef)
		{
			if (Runner.IsServer == false)
				return;

			if (_players.TryGetValue(playerRef, out Player player) == false)
				return;

			Runner.Despawn(player.Object);
		}
	}
}