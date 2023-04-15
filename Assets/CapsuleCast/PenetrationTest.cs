using UnityEngine;

public class PenetrationTest : MonoBehaviour
{
    public Transform CapsuleTrans;
    public Transform CloneCapsule;

    private CapsuleCollider capsule;

    private Vector3 upDir = Vector3.up;
    private int Layer;
    private Collider[] _internalProbedColliders = new Collider[16];

    private void Start()
    {
        capsule = CapsuleTrans.GetComponent<CapsuleCollider>();
        Layer = LayerMask.GetMask(new string[] { "Terrain", "SceneObject" });

        CloneCapsule.position = CapsuleTrans.position;
        CloneCapsule.rotation = CapsuleTrans.rotation;
    }

    private void Update()
    {
        Vector3 position = CapsuleTrans.position;
        int nHits = CharacterCollisionsOverlap(capsule, position, CapsuleTrans.rotation, _internalProbedColliders);
        if (nHits > 0)
        {
            for (int i = 0; i < nHits; i++)
            {
                Transform overlappedTransform = _internalProbedColliders[i].GetComponent<Transform>();

                bool hit = Physics.ComputePenetration(capsule,
                                              position,
                                              CapsuleTrans.rotation,
                                              _internalProbedColliders[i],
                                              overlappedTransform.position,
                                              overlappedTransform.rotation,
                                              out Vector3 direction,
                                              out float distance);

                if (hit)
                {
                    Debug.DrawRay(position, direction, Color.cyan, Time.deltaTime, false);

                    position += direction * distance;
                }
            }
            CloneCapsule.position = position;
            //CapsuleTrans.position = position;
        }
    }

    public int CharacterCollisionsOverlap(CapsuleCollider capsule, Vector3 position, Quaternion rotation, Collider[] overlappedColliders)
    {
        Vector3 bottom = position + rotation * (capsule.center + (-0.5f * capsule.height + capsule.radius) * upDir);
        Vector3 top = position + rotation * (capsule.center + (0.5f * capsule.height - capsule.radius) * upDir);

        int nbHits = Physics.OverlapCapsuleNonAlloc(bottom,
                                                    top,
                                                    capsule.radius,
                                                    overlappedColliders,
                                                    Layer,
                                                    QueryTriggerInteraction.Ignore);

        return nbHits;
    }
}