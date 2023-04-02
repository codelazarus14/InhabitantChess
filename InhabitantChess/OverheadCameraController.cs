using System;
using UnityEngine;
using Logger = InhabitantChess.Util.Logger;

namespace InhabitantChess
{
    // lightweight version of MapController for overhead view
    public class OverheadCameraController : MonoBehaviour
    {
        public OWCamera OverheadCamera;

        private Vector3 _position;
        private float _maxPanDistance = 1f;

        public void SetEnabled(bool isEnabled)
        {
            _position = Vector3.zero;
            OverheadCamera.enabled = isEnabled;
            enabled = isEnabled;
        }

        public void Setup(int cullingMask)
        {
            OverheadCamera.cullingMask = cullingMask;
            OverheadCamera.aspect = 1.6f;
        }

        private void LateUpdate()
        {
            if (OverheadCamera != null && !OWTime.IsPaused()) 
            {
                Vector2 vector = OWInput.GetAxisValue(InputLibrary.moveXZ);
                Vector3 a = transform.forward * vector.x + transform.up * vector.y;
                _position += a * Time.deltaTime;
                if (_position.sqrMagnitude > _maxPanDistance * _maxPanDistance)
                {
                    _position = _position.normalized * _maxPanDistance;
                }
                transform.localPosition = _position;
            }
        }
    }
}
