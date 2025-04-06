using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;
using Verse.AI;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HotBunkMod
{
    [StaticConstructorOnStartup]
    public static class HotBunkMod
    {
        static HotBunkMod()
        {
            new Harmony("HotBunkMod").PatchAll();
        }
    }

    public class CompProperties_HotBunk : CompProperties
    {
        public CompProperties_HotBunk()
        {
            this.compClass = typeof(CompHotBunk);
        }
    }

    public class CompHotBunk : ThingComp
    {
        public bool hotBunkEnabled = false;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref hotBunkEnabled, "hotBunkEnabled");
        }
    }

    [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetGizmos))]
    public static class Patch_BuildingBed_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
        {
            foreach (var gizmo in __result)
                yield return gizmo;

            var comp = __instance.GetComp<CompHotBunk>();
            if (comp == null)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = "HotBunk",
                defaultDesc = "Toggle whether this bed should be a Hot Bunk.",
                icon = ContentFinder<Texture2D>.Get(
                    comp.hotBunkEnabled ? "UI/Commands/HotBunk" : "UI/Commands/ColdBunk", true),
                action = () =>
                {
                    comp.hotBunkEnabled = !comp.hotBunkEnabled;
                    if (comp.hotBunkEnabled) {
                        SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOn);
                    }
                    else if (!comp.hotBunkEnabled) {
                        SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOff);
                    }
                }
            };
        }
    }

    [HarmonyPatch(typeof(Toils_LayDown))]
    [HarmonyPatch(nameof(Toils_LayDown.LayDown))]
    [HarmonyPatch(new Type[]
    {
        typeof(TargetIndex),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(PawnPosture),
        typeof(bool)
    })]
    public static class Patch_Toils_LayDown_LayDown
    {
        // Postfix captures the newly created Toil, so we can inject an AddFinishAction
        public static void Postfix(
            Toil __result,
            TargetIndex bedOrRestSpotIndex,
            bool hasBed,
            bool lookForOtherJobs,
            bool canSleep,
            bool gainRestAndHealth,
            PawnPosture noBedLayingPosture,
            bool deathrest)
        {
            // If the method returned null for some reason, don't do anything
            if (__result == null) return;

            __result.AddFinishAction(delegate
            {
                Pawn actor = __result.actor;
                if (actor?.CurJob == null) return;

                // The bed (or sleeping spot) used by the job
                var bed = actor.CurJob.GetTarget(bedOrRestSpotIndex).Thing as Building_Bed;
                if (bed == null) return;

                // Check the hot-bunk comp
                var comp = bed.GetComp<CompHotBunk>();
                if (comp == null) return;

                // If flagged as a hot bunk, clear assignment
                if (comp.hotBunkEnabled)
                {
                    // For multi-pawn beds:
                    bed.CompAssignableToPawn?.TryUnassignPawn(actor);

                    // Or for typical single assignment:
                    // actor.ownership.UnclaimBed();
                }
            });
        }
    }

}
