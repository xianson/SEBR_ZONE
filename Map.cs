using System;
using System.Collections.Generic;
using System.Linq;

using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

using VRage.Input;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRageRender;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace SEBR_NAMESPACE
{
    /// <summary>
    /// Class <c>SEBR_MAP</c> generates the map. Oh god, no comments here. Don't look too closely or your eyes will begin to sizzle.
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class SEBR_MAP : MySessionComponentBase
    {
        MyStringId draw_id;
        MyStringId circ_id;
        MyStringId nextCirc_id;
        MyStringId hexagon_id;
        Vector3D fwd = Vector3D.Forward;
        Vector3D rgt = Vector3D.Right;
        Vector3D up = Vector3D.Down;
        int showMap = 0;
        List<bool> keyHistory = new List<bool>() { false, false, false, false, false };
        float offset = -1f;
        int tick = 0;
        double mapLength = 0.021;
        Vector2 view = Vector2.Zero;
        double cameraDistance = 0.03;
        MyPlanet planet;
        double resolutionScale = 0.074 / 4000;

        public override void BeforeStart()
        {
            draw_id = MyStringId.GetOrCompute("minimap_test");
            circ_id = MyStringId.GetOrCompute("ring");
            nextCirc_id = MyStringId.GetOrCompute("ring_dotted");
            hexagon_id = MyStringId.GetOrCompute("arrow_white");
        }

        public override void Draw()
        {
            if (MyAPIGateway.Session.Player?.Character == null || MyAPIGateway.Session.Camera == null ||
                !(MyAPIGateway.Session.Config.HudState == 1 || MyAPIGateway.Session.Config.HudState == 2) ||
                SEBR_ZONE.ZoneInstance.isDedicated)
                return;

            var cam = MyAPIGateway.Session.Camera.WorldMatrix;

            // jank code to satisfy dark saber's garbage.
            if (planet == null)
            {
                List<IMyVoxelBase> voxelMaps = new List<IMyVoxelBase>();
                MyAPIGateway.Session.VoxelMaps.GetInstances(voxelMaps, (IMyVoxelBase voxelEnt) => { return voxelEnt is MyPlanet; });
                foreach(var vox in voxelMaps)
                {
                    planet = vox as MyPlanet;
                    break;
                }
                return;
            }

            float fov = MyAPIGateway.Session.Camera.FieldOfViewAngle;
            view = MyAPIGateway.Session.Camera.ViewportSize;
            cameraDistance = mapLength / Math.Tan(fov * Math.PI / 360);

            Vector3D relative_position_3d = Vector3D.Normalize(cam.Translation - planet.PositionComp.GetPosition());
            Vector2 relative_position_2d = alignVector(cubizePoint3(relative_position_3d), rgt, up);

            int nextStage = SEBR_ZONE.ZoneInstance.currentStage + 1;
            if (nextStage >= SEBR_ZONE.ZoneInstance.SEBR_STAGES.Count - 1)
                nextStage = SEBR_ZONE.ZoneInstance.SEBR_STAGES.Count - 1;

            Vector3D nextCirclePos_3d = Vector3D.Normalize(SEBR_ZONE.ZoneInstance.SEBR_STAGES[nextStage].location - planet.PositionComp.GetPosition());
            Vector3D circlePos_3d = Vector3D.Normalize(SEBR_ZONE.ZoneInstance.currentLocation - planet.PositionComp.GetPosition());

            Vector2 nextCirclePos_2d = alignVector(cubizePoint3(nextCirclePos_3d), rgt, up);
            Vector2 circlePos_2d = alignVector(cubizePoint3(circlePos_3d), rgt, up);

            if (keyHistory.All(x => !x) && MyAPIGateway.Input.IsKeyPress(MyKeys.M))
            {
                showMap++;
                if (showMap == 3) { showMap = 0; }
            }

            keyHistory.Insert(0, MyAPIGateway.Input.IsKeyPress(MyKeys.M));
            keyHistory.RemoveAt(keyHistory.Count - 1);

            if (showMap == 1)
            {
                displayMegaMap(relative_position_2d, nextCirclePos_2d, circlePos_2d, nextStage);
            }
            else if (showMap == 0)
            {
                displayMiniMap(relative_position_2d, nextCirclePos_2d, circlePos_2d);
            }
        }

        void displayMegaMap(Vector2 relative_position_2d, Vector2 nextCirclePos_2d, Vector2 circlePos_2d, int nextStage)
        {
            float x_wiggle = -0.0005f;
            float y_wiggle = -0.0003f;
            var uv_center = relative_position_2d;
            float scale = 0.2f;
            float size = 0.016f;
            var cam = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D map_to_world = cam.Translation + cam.Forward * (cameraDistance - 0.001) + cam.Right * (-uv_center.X * size / (1 - 2 * scale) + x_wiggle) + cam.Up * (uv_center.Y * size / (1 - 2 * scale) + y_wiggle);

            // map stuff

            Vector3D p0 = cam.Translation + cam.Forward * cameraDistance + cam.Left * size + cam.Up * size;
            Vector3D p1 = cam.Translation + cam.Forward * cameraDistance + cam.Left * size + cam.Down * size;
            Vector3D p2 = cam.Translation + cam.Forward * cameraDistance + cam.Right * size + cam.Down * size;
            Vector3D l1 = cam.Translation + cam.Forward * cameraDistance + cam.Right * size + cam.Up * size;
            Vector3D n0 = Vector3D.Zero;

            Vector2 uv0 = new Vector2(scale, scale);
            Vector2 uv1 = new Vector2(scale, 1 - scale);
            Vector2 uv2 = new Vector2(1 - scale, 1 - scale);
            Vector2 lv1 = new Vector2(1 - scale, scale);

            float angle = (float)GetAngle(cam.Forward, fwd, up);
            MatrixD matrix = MatrixD.CreateFromAxisAngle(cam.Forward, angle);

            MyTransparentGeometry.AddTriangleBillboard(p2, l1, p0, n0, n0, n0, uv0, uv1, uv2, draw_id, uint.MaxValue, cam.Translation, BlendTypeEnum.PostPP);
            MyTransparentGeometry.AddTriangleBillboard(p2, p1, p0, n0, n0, n0, uv0, lv1, uv2, draw_id, uint.MaxValue, cam.Translation, BlendTypeEnum.PostPP);
            MyTransparentGeometry.AddBillboardOriented(hexagon_id, Color.SkyBlue, map_to_world, Vector3D.Rotate(cam.Left, matrix), Vector3D.Rotate(cam.Up, matrix), 0.0002f, 0.0004f, Vector2.Zero, BlendTypeEnum.PostPP);

            // circle stuff
            var rad = SEBR_ZONE.ZoneInstance.currentRadius * 0.0002f / 250f;
            Vector3D map_to_world_circ = cam.Translation + cam.Forward * (cameraDistance - 0.002) + cam.Right * (-circlePos_2d.X * size / (1 - 2 * scale) + x_wiggle) + cam.Up * (circlePos_2d.Y * size / (1 - 2 * scale) + y_wiggle);
            MyTransparentGeometry.AddBillboardOriented(circ_id, Color.Red, map_to_world_circ, cam.Left, cam.Up, rad, rad, Vector2.Zero, BlendTypeEnum.PostPP); // * zone.ZoneInstance.currentRadius / 100f

            // next circle stuff

            rad = SEBR_ZONE.ZoneInstance.SEBR_STAGES[nextStage].finalRadius * 0.0002f / 250f;
            Vector3D map_to_world_circ_next = cam.Translation + cam.Forward * (cameraDistance - 0.002) + cam.Right * (-nextCirclePos_2d.X * size / (1 - 2 * scale) + x_wiggle) + cam.Up * (nextCirclePos_2d.Y * size / (1 - 2 * scale) + y_wiggle);
            MyTransparentGeometry.AddBillboardOriented(nextCirc_id, Color.Red, map_to_world_circ_next, cam.Left, cam.Up, rad, rad, Vector2.Zero, BlendTypeEnum.PostPP); // * zone.currentRadius / 100f
        }

        // no zone! for sanity reasons
        void displayMiniMap(Vector2 relative_position_2d, Vector2 nextCirclePos_2d, Vector2 circlePos_2d)
        {
            relative_position_2d = fixPos(relative_position_2d);
            float x_wiggle = 0.01f;
            float y_wiggle = -0.0053f;
            float m_size = 0.01f;
            var uv_center = relative_position_2d + new Vector2(x_wiggle, y_wiggle);
            var cam = MyAPIGateway.Session.Camera.WorldMatrix;
            float map_scale = 0.1f * (float)Math.Pow(Vector3D.Distance(cam.Translation, planet.PositionComp.GetPosition()) / planet.MinimumRadius,3.5);

            Vector3D map_to_world = cam.Translation + cam.Forward * cameraDistance + cam.Right * (view.X * resolutionScale / 2 - m_size / 2) + cam.Down * (view.Y * resolutionScale / 2 - m_size / 2);

            Vector3D p0 = cam.Translation + cam.Forward * cameraDistance + cam.Right * (view.X * resolutionScale / 2 - m_size) + cam.Down * (view.Y * resolutionScale / 2 - m_size);
            Vector3D p1 = cam.Translation + cam.Forward * cameraDistance + cam.Right * (view.X * resolutionScale / 2 - m_size) + cam.Down * (view.Y * resolutionScale / 2);
            Vector3D p2 = cam.Translation + cam.Forward * cameraDistance + cam.Right * (view.X * resolutionScale / 2) + cam.Down * (view.Y * resolutionScale / 2);
            Vector3D l1 = cam.Translation + cam.Forward * cameraDistance + cam.Right * (view.X * resolutionScale / 2) + cam.Down * (view.Y * resolutionScale / 2 - m_size);

            Vector3D n0 = Vector3D.Zero;
            Vector2 uv0 = uv_center + Vector2.UnitX * -0.3f * map_scale + Vector2.UnitY * -0.3f * map_scale;
            Vector2 uv1 = uv_center + Vector2.UnitX * -0.3f * map_scale + Vector2.UnitY * 0.3f * map_scale;
            Vector2 uv2 = uv_center + Vector2.UnitX * 0.3f * map_scale + Vector2.UnitY * 0.3f * map_scale;
            Vector2 lv1 = uv_center + Vector2.UnitX * 0.3f * map_scale + Vector2.UnitY * -0.3f * map_scale;

            float angle = (float)GetAngle(cam.Forward, fwd, up);
            MatrixD matrix = MatrixD.CreateFromAxisAngle(cam.Forward, angle);

            MyTransparentGeometry.AddTriangleBillboard(p2, l1, p0, n0, n0, n0, uv0, uv1, uv2, draw_id, uint.MaxValue, cam.Translation, BlendTypeEnum.PostPP);
            MyTransparentGeometry.AddTriangleBillboard(p2, p1, p0, n0, n0, n0, uv0, lv1, uv2, draw_id, uint.MaxValue, cam.Translation, BlendTypeEnum.PostPP);
            MyTransparentGeometry.AddBillboardOriented(hexagon_id, Color.SkyBlue, map_to_world, Vector3D.Rotate(cam.Left, matrix), Vector3D.Rotate(cam.Up, matrix), 0.0002f, 0.0004f, Vector2.Zero, BlendTypeEnum.PostPP);
        }

        Vector2 alignVector(Vector3D point, Vector3D right, Vector3D up)
        {
            Vector2 result;
            result.X = (float)Vector3D.Dot(point, right);
            result.Y = (float)Vector3D.Dot(point, up);
            return result;
        }

        Vector2 fixPos(Vector2 pos)
        {
            pos += new Vector2(1f, 1f);
            pos /= 2;
            return pos;
        }

        Vector3D cubizePoint3(Vector3D position)
        {
            // Yes its this fuckin simple im so mad - Whip
            return position / position.AbsMax();
        }

        double GetAngle(Vector3D direction, Vector3D normal, Vector3D up)
        {
            var projection = direction - Vector3D.Dot(direction, normal) * normal;

            double angle = MyMath.AngleBetween(projection, up);
            angle *= Math.Sign(Vector3D.Dot(Vector3D.Cross(up, projection), normal));
            return angle;
        }
    }
}