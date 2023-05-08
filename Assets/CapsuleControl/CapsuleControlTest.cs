using UnityEngine;

public class CapsuleControlTest : MonoBehaviour
{
    public Transform Player;

    [Range(0f, 100f)]
    public float Speed = 5f;

    public float MaxStableSlopeAngle = 60f;

    public float MaxStepHeight = 0.5f;

    public LayerMask WalkLayerMask;

    private CapsuleCollider Capsule;
    private Camera mainCamera;
    private int _layerMask;

    private Vector3 _cachedWorldUp = Vector3.up;
    private Vector3 _characterTransformToCapsuleBottomHemi;
    private Vector3 _characterTransformToCapsuleTopHemi;
    private readonly RaycastHit[] _internalCharacterHits = new RaycastHit[16];
    private readonly Collider[] _internalProbedColliders = new Collider[16];

    private const float G = 9.8f;
    private const float GroundProbingBackstepDistance = 0.1f;
    private const float SecondaryProbesVertical = 0.02f;
    private const float SecondaryProbesHorizontal = 0.001f;
    private const float CollisionOffset = 0.01f;

    private bool falling = false;
    private Vector3 fallingSpeed = Vector3.zero;

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
        if (!falling && input.sqrMagnitude <= 0.01f)
            return;
        Vector3 curPos = Player.position;
        Quaternion curRotation = Player.rotation;
        Vector3 atCharacterUp = curRotation * _cachedWorldUp;
        Vector3 moveDir = Vector3.ProjectOnPlane(input, atCharacterUp).normalized;
        float stepperDis = deltaTime * Speed;
        Vector3 prospectivePos = curPos + stepperDis * moveDir;

        //检测前进方向上的碰撞
        int nHitsForward = CapsuleCast(curPos, curRotation, moveDir, stepperDis, out RaycastHit closestHit, _internalCharacterHits);
        bool bHitForward = nHitsForward > 0 && closestHit.distance > 0f;
        if (bHitForward)
        {
            //Debug.DrawLine(closestHit.point, closestHit.point + closestHit.distance * closestHit.normal, Color.red, Time.deltaTime * 60000, false);
            //Debug.Log($"前进方向碰撞:{closestHit.transform.name}");

            #region 检测落差

            bool foundInnerNormal = false;
            bool foundOuterNormal = false;
            bool isStableLedgeInnerNormal = false;
            bool isStableLedgeOuterNormal = false;
            Vector3 checkPos = closestHit.point;
            if (CharacterCollisionsRaycast(
                        checkPos + (atCharacterUp * SecondaryProbesVertical) + (moveDir * SecondaryProbesHorizontal),
                        -atCharacterUp,
                        MaxStepHeight + SecondaryProbesVertical,
                        out RaycastHit innerLedgeHit,
                        _internalCharacterHits) > 0)
            {
                foundInnerNormal = true;
                isStableLedgeInnerNormal = IsStableOnNormal(innerLedgeHit.normal);
            }

            if (CharacterCollisionsRaycast(
                        checkPos + (atCharacterUp * SecondaryProbesVertical) + (-moveDir * SecondaryProbesHorizontal),
                        -atCharacterUp,
                        MaxStepHeight + SecondaryProbesVertical,
                        out RaycastHit outerLedgeHit,
                        _internalCharacterHits) > 0)
            {
                foundOuterNormal = true;
                isStableLedgeOuterNormal = IsStableOnNormal(outerLedgeHit.normal);
            }
            //Debug.Log($"{Time.frameCount} 检测上台阶 {FoundInnerNormal}-{FoundOuterNormal} {isStableLedgeInnerNormal}-{isStableLedgeOuterNormal}");
            if (foundInnerNormal && foundOuterNormal)
            {
                if (!isStableLedgeOuterNormal)
                {
                    //爬陡坡
                    return;
                }
            }
            if (foundOuterNormal)
            {
                Debug.Log($"{Time.frameCount} outer");
                prospectivePos = curPos + Vector3.ProjectOnPlane(moveDir, outerLedgeHit.normal).normalized * stepperDis;
            }

            #endregion 检测落差
        }

        Vector3 checkStepPos = prospectivePos + _cachedWorldUp * MaxStepHeight;
        float checkStepDis = MaxStepHeight * 2f;
        int nHitsDownward = CapsuleCast(checkStepPos, curRotation, -_cachedWorldUp, checkStepDis, out RaycastHit stepHit, _internalCharacterHits);

        if (nHitsDownward > 0)
        {
            if (!falling)
            {
                //Debug.DrawLine(stepHit.point, stepHit.point + stepHit.distance * stepHit.normal, Color.cyan, Time.deltaTime * 60000, false);
                Vector3 checkOverlapPos = checkStepPos - stepHit.distance * _cachedWorldUp;
                Vector3 correctiveOverlapPos = CharacterOverlapPenetration(checkOverlapPos, curRotation, _internalProbedColliders, _layerMask);
                Player.position = correctiveOverlapPos;
            }
            else
            {
                if (stepHit.distance < GroundProbingBackstepDistance)
                {
                    falling = false;
                }
            }
        }
        else
        {
            if (!bHitForward)
            {
                //悬空
                if (!falling)
                {
                    falling = true;
                    fallingSpeed = Speed * moveDir;
                }
            }
            else
            {
                if (falling)
                {
                    fallingSpeed = Vector3.Project(fallingSpeed, _cachedWorldUp);
                }
            }
        }

        if (falling)
        {
            fallingSpeed += deltaTime * G * -_cachedWorldUp;
            Player.position += deltaTime * fallingSpeed;
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
            if (nHits > 1)
            {
                Debug.Log($"[{Time.frameCount}] 碰撞 {i}-{nHits} {hit.transform.name} distance:{hit.distance}");
                DrawHitLine(hit, Color.yellow);
            }

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
        int nbOverlaps = CharacterOverlap(position, rotation, overlappedColliders, layers);
        if (nbOverlaps > 0)
        {
            //Debug.Log($"重叠数量 {nbOverlaps}");
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
                    //Debug.Log($"穿透距离 {resolutionDistance}");

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

    public int CharacterCollisionsRaycast(Vector3 position, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits)
    {
        int nbUnfilteredHits = Physics.RaycastNonAlloc(
            position,
            direction,
            hits,
            distance,
            _layerMask,
            QueryTriggerInteraction.Ignore);

        closestHit = new RaycastHit();
        float closestDistance = Mathf.Infinity;
        int nbHits = nbUnfilteredHits;
        for (int i = nbUnfilteredHits - 1; i >= 0; i--)
        {
            RaycastHit hit = hits[i];
            float hitDistance = hit.distance;

            if (hitDistance <= 0f || !hit.collider == Capsule)
            {
                nbHits--;
                if (i < nbHits)
                {
                    hits[i] = hits[nbHits];
                }
            }
            else
            {
                if (hitDistance < closestDistance)
                {
                    closestHit = hit;
                    closestDistance = hitDistance;
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

    private void DrawHitLine(RaycastHit hit, Color color)
    {
        Debug.DrawLine(hit.point, hit.point + hit.distance * hit.normal, color, 10f);
    }
}