using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSP.Localization;
using UnityEngine;

namespace RendererBounds
{
    /// <summary>
    /// Replaces the <see cref="Renderer.bounds"/> reads in the methods that collect a
    /// part's renderer bounds with <see cref="TightBounds.Of"/>.
    /// </summary>
    /// <remarks>
    /// Stock <c>PartGeometryUtil.GetPartRendererBounds</c> feeds craft size, the
    /// launch size checks, cargo bay and inventory fit, and thumbnails.
    /// KSPCommunityFixes replaces it with a prefix that picks renderers by its own
    /// rules, and RP-1 currently carries a copy of its own; both are patched the same
    /// way when present, so their rules for which renderers count are kept.
    /// </remarks>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class Loader : MonoBehaviour
    {
        private const string Tag = "[RendererBounds]";

        private static readonly MethodInfo GetBounds = AccessTools.PropertyGetter(typeof(Renderer), nameof(Renderer.bounds));
        private static readonly MethodInfo TightOf = AccessTools.Method(typeof(TightBounds), nameof(TightBounds.Of));

        /// <summary>Methods patched and the number of reads replaced in each, for diagnostics.</summary>
        public static readonly Dictionary<string, int> Patched = new Dictionary<string, int>();

        private static int replaced;

        private void Awake()
        {
            var harmony = new Harmony("RendererBounds");
            var transpiler = new HarmonyMethod(AccessTools.Method(typeof(Loader), nameof(Transpiler)));
            var targets = new List<MethodBase>
            {
                AccessTools.Method(typeof(PartGeometryUtil), nameof(PartGeometryUtil.GetPartRendererBounds)),
                AccessTools.Method("KSPCommunityFixes.PartBoundsIgnoreDisabledTransforms:PartGeometryUtil_GetPartRendererBounds_Prefix"),
                AccessTools.Method("RP0.KCTUtilities:GetPartRendererBounds"),
            };
            foreach (MethodBase target in targets)
            {
                if (target == null)
                    continue;
                string name = target.DeclaringType.FullName + "." + target.Name;
                replaced = 0;
                try
                {
                    harmony.Patch(target, transpiler: transpiler);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{Tag} patching {name} failed: {e}");
                    continue;
                }
                Patched[name] = replaced;
                if (replaced == 0)
                    Debug.LogWarning($"{Tag} {name} has no Renderer.bounds read to replace; its bounds stay inflated and axis aligned");
                else
                    Debug.Log($"{Tag} {name}: replaced {replaced} Renderer.bounds read(s)");
            }

            GameEvents.onGameSceneLoadRequested.Add(_ => TightBounds.Clear());

            string version = typeof(Loader).Assembly.GetName().Version.ToString(3);
            Debug.Log($"{Tag} {Localizer.Format("#LOC_RendererBounds_Loaded", version)}");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                // The getter is declared on Renderer, so reads through a MeshRenderer or
                // SkinnedMeshRenderer reference compile to the same callvirt.
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call)
                    && instruction.operand is MethodInfo method && method == GetBounds)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = TightOf;
                    replaced++;
                }
                yield return instruction;
            }
        }
    }
}
