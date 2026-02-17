using HarmonyLib;
using RustServerMetrics.HarmonyPatches.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace RustServerMetrics.HarmonyPatches.Delayed
{
	[DelayedHarmonyPatch]
	[HarmonyPatch]
	internal static class InvokeHandlerBase_DoTick_Patch
	{
		static System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
		static bool _failedExecution = false;

		readonly static CodeInstruction[] _replacementSequenceToFind = new CodeInstruction[]
		{
			new CodeInstruction(OpCodes.Ldloc_S),
			new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(InvokeAction), nameof(InvokeAction.action))),
			new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Action), nameof(Action.Invoke)))
		};

		[HarmonyPrepare]
		public static bool Prepare()
		{
			if (!RustServerMetricsLoader.__serverStarted)
			{
				Debug.Log("Note: Cannot patch InvokeHandlerBase_DoTick_Patch yet. We will patch it upon server start.");
				return false;
			}

			return true;
		}

		[HarmonyTargetMethods]
		public static IEnumerable<MethodBase> TargetMethods(Harmony harmonyInstance)
		{
			yield return AccessTools.DeclaredMethod(typeof(InvokeHandlerBase<InvokeHandler>), "DoTick");
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> originalInstructions, MethodBase methodBase)
		{
			try
			{
				LocalVariableInfo variableInfo = methodBase.GetMethodBody().LocalVariables.FirstOrDefault(x => x.LocalType == typeof(InvokeAction));
				if (variableInfo == null)
				{
					Debug.LogError($"[ServerMetrics]: Failed to find InvokeAction local variable in {nameof(InvokeHandlerBase_DoTick_Patch)}");
					return originalInstructions;
				}

				_replacementSequenceToFind[0].operand = variableInfo;

				var instructionsList = new List<CodeInstruction>(originalInstructions);
				var methodToCallInfo = typeof(InvokeHandlerBase_DoTick_Patch)
					.GetMethod(nameof(InvokeWrapper), BindingFlags.Static | BindingFlags.NonPublic);

				int replacementsCount = 0;
				while (true)
				{
					var replacementIdx = GetSequenceStartIndex(instructionsList, _replacementSequenceToFind, false);
					if (replacementIdx < 0)
						break;

					// Replace sequence:
					// [idx + 0] ldloc.s invokeAction
					// [idx + 1] ldfld action  
					// [idx + 2] callvirt Invoke
					// With:
					// [idx + 0] ldloc.s invokeAction
					// [idx + 1] call InvokeWrapper

					var newInstruction = new CodeInstruction(OpCodes.Call, methodToCallInfo);
					// Copy labels from the instruction we're replacing
					newInstruction.labels.AddRange(instructionsList[replacementIdx + 1].labels);

					instructionsList[replacementIdx + 1] = newInstruction;
					instructionsList.RemoveAt(replacementIdx + 2); // Remove callvirt

					replacementsCount++;
				}

				if (replacementsCount == 0)
				{
					Debug.LogWarning($"[ServerMetrics]: Failed to find any replacement sequences for {nameof(InvokeHandlerBase_DoTick_Patch)} - skipping patch (game code may have changed)");
					Debug.LogWarning($"[ServerMetrics]: Total instructions in method: {instructionsList.Count}");
				}
				else
				{
					Debug.Log($"[ServerMetrics]: Successfully patched {replacementsCount} invokeAction.action() call(s) in {nameof(InvokeHandlerBase_DoTick_Patch)}");
					Debug.Log($"[ServerMetrics]: Transpiled DoTick method now has {instructionsList.Count} instructions");
				}

				return instructionsList;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ServerMetrics]: Exception in {nameof(InvokeHandlerBase_DoTick_Patch)}: {ex.Message}");
				Debug.LogError($"Stack trace: {ex.StackTrace}");
				return originalInstructions;
			}
		}

		static void InvokeWrapper(InvokeAction invokeAction)
		{
			_stopwatch.Restart();
			_failedExecution = false;
			try
			{
				invokeAction.action.Invoke();
			}
			catch (Exception)
			{
				_failedExecution = true;
				throw;
			}
			finally
			{
				_stopwatch.Stop();
				MetricsLogger.Instance?.ServerInvokes.LogTime(invokeAction.action.Method, _stopwatch.Elapsed.TotalMilliseconds);
			}
		}

		static int GetSequenceStartIndex(List<CodeInstruction> originalList, CodeInstruction[] sequenceToFind, bool debug = false)
		{
			CodeInstruction firstSequence = sequenceToFind[0];
			for (int i = 0; i < originalList.Count; i++)
			{
				if (originalList.Count - i < sequenceToFind.Length)
					break;

				var instruction = originalList[i];

				if (debug && instruction.opcode == firstSequence.opcode)
				{
					UnityEngine.Debug.Log($"Trying to match starting sequence {i}, {instruction.opcode} <-> {firstSequence.opcode}, ({instruction.operand?.GetType().FullName ?? "null"}){instruction.operand} <-> ({firstSequence.operand?.GetType().FullName ?? "null"}){firstSequence.operand}");
				}
				if (instruction.opcode == firstSequence.opcode)
				{
					switch (instruction.operand)
					{
						case LocalBuilder:
						case LocalVariableInfo:
							var instructionAsLocalVarInfo = instruction.operand as LocalVariableInfo;
							var firstSequenceAsLocalVarInfo = firstSequence.operand as LocalVariableInfo;
							if (instructionAsLocalVarInfo.LocalType != firstSequenceAsLocalVarInfo.LocalType || instructionAsLocalVarInfo.LocalIndex != firstSequenceAsLocalVarInfo.LocalIndex) continue;
							break;

						default:
							if (instruction.operand != firstSequence.operand) continue;
							break;
					}

					bool found = true;
					int z;
					for (z = 1; z < sequenceToFind.Length; z++)
					{
						var currentInstruction = originalList[i + z];
						var sequenceInstruction = sequenceToFind[z];
						if (currentInstruction.opcode != sequenceInstruction.opcode)
						{
							if (sequenceInstruction.operand != null && currentInstruction.operand != sequenceInstruction.operand)
							{
								if (debug)
								{
									UnityEngine.Debug.Log($"Failed match {z}, {currentInstruction.opcode} <-> {sequenceInstruction.opcode}, ({currentInstruction.operand?.GetType().FullName ?? "null"}){currentInstruction.operand} <-> ({sequenceInstruction.operand?.GetType().FullName ?? "null"}){sequenceInstruction.operand}");
								}
								found = false;
								break;
							}
						}
					}

					if (found) return i;
				}
			}

			return -1;
		}
	}
}
