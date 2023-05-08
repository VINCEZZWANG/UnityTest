using UnityEngine;

public class CapsuleController
{
    private Transform _player;
    private CapsuleCollider _capsule;
    private int _layerMask;

    private Vector3 _cachedWorldUp = Vector3.up;
    private Vector3 _characterTransformToCapsuleBottomHemi;
    private Vector3 _characterTransformToCapsuleTopHemi;
    private readonly RaycastHit[] _internalCharacterHits = new RaycastHit[16];
    private readonly Collider[] _internalProbedColliders = new Collider[16];

    private float _maxStableSlopeAngle = 60f;
    private float _maxStepHeight = 0.5f;
    private const float G = 9.8f;
    private const float GroundProbingBackstepDistance = 0.1f;
    private const float SecondaryProbesVertical = 0.02f;
    private const float SecondaryProbesHorizontal = 0.001f;
    private const float CollisionOffset = 0.01f;

    private bool falling = false;
    private Vector3 fallingSpeed = Vector3.zero;

    public void Init(Transform player, int layerMask, float maxStableSlopeAngle, float maxStepHeight)
    {
        _player = player;
        _capsule = player.GetComponent<CapsuleCollider>();
        _layerMask = layerMask;
        _maxStableSlopeAngle = maxStableSlopeAngle;
        _maxStepHeight = maxStepHeight;
        _capsule.radius = _maxStepHeight + 0.01f;
        _capsule.center = _capsule.height * 0.5f * _cachedWorldUp;

        _characterTransformToCapsuleBottomHemi = _capsule.center + (-_cachedWorldUp * (_capsule.height * 0.5f)) + (_cachedWorldUp * _capsule.radius);
        _characterTransformToCapsuleTopHemi = _capsule.center + (_cachedWorldUp * (_capsule.height * 0.5f)) + (-_cachedWorldUp * _capsule.radius);
    }

    public bool SimpleMove(Vector3 velocity, float deltaTime)
    {
        Vector3 curPos = _player.position;
        Quaternion curRotation = _player.rotation;
        Vector3 atCharacterUp = curRotation * _cachedWorldUp;
        //限制只能朝水平方向前进
        Vector3 moveDir = Vector3.ProjectOnPlane(velocity, atCharacterUp).normalized;
        float stepperDis = deltaTime * velocity.magnitude;
        Vector3 prospectivePos = curPos + stepperDis * moveDir;

        //步进距离
        //Debug.DrawLine(curPos, prospectivePos, Color.blue, 10f);

        //前进方向检测，检测点
        int nHitsForward = CapsuleCastForwardOrUp(curPos, curRotation, moveDir, stepperDis,
            out RaycastHit closestHit, out RaycastHit highestHit, _internalCharacterHits);

        bool bHitForward = nHitsForward > 0;
        if (bHitForward)
        {
            //DrawHitLine(highestHit, Color.yellow);
        }

        #region 检测落差

        bool foundInnerNormal = false;
        bool foundOuterNormal = false;
        bool isStableLedgeInnerNormal = false;
        bool isStableLedgeOuterNormal = false;
        Vector3 checkPos = closestHit.point;
        if (CharacterCollisionsRaycast(
                    checkPos + (atCharacterUp * SecondaryProbesVertical) + (moveDir * SecondaryProbesHorizontal),
                    -atCharacterUp,
                    _maxStepHeight + SecondaryProbesVertical,
                    out RaycastHit innerLedgeHit,
                    _internalCharacterHits) > 0)
        {
            foundInnerNormal = true;
            isStableLedgeInnerNormal = IsStableOnNormal(innerLedgeHit.normal);
            DrawHitLine(innerLedgeHit, Color.red);
        }

        if (CharacterCollisionsRaycast(
                    checkPos + (atCharacterUp * SecondaryProbesVertical) + (-moveDir * SecondaryProbesHorizontal),
                    -atCharacterUp,
                    _maxStepHeight + SecondaryProbesVertical,
                    out RaycastHit outerLedgeHit,
                    _internalCharacterHits) > 0)
        {
            foundOuterNormal = true;
            isStableLedgeOuterNormal = IsStableOnNormal(outerLedgeHit.normal);
            DrawHitLine(outerLedgeHit, Color.green);
        }

        //Debug.Log($"{Time.frameCount} 检测上台阶 {FoundInnerNormal}-{FoundOuterNormal} {isStableLedgeInnerNormal}-{isStableLedgeOuterNormal}");
        if (foundInnerNormal && foundOuterNormal)
        {
            if (!isStableLedgeOuterNormal)
            {
                //爬陡坡
                return false;
            }
        }
        if (foundOuterNormal)
        {
            Debug.Log($"{Time.frameCount} outer");
            prospectivePos = curPos + Vector3.ProjectOnPlane(moveDir, outerLedgeHit.normal).normalized * stepperDis;
        }

        #endregion 检测落差

        Vector3 checkStepPos = prospectivePos + _cachedWorldUp * _maxStepHeight;
        float checkStepDis = _maxStepHeight * 2f;
        int nHitsDownward = CapsuleCast(checkStepPos, curRotation, -_cachedWorldUp, checkStepDis, out RaycastHit stepHit, _internalCharacterHits);

        if (nHitsDownward > 0)
        {
            if (!falling)
            {
                //Debug.DrawLine(stepHit.point, stepHit.point + stepHit.distance * stepHit.normal, Color.cyan, Time.deltaTime * 60000, false);
                Vector3 checkOverlapPos = checkStepPos - stepHit.distance * _cachedWorldUp;
                Vector3 correctiveOverlapPos = CharacterOverlapPenetration(checkOverlapPos, curRotation, _internalProbedColliders, _layerMask);
                _player.position = correctiveOverlapPos;
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
                    fallingSpeed = velocity;
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
            _player.position += deltaTime * fallingSpeed;
        }
        return true;
    }

    public bool Move(Vector3 velocity, float deltaTime)
    {
        if (!falling && velocity.sqrMagnitude <= 0.01f)
            return false;
        Vector3 curPos = _player.position;
        Quaternion curRotation = _player.rotation;
        Vector3 atCharacterUp = curRotation * _cachedWorldUp;
        Vector3 moveDir = velocity.normalized; //Vector3.ProjectOnPlane(velocity, atCharacterUp).normalized;
        float stepperDis = deltaTime * velocity.magnitude;
        Vector3 prospectivePos = curPos + stepperDis * moveDir;

        //检测前进方向上的碰撞
        int nHitsForward = CapsuleCast(curPos, curRotation, moveDir, stepperDis, out RaycastHit closestHit, _internalCharacterHits);
        bool bHitForward = nHitsForward > 0 && closestHit.distance > 0f;
        if (bHitForward)
        {
            //Debug.DrawLine(closestHit.point, closestHit.point + closestHit.distance * closestHit.normal, Color.red, Time.deltaTime * 60000, false);
            Debug.Log($"前进方向碰撞:{closestHit.transform.name}");

            #region 检测落差

            bool foundInnerNormal = false;
            bool foundOuterNormal = false;
            bool isStableLedgeInnerNormal = false;
            bool isStableLedgeOuterNormal = false;
            Vector3 checkPos = closestHit.point;
            if (CharacterCollisionsRaycast(
                        checkPos + (atCharacterUp * SecondaryProbesVertical) + (moveDir * SecondaryProbesHorizontal),
                        -atCharacterUp,
                        _maxStepHeight + SecondaryProbesVertical,
                        out RaycastHit innerLedgeHit,
                        _internalCharacterHits) > 0)
            {
                foundInnerNormal = true;
                isStableLedgeInnerNormal = IsStableOnNormal(innerLedgeHit.normal);
            }

            if (CharacterCollisionsRaycast(
                        checkPos + (atCharacterUp * SecondaryProbesVertical) + (-moveDir * SecondaryProbesHorizontal),
                        -atCharacterUp,
                        _maxStepHeight + SecondaryProbesVertical,
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
                    return false;
                }
            }
            if (foundOuterNormal)
            {
                Debug.Log($"{Time.frameCount} outer");
                prospectivePos = curPos + Vector3.ProjectOnPlane(moveDir, outerLedgeHit.normal).normalized * stepperDis;
            }

            #endregion 检测落差
        }

        Vector3 checkStepPos = prospectivePos + _cachedWorldUp * _maxStepHeight;
        float checkStepDis = _maxStepHeight * 2f;
        int nHitsDownward = CapsuleCast(checkStepPos, curRotation, -_cachedWorldUp, checkStepDis, out RaycastHit stepHit, _internalCharacterHits);

        if (nHitsDownward > 0)
        {
            if (!falling)
            {
                //Debug.DrawLine(stepHit.point, stepHit.point + stepHit.distance * stepHit.normal, Color.cyan, Time.deltaTime * 60000, false);
                Vector3 checkOverlapPos = checkStepPos - stepHit.distance * _cachedWorldUp;
                Vector3 correctiveOverlapPos = CharacterOverlapPenetration(checkOverlapPos, curRotation, _internalProbedColliders, _layerMask);
                _player.position = correctiveOverlapPos;
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
                    fallingSpeed = velocity;
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
            _player.position += deltaTime * fallingSpeed;
        }
        return true;
    }

    private int CapsuleCastForwardOrUp(Vector3 position, Quaternion rotation, Vector3 direction, float distance,
        out RaycastHit closestHit, out RaycastHit highestHit, RaycastHit[] _tmpHitsBuffer)
    {
        closestHit = new RaycastHit();
        highestHit = new RaycastHit();
        float highestDistance = Mathf.NegativeInfinity;

        int nHits = Physics.CapsuleCastNonAlloc(
                position + (rotation * _characterTransformToCapsuleBottomHemi) - (direction * GroundProbingBackstepDistance),
                position + (rotation * _characterTransformToCapsuleTopHemi) - (direction * GroundProbingBackstepDistance),
                _capsule.radius,
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

            //Debug.Log($"[{Time.frameCount}] 碰撞 {i}-{nHits} {hit.transform.name} distance:{hit.distance}");
            //DrawHitLine(hit, Color.yellow);

            float hitDistance = hit.distance;
            if (hitDistance > 0f)
            {
                //考虑斜向上方向的碰撞，计算碰撞点的预测高度
                Vector3 moveVector = direction * distance;
                Vector3 hitVector = hit.point - position;
                Vector3 hitHorizontalVector = Vector3.ProjectOnPlane(hitVector, _cachedWorldUp);
                Vector3 endHorizontalVector = Vector3.ProjectOnPlane(moveVector, _cachedWorldUp);
                Vector3 hitPointInPath = position + moveVector * (hitHorizontalVector.magnitude / endHorizontalVector.magnitude);
                //Debug.DrawLine(hitPointInPath, hit.point, Color.red, 10f, false);

                float hitDisVertical = Vector3.Project(hitVector - hitPointInPath, _cachedWorldUp).magnitude;
                if (hitDisVertical > highestDistance)
                {
                    highestHit = hit;
                    highestDistance = hitDisVertical;
                }
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

    private int CapsuleCast(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] _tmpHitsBuffer)
    {
        closestHit = new RaycastHit();

        int nHits = Physics.CapsuleCastNonAlloc(
                position + (rotation * _characterTransformToCapsuleBottomHemi) - (direction * GroundProbingBackstepDistance),
                position + (rotation * _characterTransformToCapsuleTopHemi) - (direction * GroundProbingBackstepDistance),
                _capsule.radius,
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

    private Vector3 CharacterOverlapPenetration(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, LayerMask layers)
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
                        _capsule,
                        position,
                        rotation,
                        overlappedColliders[i],
                        overlappedTransform.position,
                        overlappedTransform.rotation,
                        out Vector3 resolutionDirection,
                        out float resolutionDistance))
                {
                    //Debug.Log($"穿透距离 {resolutionDistance}");

                    //bool isStable = IsStableOnNormal(resolutionDirection);
                    //resolutionDirection = GetObstructionNormal(resolutionDirection, isStable);

                    Vector3 resolutionMovement = resolutionDirection * (resolutionDistance + CollisionOffset);
                    correctiveOverlapPos += resolutionMovement;
                }
            }
        }
        return correctiveOverlapPos;
    }

    private int CharacterOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, LayerMask layers, float inflate = 0f)
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
                    _capsule.radius + inflate,
                    overlappedColliders,
                    layers);

        int nbHits = nbUnfilteredHits;
        for (int i = nbUnfilteredHits - 1; i >= 0; i--)
        {
            if (overlappedColliders[i] == _capsule)// Filter out the character capsule itself
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

    private int CharacterCollisionsRaycast(Vector3 position, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits)
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

            if (hitDistance <= 0f || !hit.collider == _capsule)
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
        return Vector3.Angle(Vector3.up, normal) <= _maxStableSlopeAngle;
    }

    private void DrawHitLine(RaycastHit hit, Color color)
    {
        Debug.Log($"[{Time.frameCount}] {hit.transform.name} {hit.point} distance:{hit.distance}");
        Debug.DrawLine(hit.point, hit.point + hit.distance * hit.normal, color, 10f);
    }
}