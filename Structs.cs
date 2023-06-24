using System;
using System.Collections.Generic;

using VRageMath;

namespace SEBR_NAMESPACE
{
    /// <summary>
    /// Struct <c>SEBR_STAGE</c> has all the critical variables that define a battle royale "stage". Usually filled out by method <c>GenerateStages</c>.
    /// </summary>
    public struct SEBR_STAGE
    {
        public int duration;
        public float finalRadius;
        public Vector3D location;
        public DateTime expirationTime;

        public SEBR_STAGE(int duration, float finalRadius, Vector3D location, DateTime expirationTime)
        {
            this.duration = duration;
            this.finalRadius = finalRadius;
            this.location = location;
            this.expirationTime = expirationTime;
        }
    }

    /// <summary>
    /// Struct <c>ZoneSyncPacket</c> is a weird other-form that packages all the SEBR_STAGEs together to send from server to client.
    /// </summary>
    public struct SEBR_PACKET
    {
        public DateTime currentTime;
        public int currentStage;
        public List<SEBR_STAGE> stages;
        public bool isZone;
    }
}