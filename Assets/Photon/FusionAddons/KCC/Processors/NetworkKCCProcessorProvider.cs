namespace Fusion.KCC
{
	using UnityEngine;

	/// <summary>
	/// Default NetworkKCCProcessor provider.
	/// </summary>
	[RequireComponent(typeof(NetworkObject))]
	public sealed class NetworkKCCProcessorProvider : MonoBehaviour, IKCCProcessorProvider
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private NetworkKCCProcessor _processor;

		// IKCCProcessorProvider INTERFACE

		IKCCProcessor IKCCProcessorProvider.GetProcessor()
		{
			return _processor;
		}
	}
}
