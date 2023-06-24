using System;
using System.Collections.Generic;

using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.Utils;

using static Sandbox.Game.MyVisualScriptLogicProvider;

namespace SEBR_NAMESPACE
{
    /// <summary>
    /// Class <c>SEBR_WEATHER</c> is a sort of client-side pseudo weather that plays a sound and draws fog. Particles were implemented but are unused.
    /// </summary>
    public class SEBR_WEATHER
    {
        // CONSTANTS
        const float FOG_THRESHOLD = 0.8f;
        MyFogProperties heavyRainFog = new MyFogProperties
        {
            FogMultiplier = 0.989f,
            FogDensity = 1f,
            FogColor = new Vector3(0.3f, 0.3f, 0.3f),
            FogSkybox = 1f,
            FogAtmo = 1f
        };
        MyFogProperties lightRainFog = new MyFogProperties
        {
            FogMultiplier = 0.6f,
            FogDensity = 1f,
            FogColor = new Vector3(0.9f, 0.8f, 0.8f),
            FogSkybox = 1f,
            FogAtmo = 1f
        };
        // END CONSTANTS

        int tick = 0;
        int updateRate = 60;
        float intensity = 1f;
        float radius = 0f;
        Vector3D camera = Vector3D.Zero;
        Vector3 velocity = Vector3.Zero;

        bool inCockpit = false;
        bool inShelter = false;
        
        MyEntity3DSoundEmitter soundEmitter;
        MyParticleEffect particleEmitter;
        MySoundPair soundPair;
        MyPlanet planet;
        IMyPlayer player;

        public SEBR_WEATHER(float intensity, float radius, Vector3D position)
        {
            this.intensity = intensity;
            this.radius = radius;
            this.player = MyAPIGateway.Session.Player;
            this.planet = MyGamePruningStructure.GetClosestPlanet(position);
        }

        /// <summary>
        /// Should be called by something in Draw() or equivalent. Updates all SEBR_WEATHER functions...
        /// </summary>
        /// <param name="position"></param>
        public void Update(Vector3D position)
        {
            if (player.Character == null || player.Character.IsDead)
            {
                particleEmitter = null;
                soundEmitter = null;
                return;
            }

            //if (tick % updateRate == 0)
            UpdateWeather(position);
            UpdateSounds(position);
            UpdateFog();
            // UpdateParticles(position);
            
            /*
            MyAPIGateway.Utilities.ShowNotification($"DEBUG:\n" +
                $"inCockpit = {inCockpit}\n" +
                $"inShelter = {inShelter}\n" +
                $"intensity = {intensity}\n",16);
            */

            tick++;
        }

        /// <summary>
        /// Determines intensity of weather based upon position.
        /// </summary>
        /// <param name="position"></param>
        private void UpdateWeather(Vector3D position)
        {
            velocity = Vector3D.Zero;
            camera = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            intensity = 1f;
            double distance = Vector3D.Distance(position, camera);

            // ramp up to edge
            if (distance >= 1 && distance < 300f)
                intensity = (float)Math.Min(1.0 - 0.000000096 * distance * distance * distance + 0.0000525 * distance * distance - 0.0103833 * distance, 1.0);
            else if (distance >= 300f)
                intensity = 0f;

            // fade out above surface
            if (planet == null)
            {
                planet = MyGamePruningStructure.GetClosestPlanet(position);
                intensity = 0f;
            }
            else
            {
                double altitude = Vector3D.Distance(planet.GetClosestSurfacePointGlobal(camera), camera);

                if (altitude > 5000.0)
                    intensity = Math.Max(intensity * (float)(1.0 - (altitude - 5000.0) / 2000.0), 0);
            }

            inShelter = false;

            // if in cockpit
            if (player.Controller?.ControlledEntity?.Entity != null && !(player.Controller.ControlledEntity.Entity is IMyCharacter))
            {
                var obj = player.Controller.ControlledEntity.Entity as IMyCubeBlock;
                if (obj != null)
                    velocity = obj.CubeGrid.Physics.LinearVelocity;

                inCockpit = true;
            }
            else
            {
                if(player.Character.Physics != null)
                    velocity = player.Character.Physics.LinearVelocity;

                IHitInfo hit;
                MyAPIGateway.Physics.CastRay(camera + SEBR_ZONE.ZoneInstance.planetUp, camera + SEBR_ZONE.ZoneInstance.planetUp * 5, out hit);
                if (hit != null && hit.HitEntity != null && hit.HitEntity is IMyCubeGrid)
                    inShelter = true;
            }
        }

        /// <summary>
        /// Draws fog based upon intensity.
        /// </summary>
        private void UpdateFog()
        {
            MyFogProperties lerpedFog;

            if (intensity < FOG_THRESHOLD)
            {
                lerpedFog = MyFogProperties.Default.Lerp(ref lightRainFog, intensity / FOG_THRESHOLD);
            }
            else
                lerpedFog = lightRainFog.Lerp(ref heavyRainFog, (intensity - FOG_THRESHOLD) / (1f - FOG_THRESHOLD));

            MatrixD mat = MyAPIGateway.Session.Camera.WorldMatrix;
            Color fog = new Color(lerpedFog.FogColor.X, lerpedFog.FogColor.Y, lerpedFog.FogColor.Z, intensity/2f);
            //MySimpleObjectDraw.DrawTransparentSphere(ref mat, 0.1f, ref fog, MySimpleObjectRasterizer.Solid, 12, MyStringId.GetOrCompute("Square"));
            //MyTransparentGeometry.AddBillboardOriented(null, fog, mat.Translation + mat.Forward * 0.00001, mat.Left, mat.Up, 1f);

            IMyWeatherEffects weatherEffects = MyAPIGateway.Session.WeatherEffects;
            weatherEffects.FogMultiplierOverride = lerpedFog.FogMultiplier;
            weatherEffects.FogDensityOverride = lerpedFog.FogDensity;
            weatherEffects.FogColorOverride = lerpedFog.FogColor;
            weatherEffects.FogSkyboxOverride = lerpedFog.FogSkybox;
            weatherEffects.FogAtmoOverride = lerpedFog.FogAtmo;
        }

        /// <summary>
        /// Plays sounds based upon intensity and whether or not you're indoors or in a cockpit.
        /// </summary>
        /// <param name="position"></param>
        private void UpdateSounds(Vector3D position)
        {
            if (soundEmitter == null || soundPair == null)
            {
                soundEmitter = new MyEntity3DSoundEmitter((MyEntity)MyAPIGateway.Session.Player.Character);
                soundPair = new MySoundPair("WM_Sandstorm");
            }

            if (soundEmitter == null)
                return;

            soundEmitter.SetPosition(position);

            if(inCockpit)
                soundEmitter.VolumeMultiplier = intensity * 0.2f;
            else if (inShelter)
                soundEmitter.VolumeMultiplier = soundEmitter.VolumeMultiplier * 0.99f + intensity * 0.4f * 0.01f;
            else
                soundEmitter.VolumeMultiplier = soundEmitter.VolumeMultiplier * 0.99f + intensity * 0.01f;

            if (!soundEmitter.IsPlaying && intensity > 0)
            {
                soundEmitter.PlaySound(soundPair);
            }
            else if (intensity == 0)
            {
                soundEmitter.StopSound(true);
            }
        }

        /// <summary>
        /// Updates particles...not polished and currently unused.
        /// </summary>
        /// <param name="position"></param>
        private void UpdateParticles(Vector3D position)
        {
            MatrixD mat = MatrixD.CreateWorld(camera, Vector3D.CalculatePerpendicularVector(SEBR_ZONE.ZoneInstance.planetUp), SEBR_ZONE.ZoneInstance.planetUp);//player.Character.WorldMatrix; // MatrixD.CreateFromAxisAngle();//SEBR_ZONE.ZoneInstance.planetUp, 0.0);
            Vector3 vector = MyUtils.GetRandomVector3Normalized() * MyUtils.GetRandomFloat(0f, radius);
            mat.Translation += vector + velocity;
            //Vector3 velocity = inCockpit ? player.Controller.ControlledEntity.Entity.Physics.LinearVelocity : !inCockpit ? player.Character.Physics.LinearVelocity : Vector3.Zero;
            //mat.Translation += vector + velocity * 60f / 60f;

            //MatrixD mat = MatrixD.CreateWorld(player.Character.WorldMatrix.Translation, SEBR_ZONE.ZoneInstance.planetUp, Vector3D.CalculatePerpendicularVector(SEBR_ZONE.ZoneInstance.planetUp));

            if (particleEmitter == null)
            {
                MyParticlesManager.TryCreateParticleEffect("ZoneRain", ref mat, ref position, uint.MaxValue, out particleEmitter);
            }

            if (particleEmitter == null)
                return;

            if (intensity > 0)
            {
                particleEmitter.WorldMatrix = mat;
                particleEmitter.UserScale = 1f;
                particleEmitter.UserBirthMultiplier = intensity * 20;
                //particleEmitter.Velocity = SEBR_ZONE.ZoneInstance.planetUp * -50.0 * (1.0 + (double)intensity);
                particleEmitter.Play();
            }
            else if (intensity == 0)
            {
                particleEmitter.StopEmitting(1f);
            }
        }
    }
}