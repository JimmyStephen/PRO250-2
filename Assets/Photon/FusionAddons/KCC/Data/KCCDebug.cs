namespace Fusion.KCC
{
	using System.Collections.Generic;
	using System.Text;
	using UnityEngine;

	/// <summary>
	/// Debug configuration, logging, tracking, visualization.
	/// </summary>
	public sealed class KCCDebug
	{
		// PUBLIC MEMBERS

		public EKCCStage TraceStage;
		public bool      UseFixedData;
		public bool      EnableLogs;
		public bool      ShowPath;
		public bool      ShowGrounding;
		public bool      ShowSteppingUp;
		public bool      ShowGroundSnapping;
		public bool      ShowGroundNormal;
		public bool      ShowGroundTangent;
		public bool      ShowKinematicTangent;
		public float     DisplayTime = 10.0f;

		public readonly List<IKCCProcessor> ProcessorsStack = new List<IKCCProcessor>();

		// PRIVATE MEMBERS

		public StringBuilder _stringBuilder = new StringBuilder(1024);

		// PUBLIC METHODS

		public void SetDefaults()
		{
			TraceStage           = EKCCStage.None;
			UseFixedData         = true;
			EnableLogs           = false;
			ShowPath             = false;
			ShowGrounding        = false;
			ShowSteppingUp       = false;
			ShowGroundSnapping   = false;
			ShowGroundNormal     = false;
			ShowGroundTangent    = false;
			ShowKinematicTangent = false;

			ProcessorsStack.Clear();
		}

		public void FixedUpdate(KCC kcc)
		{
			KCCData fixedData = kcc.FixedData;

			if (ShowPath == true)
			{
				UnityEngine.Debug.DrawLine(fixedData.BasePosition, fixedData.TargetPosition, Color.red, DisplayTime);
			}

			Log(kcc, true);
		}

		public void RenderUpdate(KCC kcc)
		{
			KCCData fixedData  = kcc.FixedData;
			KCCData renderData = kcc.RenderData;

			if (ShowPath == true)
			{
				UnityEngine.Debug.DrawLine(renderData.BasePosition, renderData.TargetPosition, Color.green, DisplayTime);
			}

			KCCData selectedData = UseFixedData == true ? fixedData : renderData;

			if (ShowGrounding == true)
			{
				if (selectedData.IsGrounded == true && selectedData.WasGrounded == false)
				{
					UnityEngine.Debug.DrawLine(selectedData.TargetPosition, selectedData.TargetPosition + Vector3.up, Color.green, DisplayTime);
				}
				else if (selectedData.IsGrounded == false && selectedData.WasGrounded == true)
				{
					UnityEngine.Debug.DrawLine(selectedData.TargetPosition, selectedData.TargetPosition + Vector3.up, Color.red, DisplayTime);
				}
			}

			if (ShowGroundNormal     == true) { UnityEngine.Debug.DrawLine(selectedData.TargetPosition, selectedData.TargetPosition + selectedData.GroundNormal,     Color.magenta, DisplayTime); }
			if (ShowGroundTangent    == true) { UnityEngine.Debug.DrawLine(selectedData.TargetPosition, selectedData.TargetPosition + selectedData.GroundTangent,    Color.yellow,  DisplayTime); }
			if (ShowKinematicTangent == true) { UnityEngine.Debug.DrawLine(selectedData.TargetPosition, selectedData.TargetPosition + selectedData.KinematicTangent, Color.yellow,  DisplayTime); }

			Log(kcc, false);
		}

		public void Reset()
		{
			ProcessorsStack.Clear();
		}

		// PRIVATE METHODS

		private void Log(KCC kcc, bool isInFixedUpdate)
		{
			if (EnableLogs == false)
				return;

			KCCData data;
			_stringBuilder.Clear();

			if (isInFixedUpdate == true)
			{
				data = kcc.FixedData;
				_stringBuilder.Append($"[F]");
			}
			else
			{
				data = kcc.RenderData;
				_stringBuilder.Append($"[R]");
			}

			{
				_stringBuilder.Append($" | {nameof(data.Frame)               } {data.Frame.ToString()                   }");
				_stringBuilder.Append($" | {nameof(data.Tick)                } {data.Tick.ToString()                    }");
				_stringBuilder.Append($" | {nameof(data.Alpha)               } {data.Alpha.ToString("F4")               }");
				_stringBuilder.Append($" | {nameof(data.Time)                } {data.Time.ToString("F6")                }");
				_stringBuilder.Append($" | {nameof(data.DeltaTime)           } {data.DeltaTime.ToString("F6")           }");

				_stringBuilder.Append($" | {nameof(kcc.PhysicsContacts)      } {kcc.PhysicsContacts.ToString()          }");

				_stringBuilder.Append($" | {nameof(data.BasePosition)        } {data.BasePosition.ToString("F4")        }");
				_stringBuilder.Append($" | {nameof(data.DesiredPosition)     } {data.DesiredPosition.ToString("F4")     }");
				_stringBuilder.Append($" | {nameof(data.TargetPosition)      } {data.TargetPosition.ToString("F4")      }");
				_stringBuilder.Append($" | {nameof(data.LookPitch)           } {data.LookPitch.ToString("0.00°")        }");
				_stringBuilder.Append($" | {nameof(data.LookYaw)             } {data.LookYaw.ToString("0.00°")          }");

				_stringBuilder.Append($" | {nameof(data.InputDirection)      } {data.InputDirection.ToString("F4")      }");
				_stringBuilder.Append($" | {nameof(data.ExternalVelocity)    } {data.ExternalVelocity.ToString("F4")    }");
				_stringBuilder.Append($" | {nameof(data.ExternalAcceleration)} {data.ExternalAcceleration.ToString("F4")}");
				_stringBuilder.Append($" | {nameof(data.ExternalImpulse)     } {data.ExternalImpulse.ToString("F4")     }");
				_stringBuilder.Append($" | {nameof(data.ExternalForce)       } {data.ExternalForce.ToString("F4")       }");

				_stringBuilder.Append($" | {nameof(data.DynamicVelocity)     } {data.DynamicVelocity.ToString("F4")     }");
				_stringBuilder.Append($" | {nameof(data.KinematicSpeed)      } {data.KinematicSpeed.ToString("F4")      }");
				_stringBuilder.Append($" | {nameof(data.KinematicTangent)    } {data.KinematicTangent.ToString("F4")    }");
				_stringBuilder.Append($" | {nameof(data.KinematicDirection)  } {data.KinematicDirection.ToString("F4")  }");
				_stringBuilder.Append($" | {nameof(data.KinematicVelocity)   } {data.KinematicVelocity.ToString("F4")   }");

				_stringBuilder.Append($" | {nameof(data.IsGrounded)          } {(data.IsGrounded          ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.WasGrounded)         } {(data.WasGrounded         ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.IsOnEdge)            } {(data.IsOnEdge            ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.IsSteppingUp)        } {(data.IsSteppingUp        ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.WasSteppingUp)       } {(data.WasSteppingUp       ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.IsSnappingToGround)  } {(data.IsSnappingToGround  ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.WasSnappingToGround) } {(data.WasSnappingToGround ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.HasJumped)           } {(data.HasJumped           ? "1" : "0")  }");
				_stringBuilder.Append($" | {nameof(data.HasTeleported)       } {(data.HasTeleported       ? "1" : "0")  }");

				_stringBuilder.Append($" | {nameof(data.GroundNormal)        } {data.GroundNormal.ToString("F4")        }");
				_stringBuilder.Append($" | {nameof(data.GroundTangent)       } {data.GroundTangent.ToString("F4")       }");
				_stringBuilder.Append($" | {nameof(data.GroundPosition)      } {data.GroundPosition.ToString("F4")      }");
				_stringBuilder.Append($" | {nameof(data.GroundDistance)      } {data.GroundDistance.ToString("F4")      }");
				_stringBuilder.Append($" | {nameof(data.GroundAngle)         } {data.GroundAngle.ToString("0.00°")      }");

				_stringBuilder.Append($" | {nameof(data.RealSpeed)           } {data.RealSpeed.ToString("F4")           }");
				_stringBuilder.Append($" | {nameof(data.RealVelocity)        } {data.RealVelocity.ToString("F4")        }");
			}

			if (isInFixedUpdate == true)
			{
				UnityEngine.Debug.LogWarning(_stringBuilder.ToString());
			}
			else
			{
				_stringBuilder.Append($" | {nameof(kcc.PredictionError)      } {kcc.PredictionError.ToString("F4")      }");

				UnityEngine.Debug.Log(_stringBuilder.ToString());
			}
		}
	}
}
