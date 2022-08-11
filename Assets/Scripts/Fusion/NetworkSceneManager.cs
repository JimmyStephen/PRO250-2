using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Fusion;
using UnityEngine.SceneManagement;

namespace Projectiles
{
	using UnityScene = UnityEngine.SceneManagement.Scene;

	// Copy of default NetworkSceneManager shipped with Fusion
	// Changes: Added scene initialization (see InitializeScene method)
	public class NetworkSceneManager : NetworkSceneManagerBase
	{
		// PRIVATE MEMBERS
		[Header("Single Peer Options")]
		[SerializeField]
		private int _postLoadDelayFrames = 1;

		// NetworkSceneManagerBase INTERFACE

		protected override IEnumerator SwitchScene(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished)
		{
			if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Single)
				return SwitchSceneSinglePeer(prevScene, newScene, finished);

			return SwitchSceneMultiplePeer(prevScene, newScene, finished);
		}

		protected override void Shutdown(NetworkRunner runner)
		{
			try
			{
				// Possible exception when runner tries to read config
				var scene = runner.SimulationUnityScene;

				if (scene.IsValid() == true)
				{
					var sceneObject = scene.GetComponent<Scene>(true);
					if (sceneObject != null)
					{
						sceneObject.Deinitialize();
					}
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}

			base.Shutdown(runner);
		}

		// PRIVATE METHODS

		private YieldInstruction LoadSceneAsync(SceneRef sceneRef, LoadSceneParameters parameters, Action<UnityScene> loaded)
		{
			if (TryGetScenePathFromBuildSettings(sceneRef, out var scenePath) == false)
			{
				throw new InvalidOperationException($"Not going to load {sceneRef}: unable to find the scene name");
			}

			var loadSceneOperation = SceneManager.LoadSceneAsync(scenePath, parameters);
			Assert.Check(loadSceneOperation);

			bool alreadyHandled = false;

			// if there's a better way to get scene struct more reliably I'm dying to know
			UnityAction<UnityScene, LoadSceneMode> sceneLoadedHandler = (scene, _) =>
			{
				if (IsScenePathOrNameEqual(scene, scenePath))
				{
					Assert.Check(!alreadyHandled);
					alreadyHandled = true;
					loaded(scene);
				}
			};

			SceneManager.sceneLoaded += sceneLoadedHandler;
			loadSceneOperation.completed += _ => { SceneManager.sceneLoaded -= sceneLoadedHandler; };

			return loadSceneOperation;
		}

		private YieldInstruction UnloadSceneAsync(UnityScene scene)
		{
			return SceneManager.UnloadSceneAsync(scene);
		}

		private IEnumerator SwitchSceneSinglePeer(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished)
		{
			UnityScene loadedScene;
			UnityScene activeScene = SceneManager.GetActiveScene();

			bool canTakeOverActiveScene = prevScene == default && IsScenePathOrNameEqual(activeScene, newScene);

			if (canTakeOverActiveScene)
			{
				LogTrace($"Not going to load initial scene {newScene} as this is the currently active scene");
				loadedScene = activeScene;
			}
			else
			{
				LogTrace($"Start loading scene {newScene} in single peer mode");
				var loadSceneParameters = new LoadSceneParameters(LoadSceneMode.Single);

				loadedScene = default;
				LogTrace($"Loading scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}");

				yield return LoadSceneAsync(newScene, loadSceneParameters, scene => loadedScene = scene);

				LogTrace(
					$"Loaded scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}: {loadedScene}");

				if (!loadedScene.IsValid())
				{
					throw new InvalidOperationException($"Failed to load scene {newScene}: async op failed");
				}
			}

			for (int i = _postLoadDelayFrames; i > 0; --i)
			{
				yield return null;
			}

			yield return InitializeScene(loadedScene);

			var sceneObjects = FindNetworkObjects(loadedScene, disable: true);
			finished(sceneObjects);
		}

		private IEnumerator SwitchSceneMultiplePeer(SceneRef prevScene, SceneRef newScene, FinishedLoadingDelegate finished)
		{
			UnityScene activeScene = SceneManager.GetActiveScene();

			bool canTakeOverActiveScene = prevScene == default && IsScenePathOrNameEqual(activeScene, newScene);

			LogTrace($"Start loading scene {newScene} in multi peer mode");
			var loadSceneParameters = new LoadSceneParameters(LoadSceneMode.Additive,
				NetworkProjectConfig.ConvertPhysicsMode(Runner.Config.PhysicsEngine));

			var sceneToUnload = Runner.MultiplePeerUnityScene;
			var tempSceneSpawnedPrefabs = Runner.IsMultiplePeerSceneTemp
				? sceneToUnload.GetRootGameObjects()
				: Array.Empty<GameObject>();

			if (canTakeOverActiveScene && NetworkRunner.GetRunnerForScene(activeScene) == null
			                           && SceneManager.sceneCount > 1)
			{
				LogTrace("Going to attempt to unload the initial scene as it needs a separate Physics stage");
				yield return UnloadSceneAsync(activeScene);
			}

			if (SceneManager.sceneCount == 1 && tempSceneSpawnedPrefabs.Length == 0)
			{
				// can load non-additively, stuff will simply get unloaded
				LogTrace("Only one scene remained, going to load non-additively");
				loadSceneParameters.loadSceneMode = LoadSceneMode.Single;
			}
			else if (sceneToUnload.IsValid())
			{
				// need a new temp scene here; otherwise calls to PhysicsStage will fail
				if (Runner.TryMultiplePeerAssignTempScene())
				{
					LogTrace($"Unloading previous scene: {sceneToUnload}, temp scene created");
					yield return UnloadSceneAsync(sceneToUnload);
				}
			}

			LogTrace($"Loading scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}");

			UnityScene loadedScene = default;
			yield return LoadSceneAsync(newScene, loadSceneParameters, scene => loadedScene = scene);

			LogTrace($"Loaded scene {newScene} with parameters: {JsonUtility.ToJson(loadSceneParameters)}: {loadedScene}");

			if (!loadedScene.IsValid())
			{
				throw new InvalidOperationException($"Failed to load scene {newScene}: async op failed");
			}

			var sceneObjects = FindNetworkObjects(loadedScene, disable: true, addVisibilityNodes: true);

			// unload temp scene
			var tempScene = Runner.MultiplePeerUnityScene;
			Runner.MultiplePeerUnityScene = loadedScene;
			if (tempScene.IsValid())
			{
				if (tempSceneSpawnedPrefabs.Length > 0)
				{
					LogTrace(
						$"Temp scene has {tempSceneSpawnedPrefabs.Length} spawned prefabs, need to move them to the loaded scene.");

					foreach (var go in tempSceneSpawnedPrefabs)
					{
						Assert.Check(go.GetComponent<NetworkObject>(),
							$"Expected {nameof(NetworkObject)} on a GameObject spawned on the temp scene {tempScene.name}");

						SceneManager.MoveGameObjectToScene(go, loadedScene);
					}
				}

				LogTrace($"Unloading temp scene {tempScene}");
				yield return UnloadSceneAsync(tempScene);
			}

			yield return InitializeScene(loadedScene);

			finished(sceneObjects);
		}

		// PRIVATE MEMBERS

		private IEnumerator InitializeScene(UnityScene scene)
		{
			var sceneObject = scene.GetComponent<Scene>(true);
			sceneObject.Context.Runner = Runner;
			sceneObject.Context.LocalPlayerRef = Runner.LocalPlayer;
			sceneObject.Context.ObservedPlayerRef = Runner.LocalPlayer;

			var objectPool = Runner.GetComponent<NetworkObjectPool>();
			objectPool.Context = sceneObject.Context;

			sceneObject.Initialize();

			yield return sceneObject.Activate();
		}
	}
}
