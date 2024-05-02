using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace AdminRestrictions.Utility
{
    public static class Helpers
    {
        public static int FindCodeInstructionSequenceIndex(this IEnumerable<CodeInstruction> codeInstructions, IEnumerable<CodeInstruction> sequenceToFind)
        {
            if (codeInstructions == null || sequenceToFind == null || sequenceToFind.Count() < 1 || codeInstructions.Count() < 1) return -1;
            var firstSequenceInstruction = sequenceToFind.ElementAt(0);

            for (int i = 0; i < codeInstructions.Count() - sequenceToFind.Count(); i++)
            {
                var currentCodeInstruction = codeInstructions.ElementAt(i);

                if (OpCodesMatch(currentCodeInstruction.opcode, firstSequenceInstruction.opcode) && OperandsMatch(currentCodeInstruction.operand, firstSequenceInstruction.operand))
                {
                    bool isMatch = true;
                    // Skip First Instruction
                    for (int j = 1; j < sequenceToFind.Count(); j++)
                    {
                        var offsetCodeInstruction = codeInstructions.ElementAt(i + j);
                        var currentSequenceElement = sequenceToFind.ElementAt(j);
                        if (OpCodesMatch(offsetCodeInstruction.opcode, currentSequenceElement.opcode) && OperandsMatch(offsetCodeInstruction.operand, currentSequenceElement.operand))
                            continue;
                        isMatch = false;
                        break;
                    }

                    if (isMatch)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        static bool OpCodesMatch(OpCode a, OpCode b)
        {
            return a.Name == b.Name;
        }

        static bool OperandsMatch(object source, object target)
        {
            // are objects equal?
            if (source == target) return true;
            if (source == null && target == null) return true;
            if (source == null) return false;
            // Hack for labels, cbf adding lookup for these.
            if (source.GetType() == typeof(Label) && target == null) return true;
            if (target == null) return false;
            if (source is MethodBase sourceMethod && target is MethodBase targetMethod)
                return sourceMethod.Equals(targetMethod);

            return false;
        }
    }
}
