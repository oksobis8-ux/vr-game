using UnityEngine;

/// <summary>
/// VR karakter body animasyon kontrolcüsü - Klavye/Mouse ve VR için.
/// VRMovementController'dan hareket ve tırmanma bilgilerini alır ve Animator'a aktarır.
/// </summary>
[RequireComponent(typeof(Animator))]
public class VRBodyAnimator : MonoBehaviour
{
    #region Fields

    [Header("Referanslar")]
    [Tooltip("VR Movement Controller referansı (otomatik bulunacak)")]
    [SerializeField] private VRMovementController m_MovementController;

    [Header("Animasyon Ayarları")]
    [Tooltip("Animasyon geçişlerinin yumuşaklığı")]
    [SerializeField] private float m_AnimationDamping = 0.1f;

    [Header("Debug")]
    [Tooltip("Debug bilgilerini göster")]
    [SerializeField] private bool m_ShowDebugInfo = false;

    // Komponentler
    private Animator m_Animator;

    // Animator parametre isimleri
    private static readonly int k_SpeedParam = Animator.StringToHash("Speed");
    private static readonly int k_IsGroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int k_IsClimbingParam = Animator.StringToHash("IsClimbing");

    #endregion

    #region Unity Events

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();

        if (m_MovementController == null)
        {
            m_MovementController = GetComponentInParent<VRMovementController>();

            if (m_MovementController == null)
            {
                Transform xrOrigin = transform.root;
                m_MovementController = xrOrigin.GetComponent<VRMovementController>();
            }
        }

        if (m_MovementController == null)
        {
            Debug.LogWarning($"VRBodyAnimator: VRMovementController bulunamadı! {gameObject.name} objesinde Animator çalışmayacak.");
        }
    }

    private void Update()
    {
        if (m_MovementController == null || m_Animator == null)
            return;

        UpdateAnimatorParameters();
    }

    #endregion

    #region Private Methods

    private void UpdateAnimatorParameters()
    {
        float speed = m_MovementController.CurrentSpeed;
        m_Animator.SetFloat(k_SpeedParam, speed, m_AnimationDamping, Time.deltaTime);

        bool isGrounded = m_MovementController.IsGrounded;
        m_Animator.SetBool(k_IsGroundedParam, isGrounded);

        bool isClimbing = m_MovementController.IsClimbing;
        m_Animator.SetBool(k_IsClimbingParam, isClimbing);

        if (m_ShowDebugInfo)
        {
            Debug.Log($"VRBodyAnimator - Speed: {speed:F2}, IsGrounded: {isGrounded}, IsClimbing: {isClimbing}");
        }
    }

    #endregion

    #region Public API

    public void SetMovementController(VRMovementController _controller);
    {
        m_MovementController = _controller;
    }

public void SetAnimationDamping(float _damping)
    {
        m_AnimationDamping = Mathf.Max(0f, _damping);
    }

    #endregion
}
