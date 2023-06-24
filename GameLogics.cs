using System;
using System.Collections.Generic;

using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

using VRage.Input;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

/* The game works as follows: a faction must either have players on planet or a functional medbay in the zone to be considered "alive". */

namespace SEBR_NAMESPACE
{
    /// <summary>
    /// Class <c>SEBR_SKIT</c> manages SKITs ingame and adds them to our Zone list.
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SurvivalKit))]
    public class SEBR_SKIT : MyGameLogicComponent
    {
        bool added = false;
        IMyFunctionalBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyFunctionalBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!added) 
            {
                SEBR_ZONE.ZoneInstance.medbays.Add(block);
                added = true;
            }
        }

        public override void Close()
        {
            SEBR_ZONE.ZoneInstance.medbays.Remove(block);
        }
    }

    /// <summary>
    /// Class <c>SEBR_MBAY</c> manages medbays ingame and adds them to our Zone list.
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MedicalRoom))]
    public class SEBR_MBAY : MyGameLogicComponent
    {
        bool added = false;
        IMyFunctionalBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyFunctionalBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

        }

        public override void UpdateOnceBeforeFrame()
        {
            if (!added)
            {
                SEBR_ZONE.ZoneInstance.medbays.Add(block);
                added = true;
            }
        }

        public override void Close()
        {
            SEBR_ZONE.ZoneInstance.medbays.Remove(block);
        }
    }

    /// <summary>
    /// Class <c>SEBR_DOOR</c> manages lobby doors. Open sesame.
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Door), false, "SEBR_GATE")]
    public class SEBR_DOOR : MyGameLogicComponent
    {
        IMyDoor door;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            door = Entity as IMyDoor;
            base.Init(objectBuilder);
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (door == null)
                return;

            Sandbox.ModAPI.Ingame.IMyDoor ingameDoor = (Sandbox.ModAPI.Ingame.IMyDoor)door;
            if (SEBR_ZONE.ZoneInstance.currentStage >= SEBR_ZONE.ZoneInstance.START_STAGE && SEBR_ZONE.ZoneInstance.currentStage < SEBR_ZONE.ZoneInstance.END_POSSIBLE_STAGE)
                ingameDoor.OpenDoor();
            else
                ingameDoor.CloseDoor();
        }
    }

    /// <summary>
    /// Class <c>SEBR_DOOR</c> creates behaviour for a special gyro that always aligns up. Zeppelins much?
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "SEBR_GYRO")]
    public class SEBR_GYRO : MyGameLogicComponent
    {
        IMyCubeBlock block;
        double pitch_error = 0;
        double roll_error = 0;
        const double quarterCycle = Math.PI / 2;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = Entity as IMyCubeBlock;
            base.Init(objectBuilder);
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (block == null || block.IsFunctional == false || block.CubeGrid.Physics == null || block.CubeGrid.Physics.Gravity == Vector3D.Zero)
                return;

            block.SlimBlock.DoDamage(0.1f, MyDamageType.Bullet, true);

            Vector3D up = -Vector3D.Normalize(block.CubeGrid.Physics.Gravity);
             
            pitch_error = VectorAngleBetween(block.WorldMatrix.Forward, up) - quarterCycle;
            roll_error = VectorAngleBetween(block.WorldMatrix.Right, up) - quarterCycle;

            //apply angular acceelrations here
            Vector3D angularVel = block.CubeGrid.Physics.AngularVelocity;
            angularVel += block.WorldMatrix.Right * pitch_error * 0.05;
            angularVel += -block.WorldMatrix.Forward * roll_error * 0.05;

            block.CubeGrid.Physics.AngularVelocity = angularVel;

            block.CubeGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, null, block.CubeGrid.Physics.CenterOfMassWorld, angularVel);   
        }

        private double VectorAngleBetween(Vector3D a, Vector3D b)
        { //returns radians
          //Law of cosines to return the angle between two vectors.

            if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
        }

    }

    /// <summary>
    /// Class <c>SEBR_BEACON</c> creates behaviour for a special beacon that spews smoke.
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Beacon), false, "SEBR_BEACON")]
    public class SEBR_BEACON : MyGameLogicComponent
    {
        IMyBeacon beacon;
        MyParticleEffect emitter;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            beacon = Entity as IMyBeacon;
            base.Init(objectBuilder);
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (beacon == null || beacon.Enabled == false || beacon.IsFunctional == false)
            {
                if(emitter != null)
                {
                    emitter.StopEmitting(1f);
                }
                return;
            }

            beacon.HudText = $"[AIRDROP]";
            beacon.Radius = 15000f;
            if (!SEBR_ZONE.ZoneInstance.isDedicated)
                manageParticleEffect();
        }

        public override void Close()
        {
            if(emitter != null)
                emitter.Stop();
        }

        private void manageParticleEffect()
        {
            MatrixD mat = beacon.WorldMatrix;
            Vector3D pos = mat.Translation;

            if (emitter == null)
            {
                MyParticlesManager.TryCreateParticleEffect("AirdropSmoke", ref mat, ref pos, uint.MaxValue, out emitter);
                return;
            }

            emitter.SetTranslation(ref pos);
            emitter.WorldMatrix = mat;
            emitter.Play();
        }
    }

    /// <summary>
    /// Class <c>SEBR_BEACON</c> creates behaviour for our zoneblock and sends it the zone session.
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "bobzone")]
    public class SEBR_ZONEBLOCK : MyGameLogicComponent
    {
        IMyFunctionalBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyFunctionalBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (block.CubeGrid.Physics != null && (SEBR_ZONE.ZoneInstance.isLocalHost || SEBR_ZONE.ZoneInstance.isDedicated))
                SEBR_ZONE.ZoneInstance.zoneBlock = block;
        }

        public override void Close()
        {
            if (SEBR_ZONE.ZoneInstance.isLocalHost || SEBR_ZONE.ZoneInstance.isDedicated)
                SEBR_ZONE.ZoneInstance.zoneBlock = null;
        }
    }
}