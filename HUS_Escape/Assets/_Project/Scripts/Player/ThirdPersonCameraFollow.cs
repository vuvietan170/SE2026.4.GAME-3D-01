using UnityEngine;
using UnityEngine.InputSystem;

namespace HUSEscape.Player
{
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCameraFollow : MonoBehaviour
    {
        private const string LookActionName = "Look";

        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Orbit")]
        [SerializeField, Min(0.1f)] private float distance = 6.3f;
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 65f;

        [Header("Focus")]
        [SerializeField] private Vector3 lookAtOffset =
            new Vector3(0f, 1.2f, 0f);

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.08f;
        [SerializeField, Min(0f)] private float rotationSharpness = 12f;

        [Header("Environment collision")]
        [Tooltip("Solid layers that block the camera. Target descendants and triggers are always ignored.")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [Tooltip("Minimum probe radius. Enlarged automatically to protect the near clip plane.")]
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
        [SerializeField, Min(0.005f)] private float collisionSkin = 0.03f;
        [SerializeField, Min(0.01f)] private float collisionRecoveryTime = 0.18f;

        private InputAction lookAction;
        private GameplayInputGate inputGate;
        private Vector3 positionVelocity;
        private Camera viewCamera;
        private SphereCollider overlapProbe;
        private RaycastHit[] castHits = new RaycastHit[16];
        private Collider[] overlaps = new Collider[16];
        private readonly Vector3[] nearCorners = new Vector3[4];
        private float boomDistance;
        private float boomVelocity;

        private float yaw;
        private float pitch;

        private void Awake()
        {
            if (target == null || inputActions == null)
            {
                Debug.LogError(
                    "Camera requires Target and Input Actions.", this);
                enabled = false;
                return;
            }

            inputGate = target.GetComponent<GameplayInputGate>();
            if (inputGate == null && target.TryGetComponent<PlayerController>(out var controller)) inputGate = controller.InputGate;
            inputGate?.Configure(inputActions, this);
            lookAction = inputGate != null ? inputGate.PlayerAction(LookActionName) : null;

            if (lookAction == null)
            {
                Debug.LogError(
                    "Player/Look action was not found.", this);
                enabled = false;
                return;
            }

            Vector3 angles = transform.eulerAngles;

            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            boomDistance = distance;
            viewCamera = GetComponent<Camera>();
            // Disabled and unsaved: used only as a shape for ComputePenetration, never for simulation.
            overlapProbe = gameObject.AddComponent<SphereCollider>();
            overlapProbe.enabled = false;
            overlapProbe.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        }

        private void LateUpdate()
        {
            if (target == null || lookAction == null || inputGate == null || inputGate.IsBlocked || Time.deltaTime <= 0f)
                return;

            float deltaTime = Time.deltaTime;

            Vector2 lookInput = lookAction.ReadValue<Vector2>();

            yaw += lookInput.x * sensitivity;

            pitch -= lookInput.y * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Quaternion orbitRotation =
                Quaternion.Euler(pitch, yaw, 0f);

            Vector3 focusPoint = target.position + lookAtOffset;

            // Doors move by Transform in Update. Queries must see this frame's geometry.
            Physics.SyncTransforms();

            float radius = GetCollisionRadius();
            Vector3 safeFocus = ResolveOverlaps(focusPoint, radius);
            Vector3 backward = -(orbitRotation * Vector3.forward);
            Vector3 unobstructed = LimitLine(safeFocus, safeFocus + backward * distance, radius);
            float availableDistance = Vector3.Distance(safeFocus, unobstructed);

            if (availableDistance < boomDistance)
            {
                boomDistance = availableDistance;
                boomVelocity = 0f;
            }
            else
            {
                boomDistance = Mathf.SmoothDamp(boomDistance, availableDistance,
                    ref boomVelocity, collisionRecoveryTime);
            }

            Vector3 desiredPosition = safeFocus + backward * boomDistance;

            Vector3 candidate = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime
            );

            // Recheck the smoothed path: smoothing itself can cut through corners or lag behind a closing door.
            Vector3 corrected = LimitLine(safeFocus, candidate, radius);
            corrected = ResolveOverlaps(corrected, radius);
            corrected = LimitLine(safeFocus, corrected, radius);
            if ((corrected - candidate).sqrMagnitude > 0.000001f)
            {
                positionVelocity = Vector3.zero;
                boomDistance = Mathf.Min(boomDistance, Vector3.Distance(safeFocus, corrected));
                boomVelocity = 0f;
            }
            transform.position = corrected;

            Vector3 direction = focusPoint - transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction, Vector3.up);

                float blend =
                    1f - Mathf.Exp(-rotationSharpness * deltaTime);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    blend
                );
            }
        }

        private float GetCollisionRadius()
        {
            float radius = collisionRadius;
            if (viewCamera != null)
            {
                viewCamera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f),
                    viewCamera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearCorners);
                for (int i = 0; i < nearCorners.Length; i++)
                    radius = Mathf.Max(radius, nearCorners[i].magnitude);
            }
            return radius;
        }

        private bool BlocksCamera(Collider obstacle)
        {
            return obstacle != null && !obstacle.isTrigger &&
                !obstacle.transform.IsChildOf(target) &&
                !obstacle.transform.IsChildOf(transform);
        }

        private Vector3 LimitLine(Vector3 origin, Vector3 destination, float radius)
        {
            Vector3 delta = destination - origin;
            float length = delta.magnitude;
            if (length < 0.0001f)
                return destination;

            int count;
            while (true)
            {
                count = Physics.SphereCastNonAlloc(origin, radius, delta / length, castHits,
                    length, collisionLayers, QueryTriggerInteraction.Ignore);
                if (count < castHits.Length)
                    break;
                System.Array.Resize(ref castHits, castHits.Length * 2);
            }
            float safeLength = length;
            for (int i = 0; i < count; i++)
                if (BlocksCamera(castHits[i].collider))
                    safeLength = Mathf.Min(safeLength, Mathf.Max(0f, castHits[i].distance - collisionSkin));
            return origin + delta / length * safeLength;
        }

        private Vector3 ResolveOverlaps(Vector3 position, float radius)
        {
            if (overlapProbe == null)
                return position;

            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)),
                Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            overlapProbe.radius = radius / maxScale;
            // A cast cannot detect an obstacle enclosing its origin. Resolve those initial overlaps too.
            for (int pass = 0; pass < 8; pass++)
            {
                int count;
                while (true)
                {
                    count = Physics.OverlapSphereNonAlloc(position, radius, overlaps,
                        collisionLayers, QueryTriggerInteraction.Ignore);
                    if (count < overlaps.Length)
                        break;
                    System.Array.Resize(ref overlaps, overlaps.Length * 2);
                }
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = overlaps[i];
                    if (BlocksCamera(obstacle) && Physics.ComputePenetration(
                        overlapProbe, position, Quaternion.identity,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out Vector3 normal, out float depth) && depth > 0f)
                    {
                        position += normal * (depth + collisionSkin);
                        moved = true;
                    }
                }
                if (!moved)
                    break;
            }
            return position;
        }

        private void OnDestroy()
        {
            if (overlapProbe != null)
                Destroy(overlapProbe);
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
                angle -= 360f;

            return angle;
        }

        private void OnValidate()
        {
            distance = Mathf.Max(0.1f, distance);
            sensitivity = Mathf.Max(0f, sensitivity);
            maxPitch = Mathf.Max(minPitch + 1f, maxPitch);
            positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            collisionSkin = Mathf.Max(0.005f, collisionSkin);
            collisionRecoveryTime = Mathf.Max(0.01f, collisionRecoveryTime);
        }
    }
}
