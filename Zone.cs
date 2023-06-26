using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Sandbox.Game;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

using VRage.Input;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using VRageRender;

using static Sandbox.Game.MyVisualScriptLogicProvider;

/// <summary>
/// SEBR has been a weird passion project for a couple of years. Here's how to start it up.
///     - must have a planet
///     - must have an enabled zoneblock
/// 
/// How does it play?
///     - zone shrinks in stages
///     - lightning hits players outside zones, distracted by decoys
///     - squad is considered alive if they have a player alive on the planet or a medbay in the zone
///     - Game cannot end until a certain configurable stage
/// 
/// </summary>
namespace SEBR_NAMESPACE
{
    /// <summary>
    /// Class <c>SEBR_ZONE</c> 
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class SEBR_ZONE : MySessionComponentBase
    {
        #region TOP_LEVEL_VARS
        // THESE ARE CONFIGURED BY SEBR_CONFIG
        public int SQUAD_NUMBER = SEBR_CONFIG.squadNumber;
        public int START_STAGE = SEBR_CONFIG.gameStage;
        public int END_POSSIBLE_STAGE = SEBR_CONFIG.endPossibleStage;
        public string ADMIN_FACTION_TAG = SEBR_CONFIG.adminFactionTag;
        public int MINIMUM_PLAYERS_START = SEBR_CONFIG.minPlayers;
        public int MAX_PLAYERS = SEBR_CONFIG.maxPlayers;
        public int SYNC_TICK_TIME = SEBR_CONFIG.syncTime;
        public bool DEBUG = SEBR_CONFIG.debug;
        public List<SEBR_STAGE> SEBR_STAGES = SEBR_CONFIG.stages;
        public List<string> AI_ENABLED_FACTIONS = SEBR_CONFIG.aiEnabledFactions;
        private List<SEBR_STAGE> DEBUG_STAGES = SEBR_CONFIG.debugStages;
        public Dictionary<string, string> SEBR_FACTIONS = SEBR_CONFIG.factions;
        private List<string> SEBR_HINTS = SEBR_CONFIG.hints;
        // END CONFIGURED VALUES

        public static SEBR_ZONE ZoneInstance;

        public bool isLocalHost;
        public bool isDedicated;
        public bool isServer;
        public bool isClient;

        public bool isZoneBlock = false;
        public float currentRadius = 0.1f; //read
        public Vector3D currentLocation = Vector3D.Zero;
        public IMyFunctionalBlock zoneBlock;
        public List<IMyFunctionalBlock> medbays = new List<IMyFunctionalBlock>();
        public int currentStage = 1;
        public Vector3D planetUp = Vector3D.Zero;
        public DateTime currentTime;

        private HashSet<string> factionsAlive = new HashSet<string>();
        private int lightningTick = 0;
        private double cylinderHeight = 10000;
        private int altitudeExclude = 33000;
        private Dictionary<long,IMyPlayer> playerList = new Dictionary<long,IMyPlayer>();
        private Random random = new Random();
        private const float updateRate = 1 / 60f;
        private const ushort msgtag = 17586;
        private bool first = true;
        private double timeDifference = 0;
        private IMyGps gps;
        private MyParticleEffect emitter;
        private Vector3D lastWeatherLocation = Vector3D.Zero;
        private MyPlanet planet;
        private DateTime lastZoneEntry = DateTime.UtcNow;
        private bool firstZoneEntry = false;
        private long syncTick = 0;
        private int seedIndex = 0;
        private int hintIndex = 0;
        private DateTime lastHint = DateTime.UtcNow;
        private List<long> illegalFactions = new List<long>();
        private SEBR_WEATHER weather;
        #endregion

        #region OVERRIDE_UPDATES

        /// <summary>
        /// Method <c>UnloadData</c> runs once before the game starts. Need to register all events here.
        /// </summary>
        public override void BeforeStart()
        {
            ZoneInstance = this;

            isLocalHost = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
            isDedicated = MyAPIGateway.Utilities.IsDedicated;
            isServer = isLocalHost || isDedicated;
            isClient = !isServer;

            seedIndex = (int)(random.NextDouble() * (SEBR_HINTS.Count - 1));
            MyAPIGateway.Entities.OnEntityAdd += HandleEntityAdded;
            MyAPIGateway.Session.Factions.FactionCreated += HandleFactionCreated;
            MyAPIGateway.Session.Factions.FactionStateChanged += HandlePlayerFactionChanged;

            // lest we forget...
            if (isDedicated)
                DEBUG = false;

            if (isServer)
            {
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, HandleLightningDamage);
                MyVisualScriptLogicProvider.PlayerConnected += HandlePlayerJoined;
            }
            else
                MyAPIGateway.Multiplayer.RegisterMessageHandler(msgtag, HandleMessageRecieved);
        }

        /// <summary>
        /// Method <c>UpdateAfterSimulation</c> happens every tick after simulation has occurred. El Classico.
        /// </summary>
        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session == null)
                return;

            GeneratePlayerList();
            UpdateCriticalParts();

            if (!isZoneBlock || planet == null || currentStage == SEBR_STAGES.Count)
            {
                first = true;
                return;
            }

            InitializeZone();
            UpdateStage();
            UpdateIllegalFactions();
            UpdateFactions();
            UpdateBlurbs();

            if (currentStage < START_STAGE || currentStage == SEBR_STAGES.Count)
                return;

            UpdateZone();
            UpdatePlayers();
            UpdateMedbays();
            UpdateWinConditions();
        }

        /// <summary>
        /// Method <c>Draw</c> occurs every....frame? Do your visual stuff here, bozo.
        /// </summary>
        public override void Draw()
        {
            if (currentStage < START_STAGE || currentStage == SEBR_STAGES.Count)
                return;

            UpdateVisuals();
        }

        /// <summary>
        /// Method <c>UnloadData</c> runs once when the game is shutting down. Need to unregister all events here.
        /// </summary>
        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= HandleEntityAdded;
            MyAPIGateway.Session.Factions.FactionCreated -= HandleFactionCreated;
            MyAPIGateway.Session.Factions.FactionStateChanged -= HandlePlayerFactionChanged;

            if (isServer)
                MyVisualScriptLogicProvider.PlayerConnected -= HandlePlayerJoined;
            else
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(msgtag, HandleMessageRecieved);

            ZoneInstance = null;
        }


        #endregion

        #region SEBR_UPDATES

        /// <summary>
        /// Method <c>IniitializeZone</c> performs a cleanup / initialization process that runs before the game starts.
        /// </summary>
        public void InitializeZone()
        {
            if (!first)
                return;

            first = false;
            currentStage = 1;
            currentTime = DateTime.UtcNow;
            if (!isDedicated) // server does not need gps or particles
            {
                if (gps == null)
                {
                    // local human never null when not ds
                    foreach (IMyGps oldGPS in MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId))
                    {
                        MyAPIGateway.Session.GPS.RemoveLocalGps(oldGPS);
                    }
                    gps = MyAPIGateway.Session.GPS.Create("ZONE", "YEET", Vector3D.Zero, true, false);
                    gps.GPSColor = Color.Red;
                }
            }

            if (isDedicated || isLocalHost)
            {
                GenerateStages();
                SyncAllPlayers();
            }

            string name;

            foreach (var faction in MyAPIGateway.Session.Factions.Factions.Values)
            {
                if (!SEBR_FACTIONS.TryGetValue(faction.Tag, out name) && !AI_ENABLED_FACTIONS.Contains(faction.Tag))
                    illegalFactions.Add(faction.FactionId);
            }

            MyLog.Default.WriteLineAndConsole($"[ZONE] My heart is beating!");
        }

        /// <summary>
        /// Method <c>UpdateCriticalParts</c> is the janny method that happens every tick. It tracks if the zoneblock / planet live.
        ///     Calls the server sync method... also updates tome.
        /// </summary>
        private void UpdateCriticalParts()
        {
            if (isServer)
            {
                isZoneBlock = (zoneBlock != null && zoneBlock.Enabled);
                if (isZoneBlock)
                    planet = MyGamePruningStructure.GetClosestPlanet(zoneBlock.WorldMatrix.Translation);
                currentTime = DateTime.UtcNow;

                syncTick++;
                if (syncTick % SYNC_TICK_TIME == 0)
                {
                    syncTick = 0;
                    SyncAllPlayers();
                }
            }
            else if (currentStage < SEBR_STAGES.Count)
            {
                currentTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(timeDifference));
                planet = MyGamePruningStructure.GetClosestPlanet(SEBR_STAGES[currentStage].location);
            }
        }

        /// <summary>
        /// Method <c>UpdateEffects</c> Updates all
        /// </summary>
        private void UpdateBlurbs()
        {
            // Game has not started yet
            TimeSpan span = (SEBR_STAGES[currentStage].expirationTime - currentTime);
            if (currentStage < START_STAGE)
            {
                MyAPIGateway.Utilities.ShowNotification($"SEBR will attempt to start in { span.Minutes:00}:{ span.Seconds:00}", 16, "Green");
                MyAPIGateway.Utilities.ShowNotification($"{ playerList.Count}/{ MAX_PLAYERS}, minimum is { MINIMUM_PLAYERS_START }", 16, "Green");

                if ((currentTime - lastHint).TotalSeconds > 5)
                {
                    lastHint = DateTime.UtcNow;
                    hintIndex++;
                }

                int index = (hintIndex + seedIndex) % SEBR_HINTS.Count;

                MyAPIGateway.Utilities.ShowNotification($"\nTIP: {SEBR_HINTS[index]}", 16, "White");

            }
            else if (currentStage == SEBR_STAGES.Count - 1)
                MyAPIGateway.Utilities.ShowNotification($"SEBR will restart in { span.Minutes:00}:{ span.Seconds:00}", 16, "Red");
        }

        /// <summary>
        /// Method <c>UpdateVisuals</c> generates the funky fresh visuals like the cylinder and particle emitters and gps crap that the server doesn't care about.
        /// </summary>
        private void UpdateVisuals()
        {
            if (isDedicated)
                return;

            Vector3D cam = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            MatrixD mat = MatrixD.CreateWorld(currentLocation, planetUp, Vector3D.CalculatePerpendicularVector(planetUp));

            if (gps != null)
            {
                if (currentStage < SEBR_STAGES.Count - 1)
                {
                    TimeSpan span = (SEBR_STAGES[currentStage].expirationTime - currentTime);
                    gps.Coords = SEBR_STAGES[currentStage + 1].location;
                    gps.Name = $"Size {Math.Round(SEBR_STAGES[currentStage + 1].finalRadius):#####}\n" +
                               $"Time  {span.Minutes}:{span.Seconds:00}\n" +
                               $"Teams    {factionsAlive.Count,2:#0}\n\n\n";
                    if (Vector3D.Distance(cam, SEBR_STAGES[currentStage + 1].location) < SEBR_STAGES[currentStage + 1].finalRadius && currentStage <= SEBR_STAGES.Count - 1)
                        gps.GPSColor = Color.SkyBlue;
                    else
                        gps.GPSColor = Color.Red;
                }
                else if (currentStage == SEBR_STAGES.Count - 1)
                {
                    gps.Coords = SEBR_STAGES[currentStage].location;
                    gps.Name = $"[ZONE] Game over!";
                    gps.GPSColor = Color.Green;
                }

                MyAPIGateway.Session.GPS.RemoveLocalGps(gps);
                if (currentStage >= START_STAGE)
                    MyAPIGateway.Session.GPS.AddLocalGps(gps);
            }

            if (emitter == null)
            {
                MyParticlesManager.TryCreateParticleEffect("ZoneSmoke", ref mat, ref currentLocation, uint.MaxValue, out emitter);
            }
            Vector3D stormCenter = currentLocation - planetUp * cylinderHeight/2;

            // costs 0.1 ms
            if (emitter != null)
            {
                emitter.SetTranslation(ref stormCenter);
                emitter.UserRadiusMultiplier = 10000f / (float)Math.Pow((double)currentRadius, 0.7) / 3f;
                emitter.UserBirthMultiplier = 1 / emitter.UserRadiusMultiplier + 0.1f;
                emitter.UserScale = currentRadius * 0.97f;
                emitter.Play();
            }

            if (weather == null)
                weather = new SEBR_WEATHER(1f, 10f, cam);

            if (!SEBR_UTILS.IsPositionInCylinder(cam, currentLocation, planetUp, cylinderHeight, currentRadius))
            {
                // play lightning sound on edge
                if (firstZoneEntry && (DateTime.UtcNow - lastZoneEntry).TotalSeconds > 5)
                {
                    MyVisualScriptLogicProvider.PlaySoundAmbientLocal("WM_Lightning");
                    firstZoneEntry = false;
                    lastZoneEntry = DateTime.UtcNow;
                }
                weather.Update(cam);
            }
            else
            {
                Vector3D edge = SEBR_UTILS.GenerateClosestPositionOnCylinder(cam, currentLocation, planetUp, cylinderHeight, currentRadius);
                weather.Update(edge);
                firstZoneEntry = true;
            }

            // costs 0.05-0.1 ms
            var eep = new Vector4(100f, 1f, 1f, 1f);
            mat = MatrixD.CreateWorld(currentLocation, Vector3D.CalculatePerpendicularVector(planetUp), planetUp);
            //MySimpleObjectDraw.DrawTransparentCylinder(ref mat, currentRadius, currentRadius, (float)cylinderHeight, ref eep, true, 50, currentRadius / 100f);
        }

        /// <summary>
        /// Method <c>UpdateMedbays</c> manages medbays - turning them off outside the zone and adding them to the count of alive factions if they are not.
        /// </summary>
        private void UpdateMedbays()
        {
            if (isDedicated)
                return;
            foreach (IMyFunctionalBlock block in medbays)
            {
                if (block == null || block.MarkedForClose)
                    continue;

                string temp;

                IMyCubeBlock medCube = block as IMyCubeBlock;
                if (medCube.GetOwnerFactionTag() == ADMIN_FACTION_TAG || !SEBR_FACTIONS.TryGetValue(medCube.GetOwnerFactionTag(), out temp))
                    continue;

                if (!SEBR_UTILS.IsPositionInCylinder(block.WorldMatrix.Translation, currentLocation, planetUp, cylinderHeight, currentRadius))
                {
                    block.Enabled = false;
                }
                else if ((isServer) && block.IsFunctional && block.Enabled) // if we're the server and a medbay is on planet, add medbay faction to list of fcts
                {
                    IMyCubeBlock cube = (IMyCubeBlock)block;
                    factionsAlive.Add(cube.GetOwnerFactionTag());
                }
            }
        }

        /// <summary>
        /// Method <c>UpdatePlayers</c> manages players - adding them to the count of factions alive and spawning lightning if they are naughty.
        /// </summary>
        private void UpdatePlayers()
        {
            factionsAlive.Clear();

            lightningTick++;
            int lightningFrequency = 60 * (SEBR_STAGES.Count - currentStage); // this evaluates to 900 at gamestart
            altitudeExclude = (int)planet.MinimumRadius + (int)(cylinderHeight / 2);

            // if our player is on the planet, add his faction to the list of factions alive
            foreach (IMyPlayer player in playerList.Values)
            {
                if (player == null || player.Character == null)
                    continue;

                Vector3D position = player.Character.WorldMatrix.Translation;
                bool inSOI = Vector3D.Distance(position, planet.PositionComp.GetPosition()) < altitudeExclude;

                // if players are on planet add them to faction alive list
                if (inSOI)
                {
                    var tempFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);

                    string temp;

                    if (tempFaction != null && tempFaction.Tag != ADMIN_FACTION_TAG && SEBR_FACTIONS.TryGetValue(tempFaction.Tag, out temp))
                        factionsAlive.Add(tempFaction.Tag);

                    if (!SEBR_UTILS.IsPositionInCylinder(position, currentLocation, planetUp, cylinderHeight, currentRadius) && lightningTick > lightningFrequency && (isServer))
                    {
                        var energyLevel = MyVisualScriptLogicProvider.GetPlayersEnergyLevel(player.Identity.IdentityId);
                        MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.Identity.IdentityId, (float)Math.Max(energyLevel - 0.1f,0));
                        GenerateZoneLightning(player, position);
                    }
                }
            }
            if (lightningTick > lightningFrequency)
            {
                lightningTick = 0;
            }
        }

        /// <summary>
        /// Method <c>GeneratePlayerList</c> tries to get an accurate list of players without MES bots.
        /// </summary>
        private void GeneratePlayerList()
        {
            playerList.Clear();

            if (MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE)
            {
                playerList.Add(0L,MyAPIGateway.Session.Player);
                return;
            }

            List<IMyPlayer> temporaryList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(temporaryList);

            foreach(IMyPlayer player in temporaryList)
            {
                if (SEBR_UTILS.IsPlayerBot(player))
                    continue;

                playerList.Add(player.IdentityId, player);
            }
        }

        /// <summary>
        /// Method <c>UpdateFactions</c> prevents players from creating their own factions and shoves them into available factions.
        /// </summary>
        private void UpdateFactions()
        {
            if (isClient)
                return;

            List<IMyPlayer> NoFactionPlayers = new List<IMyPlayer>();

            foreach (var player in playerList.Values)
            {
                if (player.Character != null)
                {
                    var tempFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                    if (tempFaction == null)
                    {
                        var AllFactions = MyAPIGateway.Session.Factions.Factions;
                        foreach (var factionKey in AllFactions.Keys)
                        {
                            if (AllFactions[factionKey].Members.Count < SQUAD_NUMBER + 1 && !AI_ENABLED_FACTIONS.Contains(AllFactions[factionKey].Tag))
                            {
                                MyAPIGateway.Session.Factions.SendJoinRequest(AllFactions[factionKey].FactionId, player.IdentityId);
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Method <c>GameOver</c> handles a game over occurence.
        /// </summary>
        private void UpdateWinConditions()
        {
            if (currentStage < END_POSSIBLE_STAGE || factionsAlive.Count != 1 || currentStage == SEBR_STAGES.Count - 1 || DEBUG)
                return;

            string winner = factionsAlive.Single();

            IMyFaction fac = MyAPIGateway.Session.Factions.TryGetFactionByTag(winner);

            if (fac == null)
                return;

            MyVisualScriptLogicProvider.ShowNotificationToAll("WINNER WINNER CHICKEN DINNER!", 16 * 60 * 10, "Green");

            foreach (KeyValuePair<long, MyFactionMember> pair in fac.Members)
                foreach (IMyPlayer player in playerList.Values)
                    if (player != null && player.IdentityId == pair.Value.PlayerId)
                        MyVisualScriptLogicProvider.ShowNotificationToAll($"{player.DisplayName} survived!", 16 * 60 * 10, "Green");

            MyVisualScriptLogicProvider.ShowNotificationToAll($"{fac.Name} has won!\n", 16 * 60 * 10, "Green");

            MyLog.Default.WriteLineAndConsole($"{fac.Name} has won! Shutting down the server soon...");

            if (isServer)
            {
                currentStage = SEBR_STAGES.Count - 1;
                SEBR_STAGE stage = new SEBR_STAGE();
                DateTime newTime = DateTime.UtcNow;
                stage.expirationTime = newTime.AddSeconds((double)SEBR_STAGES[currentStage].duration);
                stage.location = SEBR_STAGES[currentStage].location;
                stage.duration = SEBR_STAGES[currentStage].duration;
                stage.finalRadius = SEBR_STAGES[currentStage].finalRadius;

                SEBR_STAGES[currentStage] = stage;
                SyncAllPlayers();
            }
        }

        /// <summary>
        /// Method <c>UpdateStage</c> handles what happens when a stage expires / begins.
        /// </summary>
        private void UpdateStage()
        {
            if (currentTime < SEBR_STAGES[currentStage].expirationTime || isClient)
                return;

            // if the game has started, proceed per normal
            if (currentStage >= START_STAGE)
                currentStage++;
            // otherwise, if we have minPlayers, start the game
            else if (playerList.Count >= MINIMUM_PLAYERS_START || DEBUG)
                currentStage++;
            // failing that, reset and try again
            else
            {
                first = true;
                InitializeZone();
            }

            if (currentStage == SEBR_STAGES.Count())
            {
                // This is how we restart the server.
                if (isDedicated)
                {
                    throw new System.Exception("Restarting the server via a crash...");
                }
                first = true;
                InitializeZone();
            }

            if (currentStage == 4 || currentStage == 8)
                GenerateAirdrop();

            SyncAllPlayers();
            SEBR_STAGE state = SEBR_STAGES[currentStage];
            MyLog.Default.WriteLineAndConsole($"[ZONE] Syncing all players.\n" +
                $"    Current Stage: {currentStage}\n" +
                $"    Final Radius: {state.finalRadius} meters\n" +
                $"    Final Location: {state.location.ToString("0")}\n" +
                $"    Remaining Time: {Math.Round((state.expirationTime - currentTime).TotalSeconds)}");
        }

        /// <summary>
        /// Method <c>UpdateZone</c> moves and resizes the zone with lerps, as well as some other stuff.
        /// </summary>
        private void UpdateZone()
        {
            SEBR_STAGE stage = SEBR_STAGES[currentStage];
            SEBR_STAGE prevStage = SEBR_STAGES[currentStage - 1];

            float stageSeconds = (float)(currentTime - prevStage.expirationTime).TotalSeconds;
            // Hacky bullshit ... sometimes the client can be ahead of the server?
            if (stageSeconds < 0)
                stageSeconds = 0;
            currentLocation = Vector3D.Lerp(prevStage.location, stage.location, stageSeconds / stage.duration);
            currentLocation = planet.GetClosestSurfacePointGlobal(ref currentLocation);
            planetUp = Vector3D.Normalize(currentLocation - planet.PositionComp.GetPosition());
            currentRadius = MathHelper.Lerp(prevStage.finalRadius, stage.finalRadius, stageSeconds / stage.duration);
            cylinderHeight = (double)currentRadius / 1.5 + 300;
        }

        /// <summary>
        /// Method <c>HandleIllegalFactions</c> tries to kill illegal factions without causing a stink.
        /// </summary>
        private void UpdateIllegalFactions()
        {
            if (isClient)
                return;

            for (int i = 0; i < illegalFactions.Count; i++)
            {
                try
                {
                    MyAPIGateway.Session.Factions.RemoveFaction(illegalFactions[i]);
                    illegalFactions.RemoveAt(i);
                    i--;
                }
                catch (Exception e) { }
            }
        }

        #endregion

        #region GENERATION

        /// <summary>
        /// Method <c>GenerateAirdrop</c> creates an airdrop. The airdrop should go to the center-ish of the next stages. Mileage varies.
        /// </summary>
        private void GenerateAirdrop()
        {
            //MyAPIGateway.Utilities.ShowNotification("Spawned airdrop...", 3000);

            Vector3D startPosition = SEBR_STAGES[currentStage + 1].location + planetUp * Math.Min(cylinderHeight, 5000);
            Vector3D dir = Vector3D.Normalize(SEBR_STAGES[currentStage + 1].location - SEBR_STAGES[currentStage].location + planet.WorldMatrix.Right);
            dir = Vector3D.Cross(dir, planetUp);
            startPosition += dir * SEBR_STAGES[0].finalRadius;
            MyVisualScriptLogicProvider.SpawnPrefab("SEBR_COURIER", startPosition, -dir, planetUp);
        }

        /// <summary>
        /// Method <c>GenerateStages</c> fills out the stages list with randomized locations and generates time stamps.
        /// </summary>
        private void GenerateStages()
        {

            if (DEBUG)
                SEBR_STAGES = DEBUG_STAGES;

            for (int i = 0; i < SEBR_STAGES.Count; i++)
            {
                if (i == 0)
                {
                    SEBR_STAGE stage = new SEBR_STAGE();
                    stage.expirationTime = DateTime.UtcNow.AddSeconds((double)SEBR_STAGES[0].duration);
                    stage.location = zoneBlock.WorldMatrix.Translation;
                    stage.duration = SEBR_STAGES[0].duration;
                    stage.finalRadius = SEBR_STAGES[0].finalRadius;
                    SEBR_STAGES[0] = stage;
                }
                else
                {
                    SEBR_STAGE stage = new SEBR_STAGE();
                    SEBR_STAGE prevStage = SEBR_STAGES[i - 1];

                    DateTime newTime = prevStage.expirationTime;
                    stage.expirationTime = newTime.AddSeconds((double)SEBR_STAGES[i].duration);

                    stage.location = GenerateRandomLocation(prevStage.location, prevStage.finalRadius, SEBR_STAGES[i].finalRadius);

                    stage.duration = SEBR_STAGES[i].duration;
                    stage.finalRadius = SEBR_STAGES[i].finalRadius;
                    SEBR_STAGES[i] = stage;
                }
            }
            currentRadius = SEBR_STAGES[0].finalRadius;
            currentLocation = SEBR_STAGES[0].location;
        }

        /// <summary>
        /// Method <c>GenerateRandomLocation</c> generates a random zone with a distance and angle from the center of the previous zone,
        ///     taking care to ensure that the new zone is inside of the old one. It tries to encourage weird zones.
        /// </summary>
        /// <param name="prevLoc"></param>
        /// <param name="prevRad"></param>
        /// <param name="currentRad"></param>
        /// <returns></returns>
        private Vector3D GenerateRandomLocation(Vector3D prevLoc, float prevRad, float currentRad)
        {
            Vector3D planetCenter = planet.PositionComp.GetPosition();
            Vector3D rightVector = planet.WorldMatrix.Right;
            Vector3D upVector = Vector3D.Normalize(zoneBlock.WorldMatrix.Translation - planetCenter);
            Vector3D forVector = Vector3D.Cross(upVector, rightVector);

            if (planetUp == Vector3D.Zero)
                planetUp = upVector;

            double maxDisplacement = prevRad - currentRad;
            if (maxDisplacement < 0)
                maxDisplacement = 0;
            double randDisplacement = Math.Pow(random.NextDouble(), 0.1) * maxDisplacement;
            double randAngle = random.NextDouble() * Math.PI * 2;

            MatrixD matrix = MatrixD.CreateFromAxisAngle(upVector, randAngle);
            Vector3D currentLoc = Vector3D.Rotate(forVector, matrix) * randDisplacement + prevLoc;
            return planet.GetClosestSurfacePointGlobal(ref currentLoc);
        }

        /// <summary>
        /// Method <c>GenerateZoneLightning</c> smites a bugger.
        /// </summary>
        private void GenerateZoneLightning(IMyPlayer player, Vector3D position)
        {
            IHitInfo hit = null;
            BoundingSphereD sphere = new BoundingSphereD(position, 50.0);
            bool flag = SEBR_UTILS.AreDecoysWithinRadius(ref sphere, ref position);
            var orig = position + planetUp * 30;
            int damage = 1000;

            // if controlled entity is grid, smite properly
            if (player.Controller?.ControlledEntity?.Entity != null && !(player.Controller.ControlledEntity.Entity is IMyCharacter))
            {
                var obj = player.Controller.ControlledEntity.Entity as IMyCubeBlock;
                if (obj != null && obj.CubeGrid.GridSizeEnum == MyCubeSize.Large)
                    damage = 3000;
            }

            // controlled entity can be a character but strike a grid instead
            MyAPIGateway.Physics.CastRay(orig, position - planetUp * 10, out hit);
            if (hit != null && hit.HitEntity != null)
            {
                position = hit.Position;
                if (hit.HitEntity is IMyCubeGrid)
                {
                    IMyCubeGrid grid = hit.HitEntity as IMyCubeGrid;
                    if (grid.GridSizeEnum == MyCubeSize.Large)
                        damage = 3000;
                }
            }

            MyVisualScriptLogicProvider.CreateLightning(position, (float)(cylinderHeight / 2), (byte)30, (short)7, 10f, (short)7, 0);

            MyExplosionTypeEnum explosionType = MyExplosionTypeEnum.CUSTOM;
            MyExplosionInfo myExplosionInfo = new MyExplosionInfo();

            myExplosionInfo.PlayerDamage = 0;
            myExplosionInfo.Damage = damage;
            myExplosionInfo.ExplosionType = explosionType;
            myExplosionInfo.ExplosionSphere = new BoundingSphereD(position, 1);
            myExplosionInfo.LifespanMiliseconds = 100;
            myExplosionInfo.ParticleScale = 0f;
            myExplosionInfo.Direction = planetUp;
            myExplosionInfo.AffectVoxels = false;
            myExplosionInfo.KeepAffectedBlocks = true;
            myExplosionInfo.VoxelExplosionCenter = position;
            myExplosionInfo.ExplosionFlags = MyExplosionFlags.CREATE_DEBRIS | MyExplosionFlags.APPLY_FORCE_AND_DAMAGE | MyExplosionFlags.CREATE_DECALS | MyExplosionFlags.APPLY_DEFORMATION;
            myExplosionInfo.VoxelCutoutScale = 0f;
            myExplosionInfo.PlaySound = false;
            myExplosionInfo.ApplyForceAndDamage = true;
            myExplosionInfo.ObjectsRemoveDelayInMiliseconds = 0;
            myExplosionInfo.StrengthImpulse = 1f;

            MyExplosions.AddExplosion(ref myExplosionInfo);
        }

        #endregion

        #region NETWORK_PROTOCOLS

        /// <summary>
        /// Method <c>SyncAllPlayers</c> syncs every player from GeneratePlayerList.
        /// </summary>
        private void SyncAllPlayers()
        {
            foreach (IMyPlayer player in playerList.Values)
            {
                SyncPlayer(player.IdentityId);
            }
        }

        /// <summary>
        /// Method <c>SyncPlayer</c> sends a packet to a single playerID with information of the stages.
        /// </summary>
        /// <param name="playerId"></param>
        private void SyncPlayer(long playerId)
        {
            var steamId = MyAPIGateway.Players.TryGetSteamId(playerId);
            if (steamId == MyAPIGateway.Multiplayer.ServerId)
                return;

            SEBR_PACKET packet = new SEBR_PACKET();
            packet.currentTime = DateTime.UtcNow;
            packet.currentStage = currentStage;
            packet.stages = SEBR_STAGES;
            packet.isZone = isZoneBlock;

            string message = MyAPIGateway.Utilities.SerializeToXML(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(msgtag, Encoding.Unicode.GetBytes(message), steamId);

            //if (isHost)
            //    MyAPIGateway.Utilities.ShowNotification($"PACKET SENT...\nsc: {stages.Count()}\nisZone: {isZone}\ncurrentStage = {currentStage}", 3000);

        }

        #endregion

        #region EVENT_HANDLING

        /// <summary>
        /// Method <c>HandlePlayerFactionChanged</c> fires when ANY faction event happens, but we filter to just changes in player faction.
        ///     We don't want players to be able to change factions ingame, so if they try to leave, we send them right back.
        /// </summary>
        /// <param name="change"></param>
        /// <param name="fromFactionId"></param>
        /// <param name="factionId"></param>
        /// <param name="playerId"></param>
        /// <param name="senderId"></param>
        private void HandlePlayerFactionChanged(MyFactionStateChange change, long fromFactionId, long factionId, long playerId, long senderId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);
            IMyPlayer player;
            if (faction == null || !playerList.TryGetValue(playerId, out player))
                return;

            // If someone joins
            if (change == MyFactionStateChange.FactionMemberAcceptJoin)
            {
                if (faction.Members.Count > SQUAD_NUMBER + 1)
                    MyAPIGateway.Session.Factions.KickMember(factionId, playerId);
                else
                    MyAPIGateway.Session.Factions.DemoteMember(factionId, playerId);
            }
            // If someone leaves
            else if (change == MyFactionStateChange.FactionMemberKick || change == MyFactionStateChange.FactionMemberLeave)
            {
                if (currentStage >= START_STAGE && faction.Members.Count < SQUAD_NUMBER + 1)
                    MyAPIGateway.Session.Factions.SendJoinRequest(factionId, playerId);
            }
        }

        /// <summary>
        /// Method <c>HandleFactionCreated</c> fires when a faction is created, which is not allowed. Adds them to the list of illegal factions.
        /// </summary>
        /// <param name="factionId"></param>
        private void HandleFactionCreated(long factionId)
        {
            IMyFaction faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);

            if (faction == null)
                return;

            string name;

            if (!SEBR_FACTIONS.TryGetValue(faction.Tag, out name) && !AI_ENABLED_FACTIONS.Contains(faction.Tag))
                illegalFactions.Add(factionId);
        }

        /// <summary>
        /// Method <c>HandleMessageRecieved</c> fires when a player recieves a packet from the server.
        /// </summary>
        /// <param name="messageData"></param>
        private void HandleMessageRecieved(byte[] messageData)
        {
            string message = Encoding.Unicode.GetString(messageData);
            SEBR_PACKET packet = MyAPIGateway.Utilities.SerializeFromXML<SEBR_PACKET>(message);
            currentStage = packet.currentStage;
            SEBR_STAGES = packet.stages;
            isZoneBlock = packet.isZone;
            // holy hell this is bad, does not account for latency, but it DOES get rid of clock drift
            timeDifference = 0;// (DateTime.UtcNow - packet.currentTime).TotalSeconds; TODO: FIX ME

            if (currentStage < SEBR_STAGES.Count)
            {
                SEBR_STAGE state = packet.stages[currentStage];
                //MyAPIGateway.Utilities.ShowNotification($"Packet recieved.\nFinal Radius: {state.finalRadius} meters\n" +
                //    $"Final Location: {state.location.ToString("0")}\n" +
                //    $"Remaining Time: {Math.Round((state.expirationTime - currentTime).TotalSeconds)}");
            }
        }

        /// <summary>
        /// Method <c>HandlePlayerJoined</c> fires when a player joins. Duh, you fucking idiot.
        /// </summary>
        /// <param name="playerId"></param>
        private void HandlePlayerJoined(long playerId)
        {
            SyncPlayer(playerId);
        }

        /// <summary>
        /// Method <c>HandleEntityAdded</c> handles init of grids for ownership.
        /// </summary>
        /// <param name="entity"></param>
        private void HandleEntityAdded(IMyEntity entity)
        {
            if (!(entity is IMyCubeGrid))
                return;

            IMyCubeGrid grid = entity as IMyCubeGrid;

            if (!grid.CustomName.Contains("SEBR") || grid.CustomName.Contains("STATION") || grid.CustomName.Contains("POD"))
                return;

            MyVisualScriptLogicProvider.ChangeOwner(entity.Name, 0L, true, true);
        }

        /// <summary>
        /// This damage handler was created to ensure that players only ever take 33 damage from lightning.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="info"></param>
        private void HandleLightningDamage(object target, ref MyDamageInformation info)
        {
            //MyAPIGateway.Utilities.ShowNotification("yeet!");
            var character = target as IMyCharacter;
            if (character == null || info.Type != MyDamageType.Explosion)
                return;

            info.Amount = 0;
            character.DoDamage(33f, MyDamageType.Bullet, true);
        }

        #endregion
    }
}