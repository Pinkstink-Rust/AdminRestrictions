using Harmony;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace AdminRestrictions.HarmonyPatches
{
    [HarmonyPatch(typeof(Bootstrap), nameof(Bootstrap.StartServer))]
    public class Bootstrap_StartServer_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> originalInstructions)
        {
            List<CodeInstruction> retList = new List<CodeInstruction>(originalInstructions);

            var methodInfo = typeof(CommandRestrictor)
                .GetMethod(nameof(CommandRestrictor.Initialize), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            retList.InsertRange(0, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Call, methodInfo)
            });

            return retList;
        }
    }
}
