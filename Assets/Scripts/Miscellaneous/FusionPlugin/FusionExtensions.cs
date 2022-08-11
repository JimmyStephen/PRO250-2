namespace Fusion.Plugin
{
	public static class FusionExtensions
	{
		public static NetworkAreaOfInterestBehaviour GetAOIPositionSource(this NetworkObject networkObject)
		{
			return networkObject.AoiPositionSource;
		}

		public static double GetRTT(this Simulation simulation)
		{
			return simulation is Simulation.Client client ? client.RttToServer : 0.0;
		}
	}
}
