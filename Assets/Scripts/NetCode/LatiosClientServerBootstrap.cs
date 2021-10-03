using Latios;
using Unity.Entities;
using Unity.NetCode;

namespace Vermetio.Client.NetCode
{
    public class LatiosClientServerBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            // The default world must be created before generating the system list in order to have a valid TypeManager instance.
            // The TypeManage is initialised the first time we create a world.
            var world = new LatiosWorld(defaultWorldName, WorldFlags.Game);
            World.DefaultGameObjectInjectionWorld = world;

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            GenerateSystemLists(systems);

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, ExplicitDefaultWorldSystems);
#if !UNITY_DOTSRUNTIME
            ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);
#endif

            PlayType playModeType = RequestedPlayType;
            int numClientWorlds = 1;

            int totalNumClients = numClientWorlds;
            if (playModeType != PlayType.Server)
            {
#if UNITY_EDITOR
                int numThinClients = RequestedNumThinClients;
                totalNumClients += numThinClients;
#endif
                for (int i = 0; i < numClientWorlds; ++i)
                {
                    CreateClientWorld(world, "ClientWorld" + i, new LatiosWorld("ClientWorld" + i, WorldFlags.Game));
                }
#if UNITY_EDITOR
                for (int i = numClientWorlds; i < totalNumClients; ++i)
                {
                    var clientWorld = CreateClientWorld(world, "ClientWorld" + i, new LatiosWorld("ClientWorld" + i, WorldFlags.Game));
                    clientWorld.EntityManager.CreateEntity(typeof(ThinClientComponent));
                }
#endif
            }

            if (playModeType != PlayType.Client)
            {
                CreateServerWorld(world, "ServerWorld", new LatiosWorld("ServerWorld", WorldFlags.Game));
            }
            return true;
        }
    }
}