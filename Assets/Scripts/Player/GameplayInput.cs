using Fusion;
using UnityEngine;

namespace Projectiles
{
	public enum EInputButtons
	{
		Fire     = 0,
		AltFire  = 1,
		Jump     = 2,
		Reload   = 3,
	}

	public struct GameplayInput : INetworkInput
	{
		public int            WeaponSlot => WeaponButton - 1;

		public Vector2        MoveDirection;
		public Vector2        LookRotationDelta;
		public byte           WeaponButton;
		public NetworkButtons Buttons;
	}
}
