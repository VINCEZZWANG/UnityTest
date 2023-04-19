using UnityEngine;

public class CapsuleControl : MonoBehaviour
{
    public Transform Player;

    [Range(0f, 100f)]
    public float Speed = 5f;

    public float MaxStableSlopeAngle = 60f;

    public float MaxStepHeight = 0.5f;

    public LayerMask WalkLayerMask;

    //[Range(0f, 360f)]
    //public float AnguleSpeed = 180f;

    private CapsuleCollider Capsule;
    private Camera mainCamera;
    private int _layerMask;

    private Vector3 _cachedWorldUp = Vector3.up;
    private Vector3 _characterTransformToCapsuleBottomHemi;
    private Vector3 _characterTransformToCapsuleTopHemi;
    private readonly RaycastHit[] _internalCharacterHits = new RaycastHit[16];
    private readonly Collider[] _internalProbedColliders = new Collider[16];

    public const float GroundProbingBackstepDistance = 0.1f;
    public const float SecondaryProbesVertical = 0.02f;
    public const float SecondaryProbesHorizontal = 0.001f;

    public const float CollisionOffset = 0.01f;

    private void Start()
    {
        Capsule = Player.GetComponent<CapsuleCollider>();
        mainCamera = Camera.main;
        _layerMask = WalkLayerMask.value;

        _characterTransformToCapsuleBottomHemi = Capsule.center + (-_cachedWorldUp * (Capsule.height * 0.5f)) + (_cachedWorldUp * Capsule.radius);
        _characterTransformToCapsuleTopHemi = Capsule.center + (_cachedWorldUp * (Capsule.height * 0.5f)) + (-_cachedWorldUp * Capsule.radius);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        Vector3 input = DirectionInput();
        Vector3 moveDir = input.normalized;
        if (input.sqrMagnitude <= 0.01f)
            return;
        Vector3 curPos = Player.position;
        Quaternion curRotation = Player.rotation;

        float stepperDis = deltaTime * Speed;
        Vector3 prospectivePos = curPos + stepperDis * input;

        //检测前进方向上的碰撞
        int nHitsForward = CapsuleCast(curPos, curRotation, moveDir, stepperDis, out RaycastHit closestHit, _internalCharacterHits);
        bool bHitForward = nHitsForward > 0 && closestHit.distance > 0f;
        Vector3 checkStepPos;
        if (bHitForward)
        {
            Debug.DrawLine(closestHit.point, closestHit.point + closestHit.distance * closestHit.normal, Color.red, Time.deltaTime * 60000, false);
            checkStepPos = prospectivePos + _cachedWorldUp * MaxStepHeight;
        }
        else
        {
            //前进方向检测不到碰撞
            checkStepPos = prospectivePos;
        }
        int nHitsDownward = CapsuleCast(checkStepPos, curRotation, -_cachedWorldUp, MaxStepHeight, out RaycastHit stepHit, _internalCharacterHits);

        Vector3 checkOverlapPos = prospectivePos;
        if (nHitsDownward > 0)
        {
            Debug.DrawLine(stepHit.point, stepHit.point + stepHit.distance * stepHit.normal, Color.cyan, Time.deltaTime * 60000, false);
            checkOverlapPos = checkStepPos - stepHit.distance * _cachedWorldUp;
            Vector3 correctiveOverlapPos = CharacterOverlapPenetration(checkOverlapPos, curRotation, _internalProbedColliders, _layerMask);
            Player.position = correctiveOverlapPos;
        }
        else
        {
            if (!bHitForward)
            {
                //TODO 悬空
            }
        }
    }

    private int CapsuleCast(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] _tmpHitsBuffer)
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

        int validHits = nHits;
        float closestDistance = Mathf.Infinity;
        for (int i = nHits - 1; i >= 0; i--)
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
            else
            {
                validHits--;
                if (i < validHits)
                {
                    _tmpHitsBuffer[i] = _tmpHitsBuffer[validHits];
                }
            }
        }

        return validHits;
    }

    public Vector3 CharacterOverlapPenetration(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, LayerMask layers)
    {
        Vector3 correctiveOverlapPos = position;
        int nbOverlaps = CharacterOverlap(position, rotation, overlappedColliders, _layerMask);
        if (nbOverlaps > 0)
        {
            Debug.Log($"重叠数量 {nbOverlaps}");
            for (int i = 0; i < nbOverlaps; i++)
            {
                //胶囊体中心在地面以下时检测会失效
                Transform overlappedTransform = overlappedColliders[i].GetComponent<Transform>();
                if (Physics.ComputePenetration(
                        Capsule,
                        position,
                        rotation,
                        overlappedColliders[i],
                        overlappedTransform.position,
                        overlappedTransform.rotation,
                        out Vector3 resolutionDirection,
                        out float resolutionDistance))
                {
                    Debug.Log($"穿透距离 {resolutionDistance}");

                    bool isStable = IsStableOnNormal(resolutionDirection);
                    //resolutionDirection = GetObstructionNormal(resolutionDirection, isStable);

                    Vector3 resolutionMovement = resolutionDirection * (resolutionDistance + CollisionOffset);
                    correctiveOverlapPos += resolutionMovement;
                }
            }
        }
        return correctiveOverlapPos;
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