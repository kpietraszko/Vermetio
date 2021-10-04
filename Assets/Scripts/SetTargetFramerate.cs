using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFramerate : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;
    }

    [ContextMenu("Test Build")]
    public void TestBuild()
    {
        BuildRunner.BuildServer();
    }

}
