using UnityEngine;

public class CapsuleControl : MonoBehaviour
{
    public Transform Player;

    [Range(0f, 100f)]
    public float Speed = 5f;

    //[Range(0f, 360f)]
    //public float AnguleSpeed = 180f;

    private CapsuleCollider Capsule;
    private Camera mainCamera;
    private int _layerMask;

    private Vector3 _cachedWorldUp = Vector3.up;
    private Vector3 _characterTransformToCapsuleBottomHemi;
    private Vector3 _characterTransformToCapsuleTopHemi;
    private RaycastHit[] _internalCharacterHits = new RaycastHit[16];

    public const float GroundProbingBackstepDistance = 0.1f;

    private void Start()
    {
        Capsule = Player.GetComponent<CapsuleCollider>();
        mainCamera = Camera.main;
        _layerMask = LayerMask.GetMask(new string[] { "Terrain", "SceneObject" });

        _characterTransformToCapsuleBottomHemi = Capsule.center + (-_cachedWorldUp * (Capsule.height * 0.5f)) + (_cachedWorldUp * Capsule.radius);
        _characterTransformToCapsuleTopHemi = Capsule.center + (_cachedWorldUp * (Capsule.height * 0.5f)) + (-_cachedWorldUp * Capsule.radius);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        Vector3 input = DirectionInput();
        Vector3 curPos = Player.position;
        Quaternion rotation = Player.rotation;

        float stepperDis = deltaTime * Speed;
        Vector3 prospectivePos = curPos + stepperDis * input;

        int nHits = CapsuleCast(curPos, rotation, input, stepperDis, _internalCharacterHits, out RaycastHit closestHit)
    }

    private int CapsuleCast(Vector3 position, Quaternion rotation, Vector3 direction, float distance, RaycastHit[] _tmpHitsBuffer, out RaycastHit closestHit)
    {
        closestHit = default;

        int nHits = Physics.CapsuleCastNonAlloc(
                position + (rotation * _characterTransformToCapsuleBottomHemi) - (direction * GroundProbingBackstepDistance),
                position + (rotation * _characterTransformToCapsuleTopHemi) - (direction * GroundProbingBackstepDistance),
                Capsule.radius,
                direction,
                _tmpHitsBuffer,
                distance + GroundProbingBackstepDistance,
                _layerMask,
                QueryTriggerInteraction.Ignore);

        float closestDistance = Mathf.Infinity;
        for (int i = 0; i < nHits; i++)
        {
            RaycastHit hit = _tmpHitsBuffer[i];
            float hitDistance = hit.distance;
            if (hitDistance > 0f)
            {
                if (hitDistance < closestDistance)
                {
                    closestHit = hit;
                    closestHit.distance -= GroundProbingBackstepDistance;
                    closestDistance = hitDistance;
                }
            }
        }

        return nHits;
    }

    public int CharacterOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, LayerMask layers, QueryTriggerInteraction triggerInteraction, float inflate = 0f)
    {
        Vector3 bottom = position + (rotation * _characterTransformToCapsuleBottomHemi);
        Vector3 top = position + (rotation * _characterTransformToCapsuleTopHemi);
        if (inflate != 0f)
        {
            bottom += (rotation * Vector3.down * inflate);
            top += (rotation * Vector3.up * inflate);
        }

        int nbUnfilteredHits = Physics.OverlapCapsuleNonAlloc(
                    bottom,
                    top,
                    Capsule.radius + inflate,
                    overlappedColliders,
                    layers,
                    triggerInteraction);

        int nbHits = nbUnfilteredHits;
        for (int i = nbUnfilteredHits - 1; i >= 0; i--)
        {
            if (overlappedColliders[i] == Capsule)// Filter out the character capsule itself
            {
                nbHits--;
                if (i < nbHits)
                {
                    overlappedColliders[i] = overlappedColliders[nbHits];
                }
            }
        }

        return nbHits;
    }

    private Vector3 DirectionInput()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
        Vector3 dirInPlayer = mainCamera.transform.TransformDirection(input);
        dirInPlayer.y = 0;
        return dirInPlayer;
    }
}