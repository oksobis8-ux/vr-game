using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class VRMovementController : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Header("Dönüş Ayarları")]
    [SerializeField] private float rotationLerpSpeed = 10f;

    [Header("Referanslar")]
    [SerializeField] private Transform headTransform;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;

    private Vector2 keyboardInput;
    private Vector2 controllerInput;
    private bool isRunning;

    public float CurrentSpeed { get; private set; }
    public bool IsGrounded => isGrounded;
    public bool IsClimbing { get; private set; }

    private InputDevice leftControllerDevice;
    private bool leftControllerValid = false;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (headTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                headTransform = cam.transform;
        }
    }

    void Start()
    {
        FindLeftController();
    }

    void Update()
    {
        if (!leftControllerValid)
            FindLeftController();

        CheckGrounded();

        if (!IsClimbing)
        {
            GetInputs();
            Move();
            ApplyGravity();
        }

        UpdateAnimationValues();
    }

    void LateUpdate()
    {
        if (headTransform == null)
            return;

        Vector3 forward = headTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationLerpSpeed * Time.deltaTime);
    }

    void CheckGrounded()
    {
        isGrounded = characterController.isGrounded;

        if (Physics.Raycast(transform.position, Vector3.down,
            groundCheckDistance + characterController.skinWidth))
        {
            isGrounded = true;
        }
    }

    void GetInputs()
    {
        keyboardInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) keyboardInput.y += 1;
            if (Keyboard.current.sKey.isPressed) keyboardInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) keyboardInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) keyboardInput.x += 1;

            keyboardInput = keyboardInput.normalized;
            isRunning = Keyboard.current.leftShiftKey.isPressed;
        }

        controllerInput = Vector2.zero;

        if (leftControllerValid && leftControllerDevice.isValid)
        {
            if (leftControllerDevice.TryGetFeatureValue(
                CommonUsages.primary2DAxis,
                out Vector2 stick))
            {
                controllerInput = stick;
            }
        }
    }

    void Move()
    {
        Vector2 moveInput =
            controllerInput.magnitude > 0.1f ? controllerInput : keyboardInput;

        if (moveInput.magnitude < 0.1f)
        {
            velocity.x = 0;
            velocity.z = 0;
            return;
        }

        float speed = moveSpeed * (isRunning ? runMultiplier : 1f);

        Vector3 forward = headTransform.forward;
        Vector3 right = headTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDir =
            (forward * moveInput.y + right * moveInput.x).normalized;

        velocity.x = moveDir.x * speed;
        velocity.z = moveDir.z * speed;

        characterController.Move(
            new Vector3(velocity.x, 0, velocity.z) * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;
        else
            velocity.y += gravity * Time.deltaTime;

        characterController.Move(
            new Vector3(0, velocity.y, 0) * Time.deltaTime);
    }

    void FindLeftController()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);

        if (devices.Count > 0)
        {
            leftControllerDevice = devices[0];
            leftControllerValid = leftControllerDevice.isValid;

            if (showDebugInfo)
                Debug.Log("Left Controller Found: " + leftControllerDevice.name);
        }
        else
        {
            leftControllerValid = false;
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("Keyboard Input: " + keyboardInput);
        GUILayout.Label("Controller Input: " + controllerInput);
        GUILayout.Label("Is Grounded: " + isGrounded);
        GUILayout.Label("Velocity: " + velocity);
        GUILayout.EndArea();
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0, speed);
    }

    public void SetRunMultiplier(float multiplier)
    {
        runMultiplier = Mathf.Max(1, multiplier);
    }

    void UpdateAnimationValues()
    {
        if (IsClimbing)
        {
            CurrentSpeed = 0;
            return;
        }

        Vector2 moveInput =
            controllerInput.magnitude > 0.1f ? controllerInput : keyboardInput;

        CurrentSpeed = Mathf.Clamp01(moveInput.magnitude);
    }

    public void SetClimbingState(bool state)
    {
        bool wasClimbing = IsClimbing;
        IsClimbing = state;

        if (wasClimbing != state)
            velocity = Vector3.zero;
    }
}