using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mythica
{
    // The island camera: drag to pan, pinch or scroll to zoom, tap to interact.
    // Works with a mouse and with touch, and ignores anything that starts on the UI.
    public class CameraRig : MonoBehaviour
    {
        const float Pitch = 48f;
        const float MinDistance = 9f, MaxDistance = 24f, DefaultDistance = 18f;
        const float PanLimit = 6.5f;
        const float TapSlop = 12f;   // pixels a press can wander and still count as a tap

        public Camera Cam { get; private set; }
        Vector3 target, targetGoal;
        float distance = DefaultDistance, distanceGoal = DefaultDistance;

        bool pressing, dragging, pressOnUI, multiTouch;
        Vector2 pressPos, lastPos;
        float lastPinch;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        static readonly Vector3 Home = new Vector3(0, 0, 1f);

        public static CameraRig Create()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.78f, 0.96f);
            cam.fieldOfView = 45f;
            cam.cullingMask &= ~(1 << Portraits.Layer);
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            rig.ResetView();
            rig.Apply();
            return rig;
        }

        public void ResetView()
        {
            target = targetGoal = Home;
            distance = distanceGoal = Screen.height > Screen.width ? MaxDistance : DefaultDistance;
        }

        void Apply()
        {
            var dir = Quaternion.Euler(Pitch, 0, 0) * Vector3.back;
            transform.position = target + dir * distance;
            transform.LookAt(target);
        }

        public void Zoom(float amount)
        {
            distanceGoal = Mathf.Clamp(distanceGoal + amount, MinDistance, MaxDistance);
        }

        bool OverUI(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = pos };
            uiHits.Clear();
            EventSystem.current.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        void LateUpdate()
        {
            HandleInput();
            target = Vector3.Lerp(target, targetGoal, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            distance = Mathf.Lerp(distance, distanceGoal, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            Apply();
        }

        void HandleInput()
        {
            // Pinch to zoom with two fingers. A pinch never counts as a tap.
            if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                if (!multiTouch || a.phase == TouchPhase.Began || b.phase == TouchPhase.Began) lastPinch = d;
                multiTouch = true;
                if (!pressOnUI && lastPinch > 0f)
                    distanceGoal = Mathf.Clamp(distanceGoal * lastPinch / Mathf.Max(1f, d), MinDistance, MaxDistance);
                lastPinch = d;
                dragging = true;
                return;
            }

            // Scroll wheel or trackpad zoom, unless the pointer is over a scrolling menu.
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !OverUI(Input.mousePosition)) Zoom(-wheel * 1.2f);

            // One finger or the mouse (touches are mirrored to the mouse on WebGL and mobile).
            if (Input.GetMouseButtonDown(0))
            {
                pressing = true;
                dragging = false;
                pressPos = lastPos = Input.mousePosition;
                pressOnUI = OverUI(pressPos);
            }
            if (pressing && Input.GetMouseButton(0))
            {
                Vector2 pos = Input.mousePosition;
                if (!pressOnUI && !multiTouch)
                {
                    if (!dragging && (pos - pressPos).magnitude > TapSlop * Mathf.Max(1f, Screen.dpi / 160f)) dragging = true;
                    if (dragging) Pan(pos - lastPos);
                }
                lastPos = pos;
            }
            if (pressing && Input.GetMouseButtonUp(0))
            {
                pressing = false;
                if (!pressOnUI && !dragging && !multiTouch && GameManager.Instance != null)
                    GameManager.Instance.Tap(Cam.ScreenPointToRay(Input.mousePosition));
            }
            if (Input.touchCount == 0 && !Input.GetMouseButton(0)) multiTouch = false;
        }

        // Drag the island under the finger.
        void Pan(Vector2 delta)
        {
            float worldPerPixel = 2f * distance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            var right = transform.right;
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            // The ground is tilted away from the camera, so vertical drags cover more ground.
            targetGoal -= (right * delta.x + forward * delta.y * 1.35f) * worldPerPixel;
            var offset = targetGoal - Home;
            offset.y = 0;
            if (offset.magnitude > PanLimit) offset = offset.normalized * PanLimit;
            targetGoal = Home + offset;
        }
    }
}
