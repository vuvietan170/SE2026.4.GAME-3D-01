using System.Linq;
using HUSEscape.World;
using UnityEngine;

/// <summary>Disposable late-frame camera/near-plane audit of the physical exit route.</summary>
[DefaultExecutionOrder(10000)]
public sealed class ExitRouteProbe : MonoBehaviour
{
    public bool Monitor;
    public int Samples { get; private set; }
    public string Failure { get; private set; }
    private readonly Vector3[] corners=new Vector3[4];
    private void LateUpdate()
    {
        if(!Monitor||Time.deltaTime<=0)return;
        var entry=FindObjectsByType<JourneySceneEntry>().SingleOrDefault();
        if(entry==null||entry.isCampus||entry.player.transform.position.z<59||entry.Gate.IsBlocked||!entry.viewCamera.GetComponent<HUSEscape.Player.ThirdPersonCameraFollow>().enabled)return;
        var camera=entry.viewCamera;Vector3 p=camera.transform.position;Samples++;
        if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z))Failure=Failure??"Non-finite camera pose";
        foreach(var hit in Physics.OverlapSphere(p,.18f,~0,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(entry.player.transform)&&!hit.transform.IsChildOf(camera.transform))Failure=Failure??"Camera body overlaps "+hit.name+" at "+p;
        camera.CalculateFrustumCorners(new Rect(0,0,1,1),camera.nearClipPlane,Camera.MonoOrStereoscopicEye.Mono,corners);
        foreach(var corner in corners)foreach(var hit in Physics.OverlapSphere(camera.transform.TransformPoint(corner),.003f,~0,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(entry.player.transform)&&!hit.transform.IsChildOf(camera.transform))Failure=Failure??"Near plane overlaps "+hit.name;
    }
}
