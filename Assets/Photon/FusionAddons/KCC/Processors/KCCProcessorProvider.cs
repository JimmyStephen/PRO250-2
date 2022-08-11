namespace Fusion.KCC
{
	using UnityEngine;

	/// <summary>
	/// Default KCCProcessor provider.
	/// </summary>
	[RequireComponent(typeof(NetworkObject))]
	public sealed class KCCProcessorProvider : MonoBehaviour, IKCCProcessorProvider
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private KCCProcessor _processor;

		// IKCCProcessorProvider INTERFACE

		IKCCProcessor IKCCProcessorProvider.GetProcessor()
		{
			return _processor;
		}
	}
}
