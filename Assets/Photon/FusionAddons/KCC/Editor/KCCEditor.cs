namespace Fusion.KCC.Editor
{
	using System.Collections.Generic;
	using UnityEngine;
	using UnityEditor;
	using Fusion.Editor;

	[CustomEditor(typeof(KCC), true)]
	public class KCCEditor : Editor
	{
		// PRIVATE MEMBERS

		private static bool _processorsStackFoldout  = true;
		private static bool _trackedCollidersFoldout = false;
		private static bool _collisionsFoldout       = false;
		private static bool _modifiersFoldout        = false;
		private static bool _ignoresFoldout          = false;

		private static EKCCStage[] _traceStages = new EKCCStage[]
		{
			EKCCStage.None,
			EKCCStage.SetInputProperties,
			EKCCStage.SetDynamicVelocity,
			EKCCStage.SetKinematicDirection,
			EKCCStage.SetKinematicTangent,
			EKCCStage.SetKinematicSpeed,
			EKCCStage.SetKinematicVelocity,
			EKCCStage.ProcessPhysicsQuery,
			EKCCStage.Stay,
			EKCCStage.Interpolate,
		};

		private static string[] _traceStageNames = new string[]
		{
			"None",
			"Set Input Properties",
			"Set Dynamic Velocity",
			"Set Kinematic Direction",
			"Set Kinematic Tangent",
			"Set Kinematic Speed",
			"Set Kinematic Velocity",
			"Process Physics Query",
			"Stay",
			"Interpolate",
		};

		// Editor INTERFACE

		public override bool RequiresConstantRepaint()
		{
			return true;
		}

		public override void OnInspectorGUI()
		{
			FusionEditorGUI.InjectPropertyDrawers(serializedObject);

			base.OnInspectorGUI();

			if (Application.isPlaying == false)
				return;

			KCC      kcc      = target as KCC;
			KCCDebug kccDebug = kcc.Debug;

			Color defaultColor             = GUI.color;
			Color defaultContentColor      = GUI.contentColor;
			Color defaultBackgroundColor   = GUI.backgroundColor;
			Color enabledBackgroundColor   = Color.green;
			Color disabledBackgroundColor  = defaultBackgroundColor;
			Color colliderBackgroundColor  = defaultBackgroundColor;
			Color processorBackgroundColor = Color.cyan;

			DrawLine(Color.gray);

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUILayout.BeginVertical();
				{
					if (DrawButton("Input Authority",   kcc.HasInputAuthority         == true, enabledBackgroundColor, disabledBackgroundColor) == true) {}
					if (DrawButton("Fixed Data",        kccDebug.UseFixedData         == true, Color.yellow,           disabledBackgroundColor) == true) { kccDebug.UseFixedData         = true;                           }
					if (DrawButton("Path",              kccDebug.ShowPath             == true, enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowPath             = !kccDebug.ShowPath;             }
					if (DrawButton("Ground Snapping",   kccDebug.ShowGroundSnapping   == true, enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowGroundSnapping   = !kccDebug.ShowGroundSnapping;   }
					if (DrawButton("Ground Normal",     kccDebug.ShowGroundNormal     == true, enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowGroundNormal     = !kccDebug.ShowGroundNormal;     }
					if (DrawButton("Kinematic Tangent", kccDebug.ShowKinematicTangent == true, enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowKinematicTangent = !kccDebug.ShowKinematicTangent; }
				}
				EditorGUILayout.EndVertical();
				EditorGUILayout.BeginVertical();
				{
					if (DrawButton("State Authority", kcc.HasStateAuthority      == true,  enabledBackgroundColor, disabledBackgroundColor) == true) {}
					if (DrawButton("Render Data",     kccDebug.UseFixedData      == false, Color.yellow,           disabledBackgroundColor) == true) { kccDebug.UseFixedData      = false;                       }
					if (DrawButton("Grounding",       kccDebug.ShowGrounding     == true,  enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowGrounding     = !kccDebug.ShowGrounding;     }
					if (DrawButton("Stepping Up",     kccDebug.ShowSteppingUp    == true,  enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowSteppingUp    = !kccDebug.ShowSteppingUp;    }
					if (DrawButton("Ground Tangent",  kccDebug.ShowGroundTangent == true,  enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.ShowGroundTangent = !kccDebug.ShowGroundTangent; }
					if (DrawButton("Logs",            kccDebug.EnableLogs        == true,  enabledBackgroundColor, disabledBackgroundColor) == true) { kccDebug.EnableLogs        = !kccDebug.EnableLogs;        }
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndHorizontal();

			if (kccDebug.ShowPath == true || kccDebug.ShowGrounding == true || kccDebug.ShowGroundSnapping == true || kccDebug.ShowGroundNormal == true || kccDebug.ShowKinematicTangent == true || kccDebug.ShowSteppingUp == true || kccDebug.ShowGroundTangent == true)
			{
				kccDebug.DisplayTime = EditorGUILayout.Slider("Display Time", kccDebug.DisplayTime, 1.0f, 60.0f);
			}

			GUI.backgroundColor = defaultBackgroundColor;

			DrawLine(Color.gray);

			KCCData data = kccDebug.UseFixedData == true ? kcc.FixedData : kcc.RenderData;

			EditorGUILayout.LabelField("Driver", kcc.Driver.ToString());
			EditorGUILayout.Toggle("Has Manual Update", kcc.HasManualUpdate);
			EditorGUILayout.Toggle("Was Grounded", data.WasGrounded);
			EditorGUILayout.Toggle("Is Grounded", data.IsGrounded);
			EditorGUILayout.Toggle("Is On Edge", data.IsOnEdge);
			EditorGUILayout.Toggle("Is Stepping Up", data.IsSteppingUp);
			EditorGUILayout.Toggle("Is Snapping To Ground", data.IsSnappingToGround);
			EditorGUILayout.LabelField("Look Pitch", data.LookPitch.ToString("0.00°"));
			EditorGUILayout.LabelField("Look Yaw", data.LookYaw.ToString("0.00°"));
			EditorGUILayout.LabelField("Real Speed", data.RealSpeed.ToString("0.00"));
			EditorGUILayout.LabelField("Ground Angle", data.GroundAngle.ToString("0.00°"));
			EditorGUILayout.LabelField("Ground Distance", data.IsGrounded == true ? data.GroundDistance.ToString("F6") : "N/A");
			EditorGUILayout.LabelField("Physics Contacts", kcc.PhysicsContacts.ToString());
			EditorGUILayout.LabelField("Collision Queries", $"{kcc.Statistics.OverlapQueries.ToString()} / {kcc.Statistics.RaycastQueries.ToString()} / {kcc.Statistics.ShapecastQueries.ToString()}");
			EditorGUILayout.LabelField("Prediction Error", kcc.PredictionError.magnitude.ToString("F6"));
			EditorGUILayout.EnumFlagsField("Active Features", kcc.ActiveFeatures);

			DrawLine(Color.gray);

			List<KCCModifier> modifiers = data.Modifiers.All;

			_modifiersFoldout = EditorGUILayout.Foldout(_modifiersFoldout, $"Networked Modifiers ({modifiers.Count})");
			if (_modifiersFoldout == true)
			{
				GUI.backgroundColor = processorBackgroundColor;

				for (int i = 0; i < modifiers.Count; ++i)
				{
					Component processor = modifiers[i].Processor as Component;
					Component provider  = modifiers[i].Provider  as Component;

					if (processor != null)
					{
						if (GUILayout.Button($"{processor.gameObject.name}\n({processor.GetType().Name})") == true)
						{
							EditorGUIUtility.PingObject(processor.gameObject);
						}
					}
					else if (provider != null)
					{
						if (GUILayout.Button($"{provider.gameObject.name}\n({provider.GetType().Name})") == true)
						{
							EditorGUIUtility.PingObject(provider.gameObject);
						}
					}
					else
					{
						if (GUILayout.Button($"{modifiers[i].NetworkObject.gameObject.name}") == true)
						{
							EditorGUIUtility.PingObject(modifiers[i].NetworkObject.gameObject);
						}
					}
				}

				GUI.backgroundColor = defaultBackgroundColor;
			}

			List<KCCCollision> collisions = data.Collisions.All;

			_collisionsFoldout = EditorGUILayout.Foldout(_collisionsFoldout, $"Networked Collisions ({collisions.Count})");
			if (_collisionsFoldout == true)
			{
				for (int i = 0; i < collisions.Count; ++i)
				{
					EditorGUILayout.BeginHorizontal();
					{
						GUI.backgroundColor = colliderBackgroundColor;

						if (GUILayout.Button($"{collisions[i].Collider.name}\n({collisions[i].Collider.GetType().Name})") == true)
						{
							EditorGUIUtility.PingObject(collisions[i].Collider.gameObject);
						}

						GUI.backgroundColor = processorBackgroundColor;

						Component processor = collisions[i].Processor as Component;
						Component provider  = collisions[i].Provider  as Component;

						if (processor != null)
						{
							if (GUILayout.Button($"{processor.gameObject.name}\n({processor.GetType().Name})") == true)
							{
								EditorGUIUtility.PingObject(processor.gameObject);
							}
						}
						else if (provider != null)
						{
							if (GUILayout.Button($"{provider.gameObject.name}\n({provider.GetType().Name})") == true)
							{
								EditorGUIUtility.PingObject(provider.gameObject);
							}
						}

						GUI.backgroundColor = defaultBackgroundColor;
					}
					EditorGUILayout.EndHorizontal();
				}
			}

			List<KCCIgnore> ignores = data.Ignores.All;

			_ignoresFoldout = EditorGUILayout.Foldout(_ignoresFoldout, $"Ignored Colliders ({ignores.Count})");
			if (_ignoresFoldout == true)
			{
				GUI.backgroundColor = colliderBackgroundColor;

				for (int i = 0; i < ignores.Count; ++i)
				{
					if (GUILayout.Button($"{ignores[i].Collider.name}\n({ignores[i].GetType().Name})") == true)
					{
						EditorGUIUtility.PingObject(ignores[i].Collider.gameObject);
					}
				}

				GUI.backgroundColor = defaultBackgroundColor;
			}

			_trackedCollidersFoldout = EditorGUILayout.Foldout(_trackedCollidersFoldout, $"Tracked Colliders ({kcc.PhysicsContacts})");
			if (_trackedCollidersFoldout == true)
			{
				GUI.backgroundColor = defaultBackgroundColor;

				foreach (Collider collider in kcc.GetTrackedColliders())
				{
					if (GUILayout.Button($"{collider.gameObject.name}\n({collider.GetType().Name})") == true)
					{
						EditorGUIUtility.PingObject(collider.gameObject);
					}
				}
			}

			DrawLine(Color.gray);

			int traceStageIndex = Mathf.Max(0, _traceStages.IndexOf(kccDebug.TraceStage));
			traceStageIndex = EditorGUILayout.Popup("Trace Stage", traceStageIndex, _traceStageNames);
			kccDebug.TraceStage = _traceStages[traceStageIndex];

			if (kccDebug.TraceStage != EKCCStage.None)
			{
				List<IKCCProcessor> processorsStack = kccDebug.ProcessorsStack;

				_processorsStackFoldout = EditorGUILayout.Foldout(_processorsStackFoldout, $"Processors Execution Stack ({processorsStack.Count})");
				if (_processorsStackFoldout == true)
				{
					GUI.backgroundColor = processorBackgroundColor;

					for (int i = 0; i < processorsStack.Count; ++i)
					{
						IKCCProcessor processor  = processorsStack[i];
						GameObject    gameObject = null;
						string        name       = "N/A";

						if (processor is Component processorComponent)
						{
							gameObject = processorComponent.gameObject;
							name       = gameObject.name;
						}

						if (GUILayout.Button($"{name}\n({processor.GetType().Name})") == true)
						{
							EditorGUIUtility.PingObject(gameObject);
						}
					}

					GUI.backgroundColor = defaultBackgroundColor;
				}
			}

			DrawLine(Color.gray);

			GUI.color           = defaultColor;
			GUI.contentColor    = defaultContentColor;
			GUI.backgroundColor = defaultBackgroundColor;
		}

		// PRIVATE METHODS

		public static void DrawLine(Color color, float thickness = 1.0f, float padding = 10.0f)
		{
			Rect controlRect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));

			controlRect.height = thickness;
			controlRect.y += padding * 0.5f;

			EditorGUI.DrawRect(controlRect, color);
		}

		private static bool DrawButton(string label, bool backgroundColorCondition, Color enabledBackgroundColor, Color disabledBackgroundColor)
		{
			GUI.backgroundColor = backgroundColorCondition == true ? enabledBackgroundColor : disabledBackgroundColor;
			return GUILayout.Button(label);
		}
	}
}
