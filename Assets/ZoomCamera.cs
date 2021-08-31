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
            #if UNITY_EDITOR
            var view = Camera.main.ScreenToViewportPoint(Mouse.current.position.ReadValue());
            var isOutside = view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1;
            if (isOutside)
                return;
            #endif
            
            var virtualCam = GetComponent<CinemachineVirtualCamera>();
            if (Mouse.current == null) // probably true on server
                return;
            
            if (Mouse.current.scroll.y.CheckStateIsAtDefaultIgnoringNoise())
                return;

            var transposer = virtualCam.GetCinemachineComponent<CinemachineFramingTransposer>();
            transposer.m_CameraDistance +=
                Mouse.current.scroll.y.ReadValue() * Time.deltaTime / 120f * 400f * -1f;

            transposer.m_CameraDistance = math.clamp(transposer.m_CameraDistance, 10f, 500f);
        }
    }
}
