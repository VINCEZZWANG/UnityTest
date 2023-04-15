using UnityEngine;

public class CapsuleCast : MonoBehaviour
{
    public Transform Player;
    public Transform PlayerCast;
    public float Distance;

    private CapsuleCollider capsule;
    private Vector3 CastDir = Vector3.forward;
    private Vector3 upDir = Vector3.up;
    private RaycastHit[] _internalProbedHits = new RaycastHit[16];
    private int Layer;

    private void Start()
    {
        capsule = Player.GetComponent<CapsuleCollider>();
        Layer = LayerMask.GetMask(new string[] { "Terrain", "SceneObject" });
        if (Distance <= 0)
            Distance = capsule.radius;
    }

    private void OnDrawGizmos()
    {
    }

    private void Update()
    {
        CastDir = Player.forward;
        Vector3 bottom = Player.position + capsule.center + (-0.5f * capsule.height + capsule.radius) * upDir;
        Vector3 top = Player.position + capsule.center + (0.5f * capsule.height - capsule.radius) * upDir;

        PlayerCast.position = Player.position + CastDir * Distance;

        int nHits = Physics.CapsuleCastNonAlloc(bottom, top, capsule.radius, CastDir,
            _internalProbedHits, Distance, Layer);
        if (nHits > 0)
        {
            for (int i = 0; i < nHits; i++)
            {
                var hit = _internalProbedHits[i];

                Debug.DrawRay(hit.point, hit.normal, Color.cyan, Time.deltaTime, false);

                Debug.Log($"[{Time.frameCount}] [{nHits}] [{hit.transform.name}] {Player.position} {hit.point} {hit.distance}");
            }
        }

        //Debug.DrawLine(top, top + Vector3.right * 2f, Color.red, Time.deltaTime, false);
        //Debug.DrawLine(bottom, bottom + Vector3.right * 2f, Color.blue, Time.deltaTime, false);

        //Debug.DrawLine(top + CastDir * Distance, top + CastDir * Distance + Vector3.forward * 2f, Color.red, Time.deltaTime, false);
        //Debug.DrawLine(bottom + CastDir * Distance, bottom + CastDir * Distance + Vector3.forward * 2f, Color.blue, Time.deltaTime, false);
    }
}