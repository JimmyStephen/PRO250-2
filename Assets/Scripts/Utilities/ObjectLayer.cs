using UnityEngine;

namespace Projectiles
{
	public static class ObjectLayer
	{
		public static int Default      { get; private set; }
		public static int Agent        { get; private set; }
		public static int Target       { get; private set; }
		public static int KCC          { get; private set; }
		public static int FirstPerson  { get; private set; }
		public static int ThirdPerson  { get; private set; }
		public static int Interaction  { get; private set; }

		static ObjectLayer()
		{
			Default          = LayerMask.NameToLayer("Default");
			Agent            = LayerMask.NameToLayer("Agent");
			Target           = LayerMask.NameToLayer("Target");
			KCC              = LayerMask.NameToLayer("KCC");
			FirstPerson      = LayerMask.NameToLayer("FirstPerson");
			ThirdPerson      = LayerMask.NameToLayer("ThirdPerson");
			Interaction      = LayerMask.NameToLayer("Interaction");
		}
	}

	public static class ObjectLayerMask
	{
		public static LayerMask Default              { get; private set; }
		public static LayerMask Agent                { get; private set; }
		public static LayerMask Target               { get; private set; }
		public static LayerMask HitTargets           { get; private set; }
		public static LayerMask BlockingProjectiles  { get; private set; }
		public static LayerMask Environment          { get; private set; }

		static ObjectLayerMask()
		{
			Default     = 1 << ObjectLayer.Default;
			Agent       = 1 << ObjectLayer.Agent;
			Target      = 1 << ObjectLayer.Target;

			Environment = Default;
			HitTargets = Agent | Target;
			BlockingProjectiles  = Default | Agent | Target;
		}
	}
}
