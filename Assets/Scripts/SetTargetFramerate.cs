using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFramerate : MonoBehaviour
{
    public AnimationCurve Curve;
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 65;
        Physics.autoSimulation = false;
    }
    //
    // [ContextMenu("Test Build")]
    // public void TestBuild()
    // {
    //     BuildRunner.BuildServer();
    // }

}
