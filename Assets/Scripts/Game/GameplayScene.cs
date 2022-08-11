using System.Collections;

namespace Projectiles
{
	public class GameplayScene : Scene
	{
		// Scene INTERFACE

		protected override void OnInitialize()
		{
			var contextBehaviours = Context.Runner.SimulationUnityScene.GetComponents<IContextBehaviour>(true);
			foreach (var behaviour in contextBehaviours)
			{
				behaviour.Context = Context;
			}

			base.OnInitialize();
		}

		protected override IEnumerator OnActivate()
		{
			AddService(Context.UI);

			yield return base.OnActivate();
		}

		protected override void OnTick()
		{
			// Validate simulation related objects before non-simulation services will try to access it
			ValidateSimulationContext();

			base.OnTick();
		}

		// PRIVATE METHODS

		private void ValidateSimulationContext()
		{
			var runner = Context.Runner;
			if (runner == null || runner.IsRunning == false)
			{
				Context.ObservedAgent = null;
				return;
			}

			var observedPlayer = Context.Runner.GetPlayerObject(Context.ObservedPlayerRef);
			Context.ObservedAgent = observedPlayer != null ? observedPlayer.GetComponent<Player>().ActiveAgent : null;
		}
	}
}
