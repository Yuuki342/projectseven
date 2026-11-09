using UnityEngine;

namespace AnimeOpenWorld
{
    /// <summary>
    /// オープンワールド向けの三人称キャラクター操作。
    /// 走る / ダッシュ / ジャンプ / 空中で滑空 / 泳ぐ / スタミナ。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("参照")]
        public Transform cameraTransform;
        [Tooltip("任意。Speed(float) / Grounded, Gliding, Swimming(bool) パラメータがあれば自動で更新します")]
        public Animator animator;
        [Tooltip("滑空中だけ表示するオブジェクト (グライダーなど)")]
        public GameObject gliderVisual;

        [Header("移動")]
        public float runSpeed = 6.5f;
        public float sprintSpeed = 10.5f;
        public float acceleration = 10f;
        [Range(0f, 1f)] public float airControl = 0.35f;
        public float turnSharpness = 12f;

        [Header("ジャンプ・重力")]
        public float jumpHeight = 1.5f;
        public float gravity = -25f;
        [Tooltip("足場を離れてもジャンプできる猶予")]
        public float coyoteTime = 0.12f;
        [Tooltip("着地直前のジャンプ入力を受け付ける猶予")]
        public float jumpBufferTime = 0.15f;

        [Header("滑空 (空中でジャンプボタン)")]
        public float glideSpeed = 9f;
        public float glideFallSpeed = -2.5f;
        [Tooltip("地面からこれ以上離れていないと滑空できない")]
        public float minGlideHeight = 1.5f;

        [Header("水泳")]
        [Tooltip("水面の高さ。WorldStreamer があれば自動で設定されます")]
        public float waterLevel = 0f;
        [Tooltip("足元が水面からこの深さより下になると泳ぎ状態")]
        public float swimDepth = 1.1f;
        public float swimSpeed = 3.5f;

        [Header("スタミナ")]
        public float maxStamina = 100f;
        public float sprintCost = 12f;
        public float glideCost = 8f;
        public float staminaRegen = 25f;
        public float regenDelay = 0.8f;

        [Header("その他")]
        [Tooltip("この高さより下に落ちたら復帰地点に戻す")]
        public float respawnBelowY = -80f;

        public float Stamina { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsGliding { get; private set; }
        public bool IsSwimming { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsExhausted { get; private set; }
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

        CharacterController controller;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressedTime = float.NegativeInfinity;
        float lastStaminaUseTime = float.NegativeInfinity;
        Vector3 spawnPoint;

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int GroundedHash = Animator.StringToHash("Grounded");
        static readonly int GlidingHash = Animator.StringToHash("Gliding");
        static readonly int SwimmingHash = Animator.StringToHash("Swimming");
        bool hasSpeed, hasGrounded, hasGliding, hasSwimming;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Stamina = maxStamina;
            spawnPoint = transform.position;
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            CacheAnimatorParameters();
        }

        /// <summary>CharacterController を一時停止して瞬間移動する</summary>
        public void Teleport(Vector3 position, bool setAsSpawnPoint = true)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            IsGliding = false;
            if (setAsSpawnPoint) spawnPoint = position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float now = Time.time;

            IsGrounded = controller.isGrounded;
            if (IsGrounded)
            {
                lastGroundedTime = now;
                IsGliding = false;
            }

            // 水面付近で状態がバタつかないよう、出入りの判定に幅を持たせる
            float swimY = waterLevel - swimDepth;
            IsSwimming = IsSwimming ? transform.position.y < swimY + 0.25f : transform.position.y < swimY;
            if (IsSwimming) IsGliding = false;

            bool jumpPressed = GameInput.JumpPressed;
            if (jumpPressed) lastJumpPressedTime = now;

            Vector3 moveDir = GetCameraRelativeInput();
            bool hasInput = moveDir.sqrMagnitude > 0.0001f;

            // --- 水平移動 ---
            IsSprinting = GameInput.Sprint && hasInput && !IsExhausted && (IsGrounded || IsSwimming);
            float targetSpeed;
            if (IsSwimming) targetSpeed = swimSpeed * (IsSprinting ? 1.6f : 1f);
            else if (IsGliding) targetSpeed = glideSpeed;
            else targetSpeed = IsSprinting ? sprintSpeed : runSpeed;

            Vector3 targetVelocity = moveDir * targetSpeed;
            if (IsGliding && !hasInput) targetVelocity = transform.forward * (glideSpeed * 0.6f); // 滑空中は前に進み続ける

            float control = (IsGrounded || IsSwimming || IsGliding) ? 1f : airControl;
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, 1f - Mathf.Exp(-acceleration * control * dt));

            // --- ジャンプ / 滑空 ---
            bool jumped = false;
            if (!IsSwimming && now - lastJumpPressedTime <= jumpBufferTime && now - lastGroundedTime <= coyoteTime)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                jumped = true;
            }
            else if (jumpPressed && !IsGrounded && !IsSwimming)
            {
                lastJumpPressedTime = float.NegativeInfinity; // 着地時に誤ってジャンプしないように
                if (IsGliding) IsGliding = false;
                else if (!IsExhausted && Stamina > 0f && !Physics.Raycast(transform.position, Vector3.down, minGlideHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    IsGliding = true;
            }

            // --- 垂直移動 ---
            if (IsSwimming)
            {
                // 浮力で水面に向かって浮く
                float targetVy = (swimY - transform.position.y) * 5f;
                verticalVelocity = Mathf.Lerp(verticalVelocity, targetVy, 1f - Mathf.Exp(-4f * dt));
            }
            else if (IsGrounded && verticalVelocity < 0f && !jumped)
            {
                verticalVelocity = -2f; // 坂道で地面に吸い付かせる
            }
            else
            {
                verticalVelocity += gravity * dt;
                if (IsGliding) verticalVelocity = Mathf.Max(verticalVelocity, glideFallSpeed);
            }

            UpdateStamina(dt, now);

            CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            // --- 向き ---
            Vector3 face = hasInput ? moveDir : (IsGliding ? horizontalVelocity : Vector3.zero);
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSharpness * dt));
            }

            if (transform.position.y < respawnBelowY) Teleport(spawnPoint, false);

            if (gliderVisual != null && gliderVisual.activeSelf != IsGliding) gliderVisual.SetActive(IsGliding);
            UpdateAnimator();
        }

        void UpdateStamina(float dt, float now)
        {
            float cost = 0f;
            if (IsGliding) cost = glideCost;
            else if (IsSprinting && horizontalVelocity.sqrMagnitude > 1f) cost = sprintCost;

            if (cost > 0f)
            {
                Stamina -= cost * dt;
                lastStaminaUseTime = now;
                if (Stamina <= 0f)
                {
                    Stamina = 0f;
                    IsExhausted = true;
                    IsGliding = false;
                    IsSprinting = false;
                }
            }
            else if (now - lastStaminaUseTime > regenDelay)
            {
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * dt);
            }

            if (IsExhausted && Stamina >= maxStamina * 0.35f) IsExhausted = false;
        }

        Vector3 GetCameraRelativeInput()
        {
            Vector2 input = GameInput.Move;
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        void CacheAnimatorParameters()
        {
            if (animator == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (p.nameHash == GroundedHash && p.type == AnimatorControllerParameterType.Bool) hasGrounded = true;
                if (p.nameHash == GlidingHash && p.type == AnimatorControllerParameterType.Bool) hasGliding = true;
                if (p.nameHash == SwimmingHash && p.type == AnimatorControllerParameterType.Bool) hasSwimming = true;
            }
        }

        void UpdateAnimator()
        {
            if (animator == null) return;
            if (hasSpeed) animator.SetFloat(SpeedHash, horizontalVelocity.magnitude);
            if (hasGrounded) animator.SetBool(GroundedHash, IsGrounded);
            if (hasGliding) animator.SetBool(GlidingHash, IsGliding);
            if (hasSwimming) animator.SetBool(SwimmingHash, IsSwimming);
        }
    }
}
