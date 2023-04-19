using UnityEngine;

public class CapsuleControl : MonoBehaviour
{
    public Transform Player;
    public Transform PlayerCast;

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
        Vector3 curPos = Player.position;
        Quaternion curRotation = Player.rotation;

        float stepperDis = deltaTime * Speed;
        Vector3 prospectivePos = curPos + stepperDis * input;

        //检测前进路径上的碰撞
        int nHitsForward = CapsuleCastForwards(curPos, curRotation, moveDir,
            stepperDis, out RaycastHit closestHit, _internalCharacterHits);
        if (nHitsForward > 0)
        {
            Debug.DrawLine(closestHit.point, closestHit.point + closestHit.distance * closestHit.normal, Color.red, Time.deltaTime * 60000, false);

            //碰撞点与当前点高度差
            //Vector3 verticalCharToHit = Vector3.Project(closestHit.point - curPos, _cachedWorldUp);
            //抬高到与当前碰撞点齐平，再加检测距离
            //Vector3 checkStepPos = closestHit.point - verticalCharToHit + (_cachedWorldUp * MaxStepHeight);
            Vector3 checkStepPos = prospectivePos + _cachedWorldUp * MaxStepHeight;
            Debug.DrawRay(checkStepPos, -_cachedWorldUp, Color.blue, Time.deltaTime * 60000, false);

            int nHitsDownward = CapsuleCastDownwards(checkStepPos, curRotation, -_cachedWorldUp,
                MaxStepHeight, out RaycastHit stepHit, _internalCharacterHits);

            Vector3 checkOverlapPos = prospectivePos;
            if (nHitsDownward > 0 && stepHit.distance > 0f)
            {
                Debug.DrawLine(stepHit.point, stepHit.point + stepHit.distance * stepHit.normal, Color.cyan, Time.deltaTime * 60000, false);
                checkOverlapPos = checkStepPos - stepHit.distance * _cachedWorldUp;
                Debug.Log($"{nHitsDownward} 拉高距离:{checkOverlapPos.y - prospectivePos.y}");
            }
            Vector3 correctiveOverlapPos = checkOverlapPos;
            int nbOverlaps = CharacterOverlap(checkOverlapPos, curRotation, _internalProbedColliders, _layerMask);
            if (nbOverlaps > 0)
            {
                Debug.Log($"重叠数量 {nbOverlaps}");
                Vector3 resolutionDirection = _cachedWorldUp;
                float resolutionDistance = 0f;
                for (int i = 0; i < nbOverlaps; i++)
                {
                    //胶囊体中心在地面以下时检测会失效
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
                        Debug.Log($"穿透距离 {resolutionDistance}");

                        bool isStable = IsStableOnNormal(resolutionDirection);
                        //resolutionDirection = GetObstructionNormal(resolutionDirection, isStable);

                        Vector3 resolutionMovement = resolutionDirection * (resolutionDistance + CollisionOffset);
                        correctiveOverlapPos += resolutionMovement;

                        break;
                    }
                }
            }
            Player.position = correctiveOverlapPos;

            //台阶检测
            //ProbeGround(closestHit.collider, closestHit.normal, closestHit.point, prospectivePos, curRotation);
        }
        else
        {
            //检测不到碰撞
            Player.position = prospectivePos;
        }
    }

    private bool ProbeGround(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 position, Quaternion rotation)
    {
        Vector3 checkDir = rotation * -_cachedWorldUp;
        Vector3 checkPos = position - MaxStepHeight * checkDir;
        RaycastHit closestHit;
        int nHitsDownward = CapsuleCastDownwards(checkPos, rotation, checkDir, MaxStepHeight, out closestHit, _internalCharacterHits);

        return true;
    }

    private int CapsuleCastForwards(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] _tmpHitsBuffer)
    {
        closestHit = new RaycastHit();

        //拉高一点检测，平地时不受地面影响
        int nHits = Physics.CapsuleCastNonAlloc(
                position + (rotation * _characterTransformToCapsuleBottomHemi) + (_cachedWorldUp * SecondaryProbesVertical),
                position + (rotation * _characterTransformToCapsuleTopHemi) + (_cachedWorldUp * SecondaryProbesVertical),
                Capsule.radius,
                direction,
                _tmpHitsBuffer,
                distance,
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