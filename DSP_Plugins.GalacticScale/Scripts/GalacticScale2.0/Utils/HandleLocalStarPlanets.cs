﻿using System.Collections.Generic;

namespace GalacticScale
{
    public static class HandleLocalStarPlanets
    {
        private static string status = "Start";
        private static string lastStatus = "";
        private static StarData closestStar;
        private static PlanetData closestPlanet;
        private static GSPlanet gsPlanet;
        private static readonly Dictionary<PlanetData, double> transitionRadii = new Dictionary<PlanetData, double>();
        private static void LogStatus(string incoming = "")
        {
            if (incoming != "")
            {
                status = incoming;
            }

            if (status == lastStatus)
            {
                return;
            }

            lastStatus = status;
            GS2.Log($"Current Status:{status}");
        }
        private static int GetLoadedPlanetCount(StarData star)
        {
            int planetsLoaded = 0;
            for (var i = 0; i < star.planetCount; i++)
            {
                if (star.planets[i].loaded)
                {
                    planetsLoaded++;
                }
            }
            return planetsLoaded;
        }
        public static string GetStarLoadingStatus(StarData star)
        {
            int planetsLoaded = GetLoadedPlanetCount(star);

            return $"{planetsLoaded}/{star.planetCount}";
        }
        public static bool Update()
        {
            StarData localStar = GameMain.localStar;
            PlanetData localPlanet = GameMain.localPlanet;
            closestStar = localStar;
            closestPlanet = localPlanet;
            bool Warping = GameMain.mainPlayer.warping;

            if (localStar != null && !localStar.loaded)
            {
                //We assume the star is still loading, so wait.
                LogStatus($"Star {localStar.name} loading {GetStarLoadingStatus(localStar)} localPlanet:{localPlanet?.name}");
                if (localPlanet != null) EnsurePlanetStillLocal();
                if (localPlanet != null && closestPlanet == null)
                {
                    GS2.Log($"Leaving Planet {localPlanet.name} as it is not the closest planet");
                    GameMain.data.LeavePlanet();
                }
                if (localPlanet == null && closestPlanet == null) // Try and speed up planet acquisition :)~
                {
                    SearchPlanet();
                    if (closestPlanet != null && closestPlanet.loaded)
                    {
                        GS2.Log($"Arriving at Planet {closestPlanet.name}");
                        GameMain.data.ArrivePlanet(closestPlanet);
                        return true;
                    }
                }
                //GS2.Warn($"localPlanet:{localPlanet?.name} closestPlanet:{closestPlanet?.name}");
                return false;
            }
            if (localStar != null && localPlanet != null && (!localPlanet.loaded || !localPlanet.factoryLoaded || localPlanet.loading))
            {
                //We assume the planet is still loading, so wait.
                LogStatus($"Planet  {localPlanet.name} Loading");
                return false;
            }
            //GS2.Warn($"localstar:{localStar?.loaded} localplanet:{localPlanet?.loaded}");
            ////Getlocalstarplanets
            //if (closestStar != null && GS2.GetGSStar(closestStar).Decorative)
            //{
            //    GS2.Log($"Ignoring Decorative Star {closestStar.name}");
            //    closestStar = null;
            //}
            if (closestStar != null)
            {
                EnsureStarStillLocal();
            }

            if (closestStar != null && closestPlanet != null)
            {
                EnsurePlanetStillLocal();
            }

            if (closestStar == null)
            {
                SearchStar();
            }

            if (!Warping)
            {
                if (closestStar != null && closestPlanet == null)
                {
                    SearchPlanet();
                }
            }
            else
            {
                closestPlanet = null;
            }

            if (closestStar != null && GameMain.data.guideRunning && GameMain.data.guideMission.forceLocalPlanet)
            {
                //Force closestPlanet for prologue use only
                closestPlanet = GameMain.data.guideMission.localPlanet;
            }
            bool ResetCamera = false;
            if (localStar != null)
            {
                if (localPlanet != null)
                {
                    if (localPlanet != closestPlanet)
                    {
                        ResetCamera = true;
                        GS2.Log($"Leaving Planet {localPlanet.name} as it is not closest");
                        GameMain.data.LeavePlanet();
                    }
                }
                else if (closestPlanet != null)
                {
                    ResetCamera = true;
                    GS2.Log($"Arriving at Planet {closestPlanet.name}");
                    GameMain.data.ArrivePlanet(closestPlanet);
                }

                if (localStar != closestStar)
                {
                    ResetCamera = true;
                    GS2.Log($"Leaving Star {localStar.name} as it is not closest");
                    GameMain.data.LeaveStar();
                }
            }
            else if (closestStar != null)
            {
                ResetCamera = true;
                GS2.Log($"Arriving at Star {closestStar.name}");
                GameMain.data.ArriveStar(closestStar);
            }
            return ResetCamera;
        }
        private static void EnsureStarStillLocal()
        {
            LogStatus($"Ensure {closestStar.name} still local...");
            if (DistanceTo(closestStar) > TransisionDistance(closestStar))
            {
                GS2.Log($"Leaving star {closestStar.name} as its too far away {DistanceTo(closestStar)/40000}AU < {TransisionDistance(closestStar)/40000}AU");
                GameMain.data.LeaveStar();
                closestStar = null;
            }
        }
        private static void EnsurePlanetStillLocal()
        {
            if (DistanceTo(closestPlanet) > TransisionDistance(closestPlanet))
            {
                GS2.Log($"Leaving planet {closestPlanet.name} as its too far away");

                closestPlanet = null;
                //GameMain.data.LeavePlanet();
            }
        }
        private static void SearchPlanet()
        {
            for (var i = 0; closestStar != null && closestPlanet == null && i < closestStar.planetCount; i++)
            {
                PlanetData planet = closestStar.planets[i];
                if (DistanceTo(planet) < TransisionDistance(planet)) { closestPlanet = planet; }
            }
        }
        private static void SearchStar()
        {
            for (int i = 0; closestStar == null && i < GameMain.galaxy.starCount; i++)
            {
                StarData star = GameMain.galaxy.stars[i];
                if (star.planetCount == 0)
                {
                    continue;
                }

                if (GS2.GetGSStar(star).Decorative)
                {
                    continue;
                }

                if (DistanceTo(star) < TransisionDistance(star))
                {
                    GS2.Log($"Found Star {star.name}");
                    closestStar = star;
                }

                if (GameMain.isRunning && closestStar != null && !closestStar.loaded)
                {
                    closestStar.Load();
                    return;
                }
            }
        }
        private static double DistanceTo(PlanetData planet) => (GameMain.mainPlayer.uPosition - planet.uPosition).magnitude - planet.realRadius;
        private static double DistanceTo(StarData star) => (GameMain.mainPlayer.uPosition - star.uPosition).magnitude;
        private static double TransisionDistance(StarData star)
        {
            //GS2.Log($"SystemRadius:{star.systemRadius}");
            //for (var i = 0; i < star.planetCount; i++)
            //{
            //    GS2.Log($"Planet:{star.planets[i].name} Orbit:{star.planets[i].orbitRadius}");
            //}
            return (star.systemRadius + 2) * 40000;
        }
        /// <summary>
        /// Calculates a transition distance for a planet, ensuring it is less than the orbit of the first moon, in order to allow the player to land on said moon.
        /// </summary>
        /// <param name="planet">The planet to calculate a transition distance for</param>
        /// <returns></returns>
        private static double TransisionDistance(PlanetData planet)
        {
            if (transitionRadii.ContainsKey(planet))
            {
                return transitionRadii[planet];
            }

            double transitionDistance = planet.realRadius * 2;
            if (gsPlanet == null || gsPlanet.planetData != planet)
            {
                gsPlanet = GS2.GetGSPlanet(planet);
            }
            if (gsPlanet.MoonsCount > 0)
            {
                PlanetData moon = gsPlanet.Moons[0].planetData;
                double distance = (moon.uPosition - planet.uPosition).magnitude - planet.realRadius - TransisionDistance(moon) - 100;
                GS2.Log($"Distance of {planet.name} is {distance}, transitionDistance is {transitionDistance}");
                if (distance < transitionDistance)
                {
                    LogStatus($"Transition Distance of {planet.name} reduced to {distance}");
                    transitionRadii.Add(planet, distance);
                    return distance;
                }
                else
                {
                    GS2.Log($"Not setting anything, as td:{transitionDistance} < d:{distance}");
                }
            }
            transitionRadii.Add(planet, transitionDistance);
            return transitionDistance;
        }
    }
}