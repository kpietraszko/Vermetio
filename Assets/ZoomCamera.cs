using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vermetio.Client
{
    public class ZoomCamera : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            var virtualCam = GetComponent<CinemachineVirtualCamera>();
            if (Mouse.current?.scroll.y.CheckStateIsAtDefaultIgnoringNoise() == true)
                return;

            var transposer = virtualCam.GetCinemachineComponent<CinemachineFramingTransposer>();
            transposer.m_CameraDistance +=
                Mouse.current.scroll.y.ReadValue() * Time.deltaTime / 120f * 400f * -1f;

            transposer.m_CameraDistance = math.clamp(transposer.m_CameraDistance, 10f, 500f);
        }
    }
}
