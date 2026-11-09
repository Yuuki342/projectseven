using UnityEngine;

namespace AnimeOpenWorld
{
    /// <summary>
    /// キャラクターの周りを回る三人称カメラ。壁や地形にめり込まない。
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [Header("距離")]
        public float distance = 6f;
        public float minDistance = 2f;
        public float maxDistance = 14f;
        public float zoomSpeed = 1.5f;

        [Header("回転")]
        public float sensitivity = 2f;
        public bool invertY = false;
        public float minPitch = -35f;
        public float maxPitch = 75f;

        [Header("追従・衝突")]
        public float followSharpness = 18f;
        public float collisionRadius = 0.25f;
        [Tooltip("プレイヤーは Ignore Raycast レイヤーに置く想定")]
        public LayerMask collisionMask = Physics.DefaultRaycastLayers;

        float yaw;
        float pitch = 15f;
        float currentDistance;
        Vector3 focus;
        bool cursorLocked;

        void Start()
        {
            yaw = transform.eulerAngles.y;
            currentDistance = distance;
            if (target != null) focus = target.position + targetOffset;
            SetCursorLocked(true);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            if (GameInput.ReleaseCursorPressed) SetCursorLocked(false);
            else if (!cursorLocked && GameInput.LockCursorPressed) SetCursorLocked(true);

            if (cursorLocked)
            {
                Vector2 look = GameInput.Look * sensitivity;
                yaw += look.x;
                pitch += invertY ? look.y : -look.y;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            distance = Mathf.Clamp(distance - GameInput.Zoom * zoomSpeed, minDistance, maxDistance);

            focus = Vector3.Lerp(focus, target.position + targetOffset, 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rotation * Vector3.back;

            float wanted = distance;
            if (Physics.SphereCast(focus, collisionRadius, dir, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.3f, hit.distance);

            // 近づくときは即座に、離れるときはゆっくり
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-6f * dt));

            transform.SetPositionAndRotation(focus + dir * currentDistance, rotation);
        }

        void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnDisable() => SetCursorLocked(false);
    }
}
