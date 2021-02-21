using System.Collections.Generic;
using Harmony;
using AdminRestrictions.Utility;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace AdminRestrictions.HarmonyPatches
{
    /// <summary>
    /// Overrides the return value in ConsoleSystem.Arg::HasPermission() for ServerAdmin = true commands that are not ran through RCon
    ///	</summary>
    ///	<remarks>
    ///	internal bool HasPermission()
    ///	{
    ///		if (this.cmd == null)
    ///		{
    ///			return false;
    ///		}
    ///		if (this.Option.IsUnrestricted)
    ///		{
    ///			return true;
    ///		}
    ///		if (!this.IsClientside)
    ///		{
    ///			if (this.cmd.ServerAdmin)
    ///			{
    ///				if (this.IsRcon)
    ///				{
    ///					return true;
    ///				}
    ///				if (this.IsAdmin)
    ///				{
    ///				    ### BEFORE ###
    ///					return true;
    ///					### END BEFORE ###
    ///					
    ///					### AFTER ###
    ///					return AdminRestrictions.Instance.CommandHasPermission(this);
    ///					### END AFTER ###
    ///				}
    ///			}
    ///			return this.cmd.ServerUser && this.Connection != null;
    ///		}
    ///		if (this.cmd.ClientAdmin)
    ///		{
    ///			return ConsoleSystem.ClientCanRunAdminCommands != null && ConsoleSystem.ClientCanRunAdminCommands();
    ///		}
    ///		if (this.Option.IsFromServer && !this.cmd.AllowRunFromServer)
    ///		{
    ///			Debug.Log("Server tried to run command \"" + this.FullString + "\", but we blocked it.");
    ///			return false;
    ///		}
    ///		return this.cmd.Client;
    ///	}
    ///	</remarks>
    [HarmonyPatch(typeof(ConsoleSystem.Arg), "HasPermission")]
    public static class ConsoleSystem_Arg_HasPermission_Patch
    {
        static readonly CodeInstruction[] _sequenceToFind = new CodeInstruction[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, typeof(ConsoleSystem.Arg).GetProperty(nameof(ConsoleSystem.Arg.IsAdmin), BindingFlags.Instance | BindingFlags.Public).GetGetMethod()),
            new CodeInstruction(OpCodes.Brfalse),
            new CodeInstruction(OpCodes.Ldc_I4_1)
        };

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> originalCodeInstructions)
        {
            var injectionIndex = originalCodeInstructions.FindCodeInstructionSequenceIndex(_sequenceToFind);
            if (injectionIndex < 0)
            {
                Debug.LogError("Failed to find HasPermission patch IL sequence");
                var sequenceString = string.Join("\n", originalCodeInstructions.Select((x, idx) => $"{idx}) {x.opcode.Name}: ({x.operand?.GetType().Name ?? "NULL"}){x.operand}"));
                Debug.LogError(sequenceString);
                var sequenceToFindString = string.Join("\n", _sequenceToFind.Select((x, idx) => $"{idx}) {x.opcode.Name}: ({x.operand?.GetType().Name ?? "NULL"}){x.operand}"));
                Debug.LogError(sequenceToFindString);
                return originalCodeInstructions;
            }

            var fieldInfo = typeof(SingletonComponent<CommandRestrictor>)
                .GetField(nameof(SingletonComponent<CommandRestrictor>.Instance), BindingFlags.Static | BindingFlags.Public);

            var methodInfo = typeof(CommandRestrictor)
                .GetMethod(nameof(CommandRestrictor.CommandHasPermission), BindingFlags.Instance | BindingFlags.NonPublic);

            var retInstructions = new List<CodeInstruction>(originalCodeInstructions);
            retInstructions.RemoveAt(injectionIndex + 3);
            retInstructions.InsertRange(injectionIndex + 3, new CodeInstruction[] {
                new CodeInstruction(OpCodes.Ldsfld, fieldInfo),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, methodInfo)
            });

            return retInstructions;
        }
    }
}
