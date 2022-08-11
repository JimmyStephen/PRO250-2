namespace Projectiles
{
	using System;
	using Fusion;

	/// <summary>
	/// Helper component to invoke fixed and render update methods before HitboxManager
	/// </summary>
	[OrderBefore(typeof(HitboxManager))]
	public sealed class BeforeHitboxManagerUpdater : SimulationBehaviour
	{
		// PRIVATE MEMBERS

		private Action _fixedUpdate;
		private Action _render;

		// PUBLIC METHODS

		public void SetDelegates(Action fixedUpdateDelegate, Action renderDelegate)
		{
			_fixedUpdate = fixedUpdateDelegate;
			_render = renderDelegate;
		}

		// SimulationBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			_fixedUpdate?.Invoke();
		}

		public override void Render()
		{
			_render?.Invoke();
		}
	}
}
