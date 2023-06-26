using System;
using System.Collections.Generic;

using Sandbox.ModAPI;
using Sandbox.Game.Entities;

using VRageMath;
using VRage.Game.Entity;

namespace SEBR_NAMESPACE
{
    public static class SEBR_UTILS
    {

        public static bool IsPlayerBot(IMyPlayer player)
        {
            return player.DisplayName == null || player.SteamUserId == null || player.IsBot;
        }

        /// <summary>
        /// Method <c>AreDecoysWithinRadius</c> determines if a decoy is in radius. It is mostly stolen from Jakaria's lightning code.
        /// </summary>
        /// <param name="localSphere"></param>
        /// <param name="hitPosition"></param>
        /// <returns></returns>
        public static bool AreDecoysWithinRadius(ref BoundingSphereD localSphere, ref Vector3D hitPosition)
        {
            List<MyEntity> m_decoyGrids = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref localSphere, m_decoyGrids, MyEntityQueryType.Both);
            foreach (MyEntity decoyGrid in m_decoyGrids)
            {
                MyCubeGrid myCubeGrid = decoyGrid as MyCubeGrid;
                if (myCubeGrid == null || !(decoyGrid is MyCubeGrid) || !myCubeGrid.Decoys.IsValid)
                {
                    continue;
                }

                foreach (IMyDecoy decoy in myCubeGrid.Decoys)
                {
                    if (decoy.IsWorking && Vector3D.Distance(decoy.PositionComp.GetPosition(), hitPosition) < 50)
                    {
                        hitPosition = decoy.PositionComp.GetPosition();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Method <c>CenteredString</c> is for formatting notifications.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        public static string CenteredString(string s, int width)
        {
            if (s.Length >= width)
            {
                return s;
            }

            int leftPadding = (width - s.Length) / 2;
            int rightPadding = width - s.Length - leftPadding;

            return new string(' ', leftPadding) + s + new string(' ', rightPadding);
        }

        /// <summary>
        /// Method <c>IsPositionInCylinder</c> detects if a position is inside a specified cylinder. Courtesy Whiplash141.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="cylinderCenterPosition"></param>
        /// <param name="cylinderAxis"></param>
        /// <param name="cylinderHeight"></param>
        /// <param name="cylinderRadius"></param>
        /// <returns></returns>
        public static bool IsPositionInCylinder(Vector3D position, Vector3D cylinderCenterPosition, Vector3D cylinderAxis, double cylinderHeight, double cylinderRadius)
        {
            if (!Vector3D.IsUnit(ref cylinderAxis))
            {
                cylinderAxis = Vector3D.Normalize(cylinderAxis);
            }
            Vector3D dirn = position - cylinderCenterPosition;
            double height = Vector3D.Dot(dirn, cylinderAxis);
            if (Math.Abs(height) > cylinderHeight * 0.5)
            {
                return false;
            }

            Vector3D perpDirn = dirn - height * cylinderAxis;
            if (perpDirn.LengthSquared() > cylinderRadius * cylinderRadius)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Method <c>GenerateClosestPositionOnCylinder</c> determines the closest point on the wall of the zone cylinder.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="cylinderCenterPosition"></param>
        /// <param name="cylinderAxis"></param>
        /// <param name="cylinderHeight"></param>
        /// <param name="cylinderRadius"></param>
        /// <returns></returns>
        public static Vector3D GenerateClosestPositionOnCylinder(Vector3D position, Vector3D cylinderCenterPosition, Vector3D cylinderAxis, double cylinderHeight, double cylinderRadius)
        {
            if (!Vector3D.IsUnit(ref cylinderAxis))
            {
                cylinderAxis = Vector3D.Normalize(cylinderAxis);
            }
            Vector3D dirn = position - cylinderCenterPosition;
            double height = Vector3D.Dot(dirn, cylinderAxis);
            Vector3D azimuth = dirn - height * cylinderAxis;
            double distance = azimuth.Length();

            return position + Vector3D.Normalize(azimuth) * (cylinderRadius - distance);
        }
    }
}