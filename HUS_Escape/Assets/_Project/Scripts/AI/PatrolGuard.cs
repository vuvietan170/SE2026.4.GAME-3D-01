using System;
using UnityEngine;

namespace HUSEscape.AI
{
    public enum GuardState { Patrol, Suspicious, Alert, Search, Return }

    /// <summary>Shared waypoint/sensing brain for the existing guard and the prototype dog.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PatrolGuard : MonoBehaviour
    {
        public Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField] private float speed = 1.5f, sightRange = 14, fieldOfView = 90, alertDelay = 1.3f, forgetDelay = 3;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField] private Renderer indicator;
        [Header("Stealth V2")]
        [SerializeField] private bool isDog;
        [SerializeField, Min(.1f)] private float chaseSpeed = 3.2f;
        [SerializeField, Min(1)] private float maxChaseDistance = 22, maxChaseTime = 8, searchTime = 4;
        [SerializeField, Min(1)] private float hearingRange = 12, alarmRange = 24;
        [SerializeField] private PatrolGuard alarmRecipient;
        [SerializeField, Min(.1f)] private float captureDistance = 1.8f;
        private bool captureReported;
        public Transform Target => player;
        public event Action<PatrolGuard> PlayerCaught;
        private Transform player;
        private CharacterController controller, playerController;
        private Animator playerAnimator;
        private float visibleTime, unseenTime, nextSense, stateSince, chaseSince, nextBark, reacquireAfter;
        private Vector3 home, lastKnownPosition;
        private MaterialPropertyBlock properties;
        private bool returningFromLimit;
        public GuardState State { get; private set; }
        public bool HasLineOfSight { get; private set; }
        public int WaypointIndex { get; private set; }
        public int CompletedWaypoints { get; private set; }
        public bool IsDog => isDog;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public bool IsBarking => isDog && Time.time < nextBark - 2;

        /// <summary>Explicit scene-local target; no global player lookup or persistent target.</summary>
        public void Bind(Transform target)
        {
            player = target;
            playerController = target != null ? target.GetComponent<CharacterController>() : null;
            playerAnimator = target != null ? target.GetComponentInChildren<Animator>() : null;
            HasLineOfSight = false; visibleTime = unseenTime = 0; captureReported = false;
        }
        private void Awake()
        {
            controller = GetComponent<CharacterController>(); properties = new MaterialPropertyBlock();
            home = transform.position; lastKnownPosition = home; Paint();
        }
        private void OnDisable() { HasLineOfSight = false; }
        private void Update()
        {
            if (Time.deltaTime <= 0 || player == null || !controller.enabled) return;
            if (Time.time >= nextSense)
            {
                nextSense = Time.time + .1f;
                HasLineOfSight = !returningFromLimit && Time.time >= reacquireAfter && CanSee(player);
                if (HasLineOfSight) lastKnownPosition = player.position;
                else if (isDog && State != GuardState.Alert && !returningFromLimit && Time.time >= reacquireAfter &&
                    CanHear(player, MovementSpeed(), IsCrouching())) ReceiveAlarm(player.position);
            }
            if (HasLineOfSight)
            {
                unseenTime = 0; visibleTime += Time.deltaTime;
                if (State != GuardState.Alert) SetState(visibleTime >= alertDelay ? GuardState.Alert : GuardState.Suspicious);
            }
            else { visibleTime = 0; unseenTime += Time.deltaTime; }
            if (State == GuardState.Alert)
            {
                if (Time.time - chaseSince >= maxChaseTime || FlatDistance(home, transform.position) >= maxChaseDistance)
                { returningFromLimit = true; HasLineOfSight = false; SetState(GuardState.Return); }
                else if (!HasLineOfSight && unseenTime >= forgetDelay) SetState(GuardState.Search);
                else
                {
                    if (isDog && HasLineOfSight && Time.time >= nextBark)
                    {
                        nextBark = Time.time + 3;
                        if (alarmRecipient != null && alarmRecipient.isActiveAndEnabled && FlatDistance(transform.position, alarmRecipient.transform.position) <= alarmRange)
                            alarmRecipient.ReceiveAlarm(lastKnownPosition);
                    }
                    if (TryCapture()) return;
                    MoveTo(lastKnownPosition, chaseSpeed, 1.6f); return;
                }
            }
            if (State == GuardState.Suspicious)
            {
                if (!HasLineOfSight && unseenTime >= forgetDelay) SetState(GuardState.Search);
                else { MoveTo(lastKnownPosition, speed, 1.5f); return; }
            }
            if (State == GuardState.Search)
            {
                if (Time.time - stateSince >= searchTime) SetState(GuardState.Return);
                else { if (FlatDistance(transform.position, lastKnownPosition) > 1.6f) MoveTo(lastKnownPosition, speed, 1.6f); else { transform.Rotate(0, 45 * Time.deltaTime, 0); Gravity(); } return; }
            }
            if (State == GuardState.Patrol || State == GuardState.Return) MovePatrol();
        }
        /// <summary>Human guards catch once per binding, only in Alert, nearby and with fresh unobstructed sight.</summary>
        public bool TryCapture()
        {
            if (!isActiveAndEnabled || isDog || captureReported || State != GuardState.Alert || Time.deltaTime <= 0 ||
                player == null || !player.gameObject.activeInHierarchy || FlatDistance(transform.position, player.position) > captureDistance ||
                Mathf.Abs(transform.position.y - player.position.y) > 1.5f || !CanSee(player)) return false;
            // UI pauses and loading must not turn into an unseen catch when controls return.
            var gate = player.GetComponent<HUSEscape.Player.GameplayInputGate>();
            if (gate == null || gate.IsBlocked || PlayerCaught == null) return false;
            captureReported = true;
            PlayerCaught.Invoke(this); return true;
        }
        private float MovementSpeed() => playerController == null ? 0 : Vector3.ProjectOnPlane(playerController.velocity, Vector3.up).magnitude;
        private bool IsCrouching() => playerAnimator != null && playerAnimator.GetBool("IsCrouching");
        /// <summary>Movement changes visibility without changing Player input or movement parameters.</summary>
        public bool CanSee(Transform target)
        {
            if (target == null) return false;
            Vector3 origin = transform.position + Vector3.up * (isDog ? .15f : .65f), end = target.position + Vector3.up * .55f;
            Vector3 delta = end - origin;
            float multiplier = target == player ? (IsCrouching() ? .55f : MovementSpeed() > 5 ? 1.25f : MovementSpeed() < .5f ? .8f : 1f) : 1;
            if (delta.magnitude > sightRange * multiplier || Vector3.Angle(transform.forward, delta) > fieldOfView * .5f) return false;
            return !Occluded(origin, end, target);
        }
        /// <summary>Prototype movement sound: stationary is silent, crouch is quieter; walls reduce range to 35%.</summary>
        public bool CanHear(Transform target, float movementSpeed, bool crouched)
        {
            if (!isDog || target == null || movementSpeed < .2f) return false;
            float radius = hearingRange * (crouched ? .22f : movementSpeed > 5 ? 1f : .55f);
            Vector3 from = transform.position + Vector3.up * .15f, to = target.position + Vector3.up * .3f;
            if (Occluded(from, to, target)) radius *= .35f;
            return Vector3.Distance(from, to) <= radius;
        }
        private bool Occluded(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 delta = to - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, sightMask, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(target)) return true;
            return false;
        }
        /// <summary>Receives only a reported position, never a live player reference from the alarm sender.</summary>
        public void ReceiveAlarm(Vector3 reportedPosition)
        {
            if (!isActiveAndEnabled || returningFromLimit || Time.time < reacquireAfter || State == GuardState.Alert ||
                !float.IsFinite(reportedPosition.x) || !float.IsFinite(reportedPosition.y) || !float.IsFinite(reportedPosition.z) ||
                FlatDistance(home, reportedPosition) > maxChaseDistance) return;
            lastKnownPosition = reportedPosition; unseenTime = 0;
            if (State != GuardState.Suspicious) SetState(GuardState.Suspicious);
        }
        private void MovePatrol()
        {
            if (waypoints == null || waypoints.Length == 0) { Gravity(); return; }
            WaypointIndex %= waypoints.Length;
            var waypoint = waypoints[WaypointIndex];
            if (waypoint == null) { WaypointIndex = (WaypointIndex + 1) % waypoints.Length; Gravity(); return; }
            if (FlatDistance(waypoint.position, transform.position) < .4f)
            {
                WaypointIndex = (WaypointIndex + 1) % waypoints.Length; CompletedWaypoints++;
                if (State == GuardState.Return) { returningFromLimit = false; reacquireAfter = Time.time + 2; SetState(GuardState.Patrol); }
                Gravity(); return;
            }
            MoveTo(waypoint.position, speed, .25f);
        }
        private void MoveTo(Vector3 position, float moveSpeed, float stopDistance)
        {
            Vector3 delta = position - transform.position; delta.y = 0;
            if (delta.magnitude < stopDistance) { Gravity(); return; }
            Vector3 direction = delta.normalized;
            if (!Clear(direction))
            {
                // Local steering only; authored routes remain open. Never tunnel through cover.
                Vector3 left = Quaternion.Euler(0, -65, 0) * direction, right = Quaternion.Euler(0, 65, 0) * direction;
                if (Clear(left)) direction = left; else if (Clear(right)) direction = right; else { Gravity(); return; }
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 130 * Time.deltaTime);
            controller.Move((direction * Mathf.Max(0, moveSpeed) + Vector3.down * 3) * Time.deltaTime);
        }
        private bool Clear(Vector3 direction)
        {
            foreach (var hit in Physics.SphereCastAll(transform.position, controller.radius * .9f, direction, .85f, sightMask, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform)) return false;
            return true;
        }
        private void Gravity() => controller.Move(Vector3.down * 3 * Time.deltaTime);
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private void SetState(GuardState value)
        {
            if (State == value) return;
            State = value; stateSince = Time.time;
            if (value == GuardState.Alert) chaseSince = Time.time;
            if (value == GuardState.Return && waypoints != null && waypoints.Length > 0)
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < waypoints.Length; i++)
                    if (waypoints[i] != null && FlatDistance(transform.position, waypoints[i].position) < nearest)
                    { nearest = FlatDistance(transform.position, waypoints[i].position); WaypointIndex = i; }
            }
            Paint();
        }
        private void Paint()
        {
            if (indicator == null || properties == null) return;
            properties.SetColor("_BaseColor", State == GuardState.Alert ? Color.red :
                State == GuardState.Suspicious || State == GuardState.Search ? Color.yellow : Color.green);
            indicator.SetPropertyBlock(properties);
        }
    }
}
