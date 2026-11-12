using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>Optional, scene-scoped right-hand contact for one door; leaves the original FK pose at zero weight.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class DoorHandleIK : MonoBehaviour
{
    [Header("Explicit scene references")]
    [SerializeField] private Animator animator;
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private TwoBoneIKConstraint rightHand;
    [SerializeField] private Transform handleTarget;

    [Header("Hand timing (seconds)")]
    [Tooltip("Blend to the handle after the existing Open animation trigger.")]
    [SerializeField, Min(.01f)] private float approachTime = .25f;
    [Tooltip("Follow the initial door swing before releasing. Reach safety may release earlier.")]
    [SerializeField, Min(0f)] private float holdTime = .06f;
    [SerializeField, Min(.01f)] private float releaseTime = .18f;
    [SerializeField, Range(0f,1f)] private float maximumWeight = 1f;
    [SerializeField, Min(.001f)] private float contactTolerance = .06f;
    [SerializeField, Min(0f)] private float contactGraceTime = .2f;

    [Header("Small stance adjustment; collision checked by existing CharacterController")]
    [SerializeField, Min(.01f)] private float alignmentTime = .35f;
    [SerializeField, Min(0f)] private float maximumAlignmentDistance = 1.5f;
    [SerializeField, Min(.05f)] private float shoulderToGripDistance = .2f;
    [SerializeField, Range(.5f,.99f)] private float reachLimit = .95f;

    private enum Phase { Idle, Reach, Follow, Release }
    private Phase phase;
    private Transform actor;
    private Vector3 hintLocal;
    private float armLength;
    private float phaseTime;
    private float releaseStart;
    private bool cullingOverridden;
    private bool ownsArm;
    private AnimatorCullingMode previousCulling;

    /// <summary>Gets whether the optional stance was reached without blocked movement.</summary>
    public bool IsPrepared { get; private set; }
    /// <summary>Gets the current constraint weight, for inspection during Play Mode.</summary>
    public float CurrentWeight => rightHand != null ? rightHand.weight : 0f;
    /// <summary>Gets wrist distance to the configured grip point; this is not a finger collision test.</summary>
    public float ContactError => rightHand != null && rightHand.data.tip != null && handleTarget != null
        ? Vector3.Distance(rightHand.data.tip.position, handleTarget.position) : float.PositiveInfinity;

    /// <summary>Validates the actual actor and Humanoid chain before enabling optional IK.</summary>
    public bool CanUse(Transform candidate, Animator candidateAnimator)
    {
        return isActiveAndEnabled && candidate != null && animator != null && candidateAnimator == animator
            && animator.isActiveAndEnabled && animator.avatar != null && animator.avatar.isValid && animator.isHuman
            && rigBuilder != null && rigBuilder.isActiveAndEnabled && rightHand != null && handleTarget != null
            && rightHand.data.root != null && rightHand.data.mid != null && rightHand.data.tip != null
            && rightHand.data.target != null && rightHand.data.hint != null
            && rightHand.data.tip.IsChildOf(rightHand.data.mid) && rightHand.data.mid.IsChildOf(rightHand.data.root);
    }

    /// <summary>Aligns only the accepted door interaction, reserving the shared arm without global state.</summary>
    public IEnumerator AlignForOpening(Transform candidate, Animator candidateAnimator)
    {
        ResetContact();
        if (!CanUse(candidate, candidateAnimator)) { yield break; }
        // Ownership is instance-scoped. Idle/disabled doors must never reset another door's constraint.
        foreach (var other in FindObjectsByType<DoorHandleIK>())
        {
            if (other != this && other.rightHand == rightHand && other.ownsArm) { yield break; }
        }
        ownsArm = true;
        actor = candidate;
        previousCulling = animator.cullingMode;
        cullingOverridden = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Use evaluated FK bones, including when the actor was offscreen before E.
        yield return null;
        if (!CanUse(actor, animator)) { ResetContact(); yield break; }
        var data = rightHand.data;
        armLength = Vector3.Distance(data.root.position, data.mid.position) + Vector3.Distance(data.mid.position, data.tip.position);
        if (armLength < .05f) { ResetContact(); yield break; }
        var controller = actor.GetComponent<CharacterController>();
        if (controller == null || !controller.enabled) { ResetContact(); yield break; }
        Vector3 facing = -transform.forward; facing.y = 0;
        Quaternion rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        Vector3 shoulderOffset = Quaternion.Inverse(actor.rotation) * (data.root.position - actor.position);
        Vector3 shoulder = handleTarget.position - rotation * Vector3.forward * Mathf.Min(shoulderToGripDistance, armLength * .65f);
        Vector3 goal = shoulder - rotation * shoulderOffset; goal.y = actor.position.y;
        Vector3 start = actor.position; Quaternion startRotation = actor.rotation;
        Vector3 delta = goal - start; delta.y = 0;
        if (delta.magnitude > maximumAlignmentDistance) { ResetContact(); yield break; }
        float elapsed = 0;
        while (elapsed < alignmentTime)
        {
            if (!CanUse(actor, animator)) { ResetContact(); yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, alignmentTime)));
            Vector3 desired = Vector3.Lerp(start, goal, t); Vector3 move = desired - actor.position; move.y = 0;
            controller.Move(move); actor.rotation = Quaternion.Slerp(startRotation, rotation, t);
            yield return null;
        }
        delta = goal - actor.position; delta.y = 0;
        if (delta.magnitude > .12f) { ResetContact(); yield break; }
        actor.rotation = rotation;
        hintLocal = actor.InverseTransformPoint(data.mid.position + actor.right * .12f - Vector3.up * .05f);
        IsPrepared = true;
    }

    /// <summary>Begins the smooth approach alongside the unchanged Open animation.</summary>
    public void BeginReach()
    {
        if (!IsPrepared || !CanUse(actor, animator)) { ResetContact(); return; }
        phase = Phase.Reach; phaseTime = 0; rightHand.weight = 0;
    }

    /// <summary>Allows a short contact grace period; unreachable hands fall back without blocking the door.</summary>
    public IEnumerator WaitForContact()
    {
        float deadline = Time.time + Mathf.Max(0f, approachTime - phaseTime) + contactGraceTime;
        while (phase == Phase.Reach && (phaseTime < approachTime || ContactError > contactTolerance) && Time.time < deadline) { yield return null; }
        if (ContactError > contactTolerance) { BeginRelease(); }
    }

    /// <summary>Follows only the early door swing before fading, with an independent reach guard.</summary>
    public void BeginFollowing()
    {
        if (phase != Phase.Reach || ContactError > contactTolerance) { BeginRelease(); return; }
        phase = Phase.Follow; phaseTime = 0;
    }

    private void Update()
    {
        if (phase == Phase.Idle) { return; }
        if (!CanUse(actor, animator)) { ResetContact(); return; }
        var data = rightHand.data;
        Vector3 difference = handleTarget.position - data.root.position;
        if (phase == Phase.Reach && difference.magnitude > armLength * 1.5f) { BeginRelease(); }
        float maxReach = armLength * reachLimit;
        // Clamp the proxy, never scale or translate the source arm bones to extend their length.
        data.target.position = data.root.position + Vector3.ClampMagnitude(difference, maxReach);
        data.target.rotation = handleTarget.rotation;
        data.hint.position = actor.TransformPoint(hintLocal);
        if (phase == Phase.Follow && difference.magnitude > maxReach) { BeginRelease(); }
        phaseTime += Time.deltaTime;
        if (phase == Phase.Reach) { rightHand.weight = maximumWeight * Mathf.SmoothStep(0, 1, Mathf.Clamp01(phaseTime / Mathf.Max(.01f, approachTime))); }
        else if (phase == Phase.Follow)
        {
            if (phaseTime >= holdTime) { BeginRelease(); }
        }
        else if (phase == Phase.Release)
        {
            rightHand.weight = releaseStart * (1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(phaseTime / Mathf.Max(.01f, releaseTime))));
            if (phaseTime >= releaseTime) { ResetContact(); }
        }
        Sync();
    }

    private void BeginRelease()
    {
        if (phase == Phase.Idle || phase == Phase.Release) { return; }
        releaseStart = CurrentWeight; phase = Phase.Release; phaseTime = 0;
    }
    private void Sync() { if (rigBuilder != null && rigBuilder.graph.IsValid()) { rigBuilder.SyncLayers(); } }
    private void ResetContact()
    {
        phase = Phase.Idle; IsPrepared = false; phaseTime = 0;
        if (ownsArm && rightHand != null) { rightHand.weight = 0; Sync(); }
        if (cullingOverridden && animator != null) { animator.cullingMode = previousCulling; }
        cullingOverridden = false;
        ownsArm = false;
    }
    private void OnEnable() => ResetContact();
    private void OnDisable() => ResetContact();
    private void OnDestroy() => ResetContact();
}
