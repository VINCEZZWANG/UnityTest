using UnityEngine;

public class CapsuleControl : MonoBehaviour
{
    public Transform Player;
    public Transform PlayerCast;

    [Range(0f, 100f)]
    public float Speed = 5f;

    public float MaxStableSlopeAngle = 60f;

    public float MaxStepHeight = 0.5f;

    //[Range(0f, 360f)]
    //public float AnguleSpeed = 180f;

    private CapsuleCollider Capsule;
    private Camera mainCamera;
    private int _layerMask;

    private Vector3 _cachedWorldUp = Vector3.up;
    private Vector3 _characterTransformToCapsuleBottomHemi;
    private Vector3 _characterTransformToCapsuleTopHemi;
    private RaycastHit[] _internalCharacterHits = new RaycastHit[16];
    private Collider[] _internalProbedColliders = new Collider[16];

    public const float GroundProbingBackstepDistance = 0.1f;
    public const float SecondaryProbesVertical = 0.02f;
    public const float SecondaryProbesHorizontal = 0.001f;
    public const float CollisionOffset = 0.01f;

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
        Quaternion curRotation = Player.rotation;

        float stepperDis = deltaTime * Speed;
        Vector3 prospectivePos = curPos + stepperDis * input;

        //��⵽ǰ��·���ϵ���ײ
        int nHitsForward = CapsuleCastForwards(curPos, curRotation, input.normalized, stepperDis, out RaycastHit closestHit, _internalCharacterHits);
        Debug.DrawRay(closestHit.point, closestHit.normal, Color.cyan, Time.deltaTime, false);
        Debug.Log($"[{Time.frameCount}] [{nHitsForward}] [{closestHit.transform.name}] {Player.position} {closestHit.point} {closestHit.distance}");

        //�ص�����
        int nbOverlaps = CharacterOverlap(prospectivePos, curRotation, _internalProbedColliders, _layerMask);
        if (nbOverlaps > 0)
        {
            Vector3 resolutionDirection = _cachedWorldUp;
            float resolutionDistance = 0f;
            for (int i = 0; i < nbOverlaps; i++)
            {
                //�����������ڵ�������ʱ����ʧЧ
                Transform overlappedTransform = _internalProbedColliders[i].GetComponent<Transform>();
                if (Physics.ComputePenetration(
                        Capsule,
                        prospectivePos,
                        curRotation,
                        _internalProbedColliders[i],
                        overlappedTransform.position,
                        overlappedTransform.rotation,
                        out resolutionDirection,
                        out resolutionDistance))
                {
                    bool isStable = IsStableOnNormal(resolutionDirection);
                    //resolutionDirection = GetObstructionNormal(resolutionDirection, isStable);

                    // Solve overlap
                    Vector3 resolutionMovement = resolutionDirection * (resolutionDistance + CollisionOffset);
                    curPos += resolutionMovement;

                    break;
                }
            }
        }

        //̨�׼��
        Vector3 groundSweepDirection = curRotation * -_cachedWorldUp;
        int nHitsDownward = CapsuleCastDownwards(prospectivePos, curRotation, groundSweepDirection, MaxStepHeight, out closestHit, _internalCharacterHits);

        if (nHitsForward == 0)
        {
            Player.position = prospectivePos;
        }
        else
        {
            Player.position = curPos;
        }
    }

    private int CapsuleCastForwards(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] _tmpHitsBuffer)
    {
        closestHit = new RaycastHit();

        //����һ���⣬ƽ��ʱ���ܵ���Ӱ��
        int nHits = Physics.CapsuleCastNonAlloc(
                position + (rotation * _characterTransformToCapsuleBottomHemi) + (_cachedWorldUp * SecondaryProbesVertical),
                position + (rotation * _characterTransformToCapsuleTopHemi) + (_cachedWorldUp * SecondaryProbesVertical),
                Capsule.radius,
                direction,
                _tmpHitsBuffer,
                distance,
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

    private int CapsuleCastDownwards(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] _tmpHitsBuffer)
    {
        closestHit = new RaycastHit();

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

    public int CharacterOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, LayerMask layers, float inflate = 0f)
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
                    layers);

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

    private bool IsStableOnNormal(Vector3 normal)
    {
        return Vector3.Angle(Vector3.up, normal) <= MaxStableSlopeAngle;
    }

    private Vector3 DirectionInput()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
        Vector3 dirInPlayer = mainCamera.transform.TransformDirection(input);
        dirInPlayer.y = 0;
        return dirInPlayer;
    }
}