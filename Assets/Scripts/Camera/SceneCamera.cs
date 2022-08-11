using UnityEngine;

namespace Projectiles
{
	public class SceneCamera : SceneService
	{
		// PUBLIC MEMBERS

		public Camera      Camera        => _camera;
		public ShakeEffect ShakeEffect   => _shakeEffect;

		// PRIVATE MEMBERS

		[SerializeField]
		private Camera _camera;
		[SerializeField]
		private ShakeEffect _shakeEffect;
	}
}
