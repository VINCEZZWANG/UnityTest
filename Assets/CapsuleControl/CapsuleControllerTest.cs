using UnityEngine;

public class CapsuleControllerTest : MonoBehaviour
{
    public Transform Player;

    [Range(0f, 100f)]
    public float Speed = 5f;

    public float MaxStableSlopeAngle = 60f;

    public float MaxStepHeight = 0.5f;

    public LayerMask WalkLayerMask;

    private CapsuleController capsuleController;
    private Camera mainCamera;

    private void Awake()
    {
        capsuleController = new CapsuleController();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        capsuleController.Init(Player, WalkLayerMask.value, MaxStableSlopeAngle, MaxStepHeight);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        Vector3 input = DirectionInput();
        if (input.sqrMagnitude <= 0.01f)
            return;

        capsuleController.SimpleMove(input, deltaTime);
    }

    private Vector3 DirectionInput()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;
        Vector3 dirInPlayer = mainCamera.transform.TransformDirection(input);
        dirInPlayer.y = 0;
        return dirInPlayer * Speed;
    }
}