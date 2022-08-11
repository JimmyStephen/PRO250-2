namespace Fusion.KCC
{
	using UnityEngine;

	/// <summary>
	/// Default NetworkAoIKCCProcessor provider.
	/// </summary>
	[RequireComponent(typeof(NetworkObject))]
	public sealed class NetworkAoIKCCProcessorProvider : MonoBehaviour, IKCCProcessorProvider
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private NetworkAoIKCCProcessor _processor;

		// IKCCProcessorProvider INTERFACE

		IKCCProcessor IKCCProcessorProvider.GetProcessor()
		{
			return _processor;
		}
	}
}
