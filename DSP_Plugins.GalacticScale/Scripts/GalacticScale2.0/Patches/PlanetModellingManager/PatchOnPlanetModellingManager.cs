﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Logging;
using UnityEngine;
using GalacticScale.Scripts;

namespace GalacticScale
{
	
	public class PatchOnPlanetModellingManager : MonoBehaviour
	{
		[HarmonyPrefix, HarmonyPatch(typeof(PlanetModelingManager), "Algorithm")]
		public static bool Algorithm(PlanetData planet, ref PlanetAlgorithm __result)
		{
			if (DSPGame.IsMenuDemo) return true;
			if (GS2.Vanilla) return true;
			//GS2.Log("CHOOSING ALGORITHM FOR " + planet.displayName + " rawdata?"+(planet.data != null));
            GSPlanet gsPlanet = GS2.GetGSPlanet(planet);
			GSTheme gsTheme = GS2.ThemeLibrary[gsPlanet.Theme];
			//GS2.Log("Use Custom Generation? " + gsTheme.CustomGeneration);
			if (!GS2.ThemeLibrary[gsPlanet.Theme].CustomGeneration)
			{
				//GS2.Log("CHOSE COMPLETELY VANILLA");
				return true;
			}
			//GS2.Log("USING CUSTOM GENERATION FOR PLANET " + planet.displayName);
            __result = new GS2PlanetAlgorithm(gsPlanet);//new GS2PlanetAlgorithm(gsPlanet);
			__result.Reset(5, planet);
			//GS2.Log("PatchOnPlanetModellingManager|Algorithm|"+__result.planet.name+"|End|"+__result.seed);
			return false;
		}
	
	    [HarmonyPrefix]
	    [HarmonyPatch(typeof(PlanetModelingManager), "ModelingPlanetMain")]
	    public static bool ModelingPlanetMain(PlanetData planet)
	    {
		    //GS2.Log("PatchPlanetSize|PatchOnPlanetModellingManager|ModelingPlanetMain|"+planet.name);
		    planet.data.AddFactoredRadius(planet);
		    return true;
	    }
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(PlanetModelingManager), "ModelingPlanetMain")]
        public static IEnumerable<CodeInstruction> ModelingPlanetMainTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);

            //Patch.Debug("ModelingPlanetMain Transpiler.", LogLevel.Debug, Patch.DebugPlanetModelingManagerDeep);
            for (int instructionCounter = 0; instructionCounter < instructionList.Count; instructionCounter++)
            {
                if (instructionList[instructionCounter].Calls(typeof(PlanetData).GetProperty("realRadius").GetGetMethod()))
                {
                    //Patch.Debug("Found realRadius Property getter call.", LogLevel.Debug, Patch.DebugPlanetModelingManagerDeep);
                    if (instructionCounter + 4 < instructionList.Count &&
                        instructionList[instructionCounter + 1].opcode == OpCodes.Ldc_R4 && instructionList[instructionCounter + 1].OperandIs(0.2f) &&
                        instructionList[instructionCounter + 2].opcode == OpCodes.Add &&
                        instructionList[instructionCounter + 3].opcode == OpCodes.Ldc_R4 && instructionList[instructionCounter + 3].OperandIs(0.025f))
                    {
                        //Patch.Debug("Found THE CORRECT realRadius Property getter call.", LogLevel.Debug, Patch.DebugPlanetModelingManagerDeep);
                        //+1 = ldc.r4 0.2
                        //+2 = add
                        //+3 = ldc.r4 0.025 <-- replace
                        instructionList.RemoveAt(instructionCounter + 3);
                        List<CodeInstruction> toInsert = new List<CodeInstruction>()
                        {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(instructionList[instructionCounter]),
                            new CodeInstruction(OpCodes.Ldc_R4, 8000f),
                            new CodeInstruction(OpCodes.Div)
                        };
                        instructionList.InsertRange(instructionCounter + 3, toInsert);
                    }
                }
                else if (instructionList[instructionCounter].Calls(typeof(PlanetRawData).GetMethod("GetModPlane")))
                {
                    //GS2.Log("Found GetModPlane callvirt. Replacing with GetModPlaneInt call.");
                    instructionList[instructionCounter] = new CodeInstruction(OpCodes.Call, typeof(PlanetRawDataExtension).GetMethod("GetModPlaneInt"));
                }
            }

            return instructionList.AsEnumerable();
        }
    }
}