using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Vermetio;

public class BoatsProxyPool : MonoBehaviour
{
    // [SerializeField] private GameObject BoatProxyPrefab;
    private List<GameObject> _proxies;
    private Dictionary<Entity, GameObject> _proxiesOfEntities;

    // Start is called before the first frame update
    void Start()
    {
        _proxies = new List<GameObject>();
        foreach (Transform child in transform)
        {
            _proxies.Add(child.gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        var world = EntityHelpers.GetWorldWith<ServerSimulationSystemGroup>(World.All);

        _proxiesOfEntities = new Dictionary<Entity, GameObject>(50);
        NativeArray<Entity> entities;
        
        // For client all boats except current player, for server just AIAgents
        if (world == null)
        {
            world = EntityHelpers.GetWorldWith<ClientSimulationSystemGroup>(World.All);

            entities = world.EntityManager.CreateEntityQuery(new EntityQueryDesc()
                {
                    All = new ComponentType[] { typeof(ProbyBuoyantComponent) },
                    None = new ComponentType[] { typeof(BoatInput) }
                }) 
                .ToEntityArray(Allocator.Temp);
        }
        else
        {
            entities = world.EntityManager.CreateEntityQuery(new ComponentType [] { typeof (AIAgentComponent)})
                .ToEntityArray(Allocator.Temp);
        }

        for (int i = 0; i < _proxies.Count; i++)
        {
            if (i >= entities.Length)
            {
                _proxies[i].SetActive(false);
                continue;
            }
            
            _proxiesOfEntities.Add(entities[i], _proxies[i]); // for future debugging use
            _proxies[i].SetActive(true);
            #if UNITY_EDITOR
            _proxies[i].GetComponent<AIAgentDebug>().AIAgentEntity = entities[i];
            #endif
            
            var boatPosition = world.EntityManager.GetComponentData<Translation>(entities[i]).Value;
            var boatRotation = world.EntityManager.GetComponentData<Rotation>(entities[i]).Value;
            _proxies[i].transform.SetPositionAndRotation(boatPosition, boatRotation);
        }
    }
}