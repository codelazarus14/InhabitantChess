using UnityEngine;

namespace InhabitantChess
{
    [RequireComponent(typeof(OWCamera))]
    public class OverheadCameraController : MonoBehaviour
    {
        public OWCamera OverheadCam;

        private Vector2 _position;
        private const float Height = 3f, PanSpeed = 1.5f, MaxPanDistance = 0.5f;
        //private float _initSnapTime, _snapDuration, _snapTargetX, 
        //    _snapTargetY, _initSnapDegreesX, _initSnapDegreesY;
        //private bool _isSnapping;

        public void ResetPosition()
        {
            _position = Vector3.zero;
        }

        public void Setup()
        {
            OverheadCam = GetComponent<OWCamera>();
        }

        private void Update()
        {
            //if (_isSnapping)
            //{
            //    float num = Mathf.InverseLerp(_initSnapTime, _initSnapTime + _snapDuration, Time.unscaledTime);
            //    if (num >= 1f)
            //    {
            //        _isSnapping = false;
            //    }
            //    float posX = Mathf.Lerp(_initSnapDegreesX, _snapTargetX, num);
            //    float posY = Mathf.Lerp(_initSnapDegreesY, _snapTargetY, num);
            //    _position = new Vector2(posX, posY);
            //}
            transform.localPosition = Vector3.Lerp(transform.localPosition, new Vector3(_position.x, Height, _position.y), 0.1f);
        }

        private void LateUpdate()
        {
            if (OverheadCam != null && !OWTime.IsPaused() /*&& !_isSnapping*/)
            {
                if (OWInput.IsPressed(InputLibrary.moveXZ))
                {
                    Vector2 vector = OWInput.GetAxisValue(InputLibrary.moveXZ);
                    // flipped, camera is rotated 270 on creation (InhabitantChess) to face board correctly
                    _position.x -= vector.y * PanSpeed * Time.deltaTime;
                    _position.y += vector.x * PanSpeed * Time.deltaTime;
                    if (_position.sqrMagnitude > MaxPanDistance * MaxPanDistance)
                    {
                        _position = _position.normalized * MaxPanDistance;
                    }
                }
            }
        }

        // from PlayerCameraController - unused if we're only gonna reset position on enter/exit camera
        //public void SnapToDegreesOverSeconds(float targetX, float targetY, float duration)
        //{
        //    if (duration < Time.deltaTime)
        //    {
        //        _position.x = targetX;
        //        _position.y = targetY;
        //        return;
        //    }
        //    _initSnapTime = Time.unscaledTime;
        //    _snapDuration = duration;
        //    _isSnapping = true;
        //    _snapTargetX = targetX;
        //    _snapTargetY = targetY;
        //    _initSnapDegreesX = _position.x;
        //    _initSnapDegreesY = _position.y;
        //}
    }
}
