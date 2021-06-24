﻿using System;
using System.Collections.Generic;
using System.Threading;
using static GalacticScale.GS2;
using static PlanetModelingManager;

namespace GalacticScale
{
    public static class Modeler
    {
        public static List<PlanetData> planetModQueue = new List<PlanetData>();
        public static bool planetModQueueSorted = false;
        public static List<PlanetData> planetQueue = new List<PlanetData>();
        public static bool planetQueueSorted = false;

        public static int DistanceComparison(PlanetData p1, PlanetData p2)
        {
            double d1 = distanceTo(p1);
            double d2 = distanceTo(p2);
            if (d1 > d2) return 1;
            return -1;
        }
        static double distanceTo(PlanetData planet)
        {
            return (GameMain.mainPlayer.uPosition - planet.uPosition).magnitude;
        }
        public static bool Compute(ref ThreadFlag ___planetComputeThreadFlag, ref ThreadFlagLock ___planetComputeThreadFlagLock, ref Thread ___planetComputeThread)
        {
            object obj = null;
            lock (planetComputeThreadFlagLock)
            {
                obj = planetComputeThread;
            }
            int cycles = 0;
            while (true)
            {
                cycles++;
                HighStopwatch pqsw = new HighStopwatch();
                pqsw.Begin();
                int num = 0;
                lock (planetComputeThreadFlagLock)
                {
                    if (planetComputeThreadFlag != ThreadFlag.Running)
                    {
                        planetComputeThreadFlag = ThreadFlag.Ended;
                        return false;
                    }
                    if (obj != planetComputeThread)
                    {
                        return false;
                    }
                }
                PlanetData planetData = null; 
                lock (genPlanetReqList)
                {
                    if (genPlanetReqList.Count > 0)
                    {
                        Log("Processing List");
                        planetQueueSorted = false;
                        while (genPlanetReqList.Count > 0) planetQueue.Add(genPlanetReqList.Dequeue());
                    }
                }
                if (!planetQueueSorted && planetQueue.Count > 1)
                    lock (planetQueue)
                    {
                        Log($"Sorting Queue with {planetQueue.Count} entries");
                        planetQueue.Sort(DistanceComparison);
                        planetQueueSorted = true;
                        Log("Sorted");
                    }
                if (planetQueue.Count > 0)
                {

                    planetData = planetQueue[0];
                    planetQueue.RemoveAt(0);
                    Log($"Retrieved sorted planet from list: {planetData.name}");
                }
                if (planetData != null)
                {
                    Log($"Preamble time taken:{pqsw.duration:F5}");
                    try
                    {
                        PlanetAlgorithm planetAlgorithm = Algorithm(planetData);
                        if (planetAlgorithm != null)
                        {
                            HighStopwatch highStopwatch = new HighStopwatch();
                            double num2 = 0.0;
                            double num3 = 0.0;
                            double num4 = 0.0;
                            if (planetData.data == null)
                            {
                                highStopwatch.Begin();
                                planetData.data = new PlanetRawData(planetData.precision);
                                planetData.modData = planetData.data.InitModData(planetData.modData);
                                planetData.data.CalcVerts();
                                planetData.aux = new PlanetAuxData(planetData);
                                planetAlgorithm.GenerateTerrain(planetData.mod_x, planetData.mod_y);
                                planetAlgorithm.CalcWaterPercent();
                                num2 = highStopwatch.duration;
                            }
                            if (planetData.factory == null)
                            {
                                highStopwatch.Begin();
                                if (planetData.type != EPlanetType.Gas)
                                {
                                    planetAlgorithm.GenerateVegetables();
                                }
                                num3 = highStopwatch.duration;
                                highStopwatch.Begin();
                                if (planetData.type != EPlanetType.Gas)
                                {
                                    planetAlgorithm.GenerateVeins(sketchOnly: false);
                                }
                                num4 = highStopwatch.duration;
                            }
                            if (planetComputeThreadLogs != null)
                            {
                                lock (planetComputeThreadLogs)
                                {
                                    planetComputeThreadLogs.Add($"{planetData.displayName}\r\nGenerate Terrain {num2:F5} s\r\nGenerate Vegetables {num3:F5} s\r\nGenerate Veins {num4:F5} s\r\n");
                                    Log($"{planetData.displayName}\r\nGenerate Terrain {num2:F5} s\r\nGenerate Vegetables {num3:F5} s\r\nGenerate Veins {num4:F5} s\r\n");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (planetComputeThreadError)
                        {
                            if (string.IsNullOrEmpty(planetComputeThreadError))
                            {
                                planetComputeThreadError = ex.ToString();
                            }
                        }
                    }
                    lock (modPlanetReqList)
                    {
                        Log($"Queuing {planetData.name} in modPlanetReqList after {pqsw.duration:F5}");
                        modPlanetReqList.Enqueue(planetData);
                    }
                }
                if (cycles > 600)
                {
                    cycles = 0;   
                    Log("Modeler 10sec Tick");
                }
                    if (planetData == null)
                {
                    Thread.Sleep(50);
                }
                else if (num % 20 == 0)
                {
                    Thread.Sleep(2);
                }
            }
        }
        public static void ModelingCoroutine()
        {
            if (currentModelingPlanet == null)
            {
                PlanetData planetData = null;
                lock (modPlanetReqList)
                {
                    if (modPlanetReqList.Count > 0)
                    {
                        Log("Processing List");
                        planetModQueueSorted = false;
                        while (modPlanetReqList.Count > 0) planetModQueue.Add(modPlanetReqList.Dequeue());
                    }
                }
                if (!planetModQueueSorted && planetModQueue.Count > 1)
                    lock (planetModQueue)
                    {
                        HighStopwatch hsw = new HighStopwatch();
                        hsw.Begin();
                        Log($"Sorting ModQueue with {planetModQueue.Count} entries");
                        planetModQueue.Sort(DistanceComparison);
                        planetModQueueSorted = true;
                        Log($"Sorted ModQueue in {hsw.duration:F5}");
                    }
                if (planetModQueue.Count > 0)
                {

                    planetData = planetModQueue[0];
                    planetModQueue.RemoveAt(0);
                    Log($"Retrieved sorted planet from mod list: {planetData.name}");
                }

                if (planetData != null)
                {
                    currentModelingPlanet = planetData;
                    currentModelingStage = 0;
                    currentModelingSeamNormal = 0;
                }
            }
            if (currentModelingPlanet != null)
            {
                try
                {
                    ModelingPlanetMain(currentModelingPlanet);
                }
                catch (Exception message)
                {
                    Error(message.ToString());
                    currentModelingPlanet.Unload();
                    currentModelingPlanet.factoryLoaded = false;
                    currentModelingPlanet = null;
                    currentModelingStage = 0;
                    currentModelingSeamNormal = 0;
                }
            }
            return;
        }
    }
}