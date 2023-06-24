using System;
using System.Collections.Generic;

using VRageMath;


namespace SEBR_NAMESPACE
{
    /// <summary>
    /// Class <c>SEBR_CONFIG</c> contains all configurable static values for SEBR and its game states.
    /// </summary>
    public static class SEBR_CONFIG
    {
        public static int squadNumber = 1; // number of players per faction
        public static int gameStage = 2; // stage when game "starts" - doors open, zone appears
        public static int endPossibleStage = 5; // stage when the game is allowed to end by elim
        public static string adminFactionTag = "SEBR"; // this tag is ignored for faction operations
        public static int minPlayers = 5; // number of players to wait for before start
        public static int maxPlayers = 25; // not really important / used, for display purposes only
        public static int syncTime = 1200; // how often we should syncAllPlayers - lower number is faster
        public static bool debug = true; // runs the gauntlet as fast as possible
        public static List<SEBR_STAGE> stages = new List<SEBR_STAGE>()
        {
            // this is just some initial config that gets filled out later - must be in pairs
            // if the radius is the same the ring does not move
            // zeroth stage - starts past this
            new SEBR_STAGE(1,15000f,Vector3D.Zero,DateTime.Today),
            // stages[1] Lobby phase - game does not proceed until minplayers!
            new SEBR_STAGE(180,15000f,Vector3D.Zero,DateTime.Today),
            // stages[gameStage] When this stage starts, teams are assigned and effects play - Game has started!
            new SEBR_STAGE(600,15000f,Vector3D.Zero,DateTime.Today),
            // first shrink set
            new SEBR_STAGE(300,10000f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(300,10000f,Vector3D.Zero,DateTime.Today),
            // Second shrink set - game can end, doors close
            new SEBR_STAGE(180,7500f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(300,7500f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(180,5000f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(300,5000f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(180,2500f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(300,2500f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(60,1000f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(180,1000f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(60,100f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(120,100f,Vector3D.Zero,DateTime.Today),
            // shrinks to nothing
            new SEBR_STAGE(180,0.1f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(60,0.1f,Vector3D.Zero,DateTime.Today)
        };
        public static List<SEBR_STAGE> debugStages = new List<SEBR_STAGE>()
        {
            // this is just some initial config that gets filled out later - must be in pairs
            // if the radius is the same the ring does not move
            // zeroth stage - starts past this
            new SEBR_STAGE(1,15000f,Vector3D.Zero,DateTime.Today),
            // stages[1] Lobby phase - game does not proceed until minplayers!
            new SEBR_STAGE(5,15000f,Vector3D.Zero,DateTime.Today),
            // stages[gameStage] When this stage starts, teams are assigned and effects play - Game has started!
            new SEBR_STAGE(5,15000f,Vector3D.Zero,DateTime.Today),
            // first shrink set
            new SEBR_STAGE(5,10000f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(5,10000f,Vector3D.Zero,DateTime.Today),
            // Second shrink set - game can end, doors close
            new SEBR_STAGE(5,7500f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(5,7500f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(5,5000f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(5,5000f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(5,2500f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(5,2500f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(5,1000f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(5,1000f,Vector3D.Zero,DateTime.Today),
            // test shrink stage
            new SEBR_STAGE(5,100f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(5,100f,Vector3D.Zero,DateTime.Today),
            // shrinks to nothing
            new SEBR_STAGE(5,0.1f,Vector3D.Zero,DateTime.Today),
            new SEBR_STAGE(60,0.1f,Vector3D.Zero,DateTime.Today)
        };
        public static Dictionary<string, string> factions = new Dictionary<string, string>()
        {
            { "DOG", "Squad Canine" },
            { "CAT", "Squad Feline" },
            { "OWL", "Squad Avian" },
            { "FOX", "Squad Vulpine" },
            { "TIG", "Squad Tiger" },
            { "LIO", "Squad Lion" },
            { "BEA", "Squad Bear" },
            { "WOL", "Squad Wolf" },
            { "EAG", "Squad Eagle" },
            { "HAW", "Squad Hawk" },
            { "RHN", "Squad Rhino" },
            { "GIR", "Squad Giraffe" },
            { "HIP", "Squad Hippo" },
            { "COB", "Squad Cobra" },
            { "LEM", "Squad Lemur" },
            { "POR", "Squad Porcupine" },
            { "JAG", "Squad Jaguar" },
            { "ORC", "Squad Orca" },
            { "KAN", "Squad Kangaroo" },
            { "PEL", "Squad Pelican" },
            { "ZEB", "Squad Zebra" },
            { "MOO", "Squad Moose" },
            { "ROO", "Squad Raccoon" },
            { "OPO", "Squad Opossum" },
            { "ANT", "Squad Antelope" }
        };
        public static List<string> hints = new List<string>()
        {
            "D4RKS5B3R is rumored to have a shapely, round ass.",
            "We have tried making SEBR at least four times.",
            "A match lasts around an hour if everyone is bad at the game.",
            "Your drop pod comes loaded with lots of starter supplies and can be salvaged for parts.",
            "Remain calm. Remember: it's only a game.",
            "Large loot areas are called out on the map by purple rectangles.",
            "The bots have feelings too.",
            "Medbays and Survival Kits turn off outside the zone.",
            "You can find many garages with vehicles outside city limits.",
            "Good luck. Have fun.",
            "Press M to switch between map display modes.",
            "Staying outside the zone is pretty dumb.",
            "Decoys attract lightning strikes but are expensive.",
            "Press Shift + R to place a GPS ping on your target and say 'thanks, Klime!'",
            "LastStandGamers can suck my cock.",
            "If you land in a bad spot, respawn and try again!",
            "The lobby doors stay open for a while, but eventually close.",
            "Functional Medbays / Survival Kits inside the zone flag your squad as 'alive'.",
            "Hot take: the previous water mod guy has an enormous cock.",
            "Airdrops tend to drop kind of sort of in the center of the next zone.",
            "Listening to Crab God (10 hour version) can change your life.",
            "There is a GPS marker at the center of the next zone.",
            "The map circles are slightly inaccurate...we're working on that.",
            "Play DotA 2 and develop megacancer.",
            "The frequency of lightning strikes outside the zone increases throughout the game.",
            "You can develop your professional skills by sending bobert money.",
            "If you've got a massive brain, you can move outside between lightning strikes to avoid damage.",
            "Stealing isn't cool, but looting is.",
            "You can just shoot down the airdrops...no need to wait for a late delivery!",
            "To really kill a squad prepare to destroy their grid, medbays, and at least a layer of their family tree.",
        };
    }
}