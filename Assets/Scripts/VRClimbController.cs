using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// VR Tırmanma Kontrolcüsü.
/// Sol/sağ Meta Quest 2 trigger tuşları ile dağ yüzeyine tutunma ve tırmanma.
/// Klavye/mouse desteği de vardır (test ve PC gameplay için).
///
/// Tırmanma mekaniği:
/// - Trigger basılı + el yüzeye yakın = tutunma
/// - Tutunurken controller'ı aşağı çek = karakter yukarı gider (ters hareket)
/// - Her iki trigger da bırakılırsa = düşme (yerçekimi)
///
/// Klavye/Mouse:
/// - Sol/sağ mouse butonu = tutunma
/// - W/S = yukarı/aşağı tırmanma
/// - Mouse aşağı çekme = yukarı tırmanma
/// </summary>
public class VRClimbController : MonoBehaviour
{
    #region Fields

    [Header("Referanslar")]
    [Tooltip("Sol controller Transform (otomatik bulunur)")]
    [SerializeField] private Transform m_LeftControllerTransform;

    [Tooltip("Sağ controller Transform (otomatik bulunur)")]
    [SerializeField] private Transform m_RightControllerTransform;

    [Tooltip("Kamera Transform (otomatik bulunur)")]
    [SerializeField] private Transform m_HeadTransform;

    [Header("Tırmanma Ayarları")]
    [Tooltip("El ile tutunma algılama yarıçapı (metre)")]
    [SerializeField] private float m_GrabRadius = 0.3f;

    [Tooltip("Trigger basma eşiği (0-1 arası, bu değerin üstünde tutunma aktif)")]
    [SerializeField] private float m_TriggerThreshold = 0.5f;

    [Tooltip("VR tırmanma hız çarpanı")]
    [SerializeField] private float m_ClimbSpeed = 1.5f;

    [Tooltip("Tırmanılabilir yüzey layer'ı")]
    [SerializeField] private LayerMask m_ClimbableLayer;

    [Header("Klavye/Mouse Ayarları")]
    [Tooltip("Klavye ile tırmanma hızı (m/s)")]
    [SerializeField] private float m_KeyboardClimbSpeed = 3f;

    [Tooltip("Mouse tırmanma hassasiyeti")]
    [SerializeField] private float m_MouseClimbSensitivity = 0.02f;

    [Tooltip("Kameradan yüzey algılama mesafesi (klavye/mouse modu)")]
    [SerializeField] private float m_CameraReachDistance = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool m_ShowDebugInfo = false;

    // Cached components
    private VRMovementController m_MovementController;
    private CharacterController m_CharacterController;

    // XR Controller devices
    private UnityEngine.XR.InputDevice m_LeftDevice;
    private UnityEngine.XR.InputDevice m_RightDevice;
    private bool m_LeftDeviceValid;
    private bool m_RightDeviceValid;

    // Trigger values
    private float m_LeftTriggerValue;
    private float m_RightTriggerValue;

    // Surface proximity
    private bool m_LeftNearSurface;
    private bool m_RightNearSurface;
    private bool m_CameraNearSurface;

    // Grip state
    private bool m_LeftGripping;
    private bool m_RightGripping;

    // Controller LOCAL position tracking (local = tracking-only, no feedback loop)
    private Vector3 m_LastLeftLocalPos;
    private Vector3 m_LastRightLocalPos;
    private bool m_LeftPosInitialized;
    private bool m_RightPosInitialized;

    // Active hand tracking
    private enum ActiveHand { None, Left, Right }
    private ActiveHand m_ActiveHand = ActiveHand.None;

    // Climbing state
    private bool m_IsClimbing;

    // Pre-allocated overlap buffer
    private readonly Collider[] m_OverlapResults = new Collider[10];

    // Reusable device list
    private readonly List<UnityEngine.XR.InputDevice> m_DeviceBuffer = new List<UnityEngine.XR.InputDevice>();

    #endregion

    #region Unity Events

    private void Awake()
    {
        m_MovementController = GetComponent<VRMovementController>();
        m_CharacterController = GetComponent<CharacterController>();

        FindControllerTransforms();
        FindHeadTransform();
    }

    private void Start()
    {
        FindControllerDevices();
    }

    private void Update()
    {
        if (!m_LeftDeviceValid || !m_RightDeviceValid)
            FindControllerDevices();

        ReadTriggerInputs();
        CheckClimbableSurfaces();
        UpdateGripState();

        if (m_IsClimbing)
            ApplyClimbMovement();

        if (m_MovementController != null)
            m_MovementController.SetClimbingState(m_IsClimbing);
    }

    private void OnGUI()
    {
        if (!m_ShowDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 220, 350, 200));
        GUILayout.Label($"--- VR Climb Controller ---");
        GUILayout.Label($"Is Climbing: {m_IsClimbing}");
        GUILayout.Label($"Left Grip: {m_LeftGripping} | Trigger: {m_LeftTriggerValue:F2} | Near: {m_LeftNearSurface}");
        GUILayout.Label($"Right Grip: {m_RightGripping} | Trigger: {m_RightTriggerValue:F2} | Near: {m_RightNearSurface}");
        GUILayout.Label($"Camera Near Surface: {m_CameraNearSurface}");
        GUILayout.Label($"Active Hand: {m_ActiveHand}");
        GUILayout.EndArea();
    }

    #endregion

    #region Private Methods

    private void FindControllerTransforms()
    {
        if (m_LeftControllerTransform != null && m_RightControllerTransform != null)
            return;

        Transform cameraOffset = transform.Find("Camera Offset");
        if (cameraOffset == null)
            return;

        if (m_LeftControllerTransform == null)
            m_LeftControllerTransform = cameraOffset.Find("Left Controller");
        if (m_RightControllerTransform == null)
            m_RightControllerTransform = cameraOffset.Find("Right Controller");
    }

    private void FindHeadTransform()
    {
        if (m_HeadTransform != null) return;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            m_HeadTransform = mainCam.transform;
            return;
        }

        Transform cameraOffset = transform.Find("Camera Offset/Main Camera");
        if (cameraOffset != null)
            m_HeadTransform = cameraOffset;
    }

    private void FindControllerDevices()
    {
        if (!m_LeftDeviceValid)
        {
            m_DeviceBuffer.Clear();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, m_DeviceBuffer);
            if (m_DeviceBuffer.Count > 0)
            {
                m_LeftDevice = m_DeviceBuffer[0];
                m_LeftDeviceValid = m_LeftDevice.isValid;
            }
        }

        if (!m_RightDeviceValid)
        {
            m_DeviceBuffer.Clear();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, m_DeviceBuffer);
            if (m_DeviceBuffer.Count > 0)
            {
                m_RightDevice = m_DeviceBuffer[0];
                m_RightDeviceValid = m_RightDevice.isValid;
            }
        }
    }

    /// <summary>
    /// VR trigger ve klavye/mouse input'larını okur
    /// </summary>
    private void ReadTriggerInputs()
    {
        m_LeftTriggerValue = 0f;
        m_RightTriggerValue = 0f;

        // VR trigger input
        if (m_LeftDeviceValid && m_LeftDevice.isValid)
            m_LeftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out m_LeftTriggerValue);

        if (m_RightDeviceValid && m_RightDevice.isValid)
            m_RightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out m_RightTriggerValue);

        // Klavye/Mouse fallback
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed)
                m_LeftTriggerValue = Mathf.Max(m_LeftTriggerValue, 1f);
            if (Mouse.current.rightButton.isPressed)
                m_RightTriggerValue = Mathf.Max(m_RightTriggerValue, 1f);
        }
    }

    /// <summary>
    /// Tırmanılabilir yüzeye yakınlık kontrolü.
    /// VR: Controller pozisyonları etrafında OverlapSphere.
    /// Klavye/Mouse: Kameradan ileri doğru raycast + oyuncu etrafında OverlapSphere.
    /// </summary>
    private void CheckClimbableSurfaces()
    {
        m_LeftNearSurface = false;
        m_RightNearSurface = false;
        m_CameraNearSurface = false;

        // VR: Her controller etrafında sphere check
        if (m_LeftControllerTransform != null && m_LeftControllerTransform.gameObject.activeInHierarchy)
        {
            int count = Physics.OverlapSphereNonAlloc(
                m_LeftControllerTransform.position, m_GrabRadius, m_OverlapResults, m_ClimbableLayer);
            m_LeftNearSurface = count > 0;
        }

        if (m_RightControllerTransform != null && m_RightControllerTransform.gameObject.activeInHierarchy)
        {
            int count = Physics.OverlapSphereNonAlloc(
                m_RightControllerTransform.position, m_GrabRadius, m_OverlapResults, m_ClimbableLayer);
            m_RightNearSurface = count > 0;
        }

        // Klavye/Mouse: Kamera ileri raycast + oyuncu etrafı
        if (m_HeadTransform != null)
        {
            if (Physics.Raycast(m_HeadTransform.position, m_HeadTransform.forward,
                m_CameraReachDistance, m_ClimbableLayer))
            {
                m_CameraNearSurface = true;
            }

            if (!m_CameraNearSurface)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    transform.position + Vector3.up, 1.2f, m_OverlapResults, m_ClimbableLayer);
                m_CameraNearSurface = count > 0;
            }
        }

        // VR controller yoksa veya aktif değilse kamera kontrolünü kullan
        bool leftControllerActive = m_LeftControllerTransform != null
            && m_LeftControllerTransform.gameObject.activeInHierarchy;
        bool rightControllerActive = m_RightControllerTransform != null
            && m_RightControllerTransform.gameObject.activeInHierarchy;

        if (!leftControllerActive)
            m_LeftNearSurface = m_CameraNearSurface;
        if (!rightControllerActive)
            m_RightNearSurface = m_CameraNearSurface;
    }

    /// <summary>
    /// Tutunma durumunu günceller.
    /// Yeni tutunma = trigger + yüzeye yakın.
    /// Devam eden tutunma = sadece trigger (sticky grip).
    /// </summary>
    private void UpdateGripState()
    {
        bool wasLeftGripping = m_LeftGripping;
        bool wasRightGripping = m_RightGripping;
        bool triggerLeftPressed = m_LeftTriggerValue >= m_TriggerThreshold;
        bool triggerRightPressed = m_RightTriggerValue >= m_TriggerThreshold;

        // Sticky grip: ilk tutunmada yüzeye yakınlık gerekli, devamında sadece trigger yeterli
        if (!wasLeftGripping)
            m_LeftGripping = triggerLeftPressed && m_LeftNearSurface;
        else
            m_LeftGripping = triggerLeftPressed;

        if (!wasRightGripping)
            m_RightGripping = triggerRightPressed && m_RightNearSurface;
        else
            m_RightGripping = triggerRightPressed;

        // Yeni tutunma başlangıcında pozisyon kaydet (local position = tracking-only)
        if (m_LeftGripping && !wasLeftGripping)
        {
            m_ActiveHand = ActiveHand.Left;
            if (m_LeftControllerTransform != null)
            {
                m_LastLeftLocalPos = m_LeftControllerTransform.localPosition;
                m_LeftPosInitialized = true;
            }
        }

        if (m_RightGripping && !wasRightGripping)
        {
            m_ActiveHand = ActiveHand.Right;
            if (m_RightControllerTransform != null)
            {
                m_LastRightLocalPos = m_RightControllerTransform.localPosition;
                m_RightPosInitialized = true;
            }
        }

        // Aktif el bırakıldığında diğer ele geç
        if (m_ActiveHand == ActiveHand.Left && !m_LeftGripping && m_RightGripping)
        {
            m_ActiveHand = ActiveHand.Right;
            if (m_RightControllerTransform != null)
            {
                m_LastRightLocalPos = m_RightControllerTransform.localPosition;
                m_RightPosInitialized = true;
            }
        }
        else if (m_ActiveHand == ActiveHand.Right && !m_RightGripping && m_LeftGripping)
        {
            m_ActiveHand = ActiveHand.Left;
            if (m_LeftControllerTransform != null)
            {
                m_LastLeftLocalPos = m_LeftControllerTransform.localPosition;
                m_LeftPosInitialized = true;
            }
        }

        // Tırmanma durumunu güncelle
        bool wasClimbing = m_IsClimbing;
        m_IsClimbing = m_LeftGripping || m_RightGripping;

        // İki el de bırakıldıysa sıfırla
        if (!m_IsClimbing)
        {
            m_ActiveHand = ActiveHand.None;
            m_LeftPosInitialized = false;
            m_RightPosInitialized = false;
        }
    }

    /// <summary>
    /// Tırmanma hareketini uygular.
    /// VR: Controller local position deltasının tersi.
    /// Klavye/Mouse: W/S tuşları ve mouse delta.
    /// </summary>
    private void ApplyClimbMovement()
    {
        if (m_CharacterController == null) return;

        Vector3 climbDelta = Vector3.zero;
        bool hasVRMovement = false;

        // VR controller tırmanma (local position kullanarak feedback loop'u önler)
        if (m_ActiveHand == ActiveHand.Left
            && m_LeftControllerTransform != null
            && m_LeftPosInitialized)
        {
            Vector3 currentLocal = m_LeftControllerTransform.localPosition;
            Vector3 localDelta = currentLocal - m_LastLeftLocalPos;
            climbDelta = -localDelta * m_ClimbSpeed;
            m_LastLeftLocalPos = currentLocal;
            hasVRMovement = localDelta.sqrMagnitude > 0.000001f;
        }
        else if (m_ActiveHand == ActiveHand.Right
            && m_RightControllerTransform != null
            && m_RightPosInitialized)
        {
            Vector3 currentLocal = m_RightControllerTransform.localPosition;
            Vector3 localDelta = currentLocal - m_LastRightLocalPos;
            climbDelta = -localDelta * m_ClimbSpeed;
            m_LastRightLocalPos = currentLocal;
            hasVRMovement = localDelta.sqrMagnitude > 0.000001f;
        }

        // Klavye/Mouse tırmanma (VR hareketi yoksa)
        if (!hasVRMovement)
        {
            climbDelta += GetKeyboardClimbDelta();
        }

        if (climbDelta.sqrMagnitude > 0.000001f)
            m_CharacterController.Move(climbDelta);
    }

    /// <summary>
    /// Klavye/Mouse ile tırmanma delta vektörünü hesaplar
    /// </summary>
    private Vector3 GetKeyboardClimbDelta()
    {
        Vector3 delta = Vector3.zero;

        // Mouse delta: aşağı çekme = yukarı tırmanma (ters)
        if (Mouse.current != null)
        {
            bool anyMouseGrip = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
            if (anyMouseGrip)
            {
                float mouseDeltaY = Mouse.current.delta.y.ReadValue();
                delta.y -= mouseDeltaY * m_MouseClimbSensitivity;
            }
        }

        // Klavye: W = yukarı, S = aşağı, A/D = yanlara
        if (Keyboard.current != null)
        {
            bool anyGrip = m_LeftGripping || m_RightGripping;
            if (anyGrip)
            {
                float vertical = 0f;
                float horizontal = 0f;

                if (Keyboard.current.wKey.isPressed) vertical += 1f;
                if (Keyboard.current.sKey.isPressed) vertical -= 1f;
                if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
                if (Keyboard.current.dKey.isPressed) horizontal += 1f;

                delta.y += vertical * m_KeyboardClimbSpeed * Time.deltaTime;

                // Yatay hareket: kamera yönüne göre
                if (m_HeadTransform != null && Mathf.Abs(horizontal) > 0.01f)
                {
                    Vector3 right = m_HeadTransform.right;
                    right.y = 0f;
                    right.Normalize();
                    delta += right * horizontal * m_KeyboardClimbSpeed * Time.deltaTime;
                }
            }
        }

        return delta;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Karakter şu an tırmanıyor mu?
    /// </summary>
    public bool IsClimbing => m_IsClimbing;

    /// <summary>
    /// Sol el tutunmuş mu?
    /// </summary>
    public bool IsLeftGripping => m_LeftGripping;

    /// <summary>
    /// Sağ el tutunmuş mu?
    /// </summary>
    public bool IsRightGripping => m_RightGripping;

    #endregion
}
