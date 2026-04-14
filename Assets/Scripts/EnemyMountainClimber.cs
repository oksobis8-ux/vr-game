using UnityEngine;

namespace SlainAndSeasoned.Gameplay
{
    [RequireComponent(typeof(Animator))]
    public class EnemyMountainClimber : MonoBehaviour
    {
        #region Fields

        [Header("Tırmanma Ayarları")]
        [Tooltip("Tırmanma hızı (m/s)")]
        [SerializeField] private float m_ClimbSpeed = 1.5f;

        [Tooltip("Yüzeyden tutulacak mesafe (model pivot'una göre ayarla)")]
        [SerializeField] private float m_SurfaceOffset = 0.05f;

        [Tooltip("Yüzey tespiti için ray uzunluğu")]
        [SerializeField] private float m_RaycastLength = 1.5f;

        [Tooltip("Dağ objelerinin bulunduğu layer")]
        [SerializeField] private LayerMask m_MountainLayer;

        [Header("Hizalama Ayarları")]
        [Tooltip("Yüzey normaline dönüş hızı")]
        [SerializeField] private float m_RotationSpeed = 8f;

        [Tooltip("Normal yumuşatma hızı (yüksek = sert geçişler, düşük = yumuşak)")]
        [SerializeField] private float m_NormalSmoothSpeed = 6f;

        [Header("Döngü Ayarları")]
        [Tooltip("Bu yüksekliğe ulaşınca başlangıca döner (World Space Y)")]
        [SerializeField] private float m_MaxClimbHeight = 30f;

        [Tooltip("Başlangıca dönsün mü?")]
        [SerializeField] private bool m_LoopClimb = true;

        [Header("Debug")]
        [SerializeField] private bool m_ShowDebugInfo = false;

        private Animator m_Animator;
        private Vector3 m_SurfaceNormal = Vector3.up;
        private Vector3 m_StartWorldPosition;
        private bool m_IsOnSurface = false;

        private static readonly int k_IsClimbingParam = Animator.StringToHash("IsClimbing");

        #endregion

        #region Unity Events

        private void Awake()
        {
            m_Animator = GetComponent<Animator>();
            m_StartWorldPosition = transform.position;
        }

        private void Start()
        {
            // İlk yüzey tespiti: çevreye ray atarak dağa temas noktasını bul
            InitializeSurfaceDetection();
        }

        private void Update()
        {
            ClimbUpdate();
            AnimatorUpdate();

            if (m_LoopClimb && transform.position.y >= m_MaxClimbHeight)
            {
                ResetToStart();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!m_ShowDebugInfo) return;

            // Yüzey normalini görselleştir
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, m_SurfaceNormal * 0.5f);

            // Tırmanma yönünü görselleştir
            Gizmos.color = Color.yellow;
            Vector3 climbDir = CalculateClimbDirection();
            Gizmos.DrawRay(transform.position, climbDir * 0.5f);

            // Ray başlangıç noktasını görselleştir
            Gizmos.color = Color.red;
            Vector3 rayOrigin = transform.position + m_SurfaceNormal * m_RaycastLength;
            Gizmos.DrawWireSphere(rayOrigin, 0.05f);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Başlangıçta en yakın dağ yüzeyini bulur, nesneyi yüzeye yerleştirir.
        /// </summary>
        private void InitializeSurfaceDetection()
        {
            // 6 ana yöne kısa ray atarak en yakın dağ yüzeyini bul
            Vector3[] searchDirections =
            {
                Vector3.forward, Vector3.back,
                Vector3.left,    Vector3.right,
                Vector3.up,      Vector3.down
            };

            float closestDistance = float.MaxValue;

            foreach (Vector3 dir in searchDirections)
            {
                if (Physics.Raycast(transform.position, dir, out RaycastHit hit, m_RaycastLength * 3f, m_MountainLayer))
                {
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        m_SurfaceNormal = hit.normal;
                        transform.position = hit.point + hit.normal * m_SurfaceOffset;
                        m_IsOnSurface = true;
                    }
                }
            }

            if (!m_IsOnSurface && m_ShowDebugInfo)
            {
                Debug.LogWarning($"EnemyMountainClimber [{name}]: Başlangıçta yüzey bulunamadı. " +
                                 "MountainLayer ayarını ve nesnenin konumunu kontrol et.");
            }
        }

        /// <summary>
        /// Her frame tırmanma mantığını çalıştırır: hareket et, yüzeye yapış, hizala.
        /// </summary>
        private void ClimbUpdate()
        {
            Vector3 climbDir = CalculateClimbDirection();

            // Hedef pozisyonu hesapla (yüzeyde yukarı hareket)
            Vector3 nextPosition = transform.position + climbDir * (m_ClimbSpeed * Time.deltaTime);

            // Yüzeye yapıştır
            SnapToSurface(ref nextPosition);

            // Pozisyonu uygula
            transform.position = nextPosition;

            // Yüzey normaline göre döndür
            if (m_IsOnSurface)
            {
                AlignToSurface(climbDir);
            }
        }

        /// <summary>
        /// Yüzey düzleminde "dünya yukarısı" yönünü hesaplar.
        /// Dağın yüzeyine göre yukarı doğru tırmanma vektörü verir.
        /// </summary>
        private Vector3 CalculateClimbDirection()
        {
            // Yüzey düzleminde dünya "up" yönünü projeksiyon et
            Vector3 upOnSurface = Vector3.ProjectOnPlane(Vector3.up, m_SurfaceNormal).normalized;

            // Normal neredeyse tam yukarıysa (yatay yüzey), alternatif kullan
            if (upOnSurface.sqrMagnitude < 0.01f)
            {
                upOnSurface = Vector3.ProjectOnPlane(transform.forward, m_SurfaceNormal).normalized;
            }

            return upOnSurface;
        }

        /// <summary>
        /// Nesneyi dağ yüzeyine yapıştırır.
        /// Mevcut surface normal'i kullanarak ray atar, yeni yüzey normal'ini günceller.
        /// </summary>
        private void SnapToSurface(ref Vector3 _position)
        {
            // Yüzey normalinin yukarısından aşağı doğru ray at
            Vector3 rayOrigin = _position + m_SurfaceNormal * m_RaycastLength;
            Vector3 rayDir = -m_SurfaceNormal;

            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, m_RaycastLength * 2f, m_MountainLayer))
            {
                // Normal'i yumuşatarak güncelle (ani dönüşleri önler)
                m_SurfaceNormal = Vector3.Lerp(
                    m_SurfaceNormal,
                    hit.normal,
                    m_NormalSmoothSpeed * Time.deltaTime).normalized;

                _position = hit.point + m_SurfaceNormal * m_SurfaceOffset;
                m_IsOnSurface = true;

                if (m_ShowDebugInfo)
                {
                    Debug.DrawLine(rayOrigin, hit.point, Color.green);
                }
            }
            else
            {
                // Normal ray ile bulunamadı — daha geniş arama yap
                RecoverSurface(ref _position);
            }
        }

        /// <summary>
        /// Yüzey kaybedildiğinde, farklı uzunluklarla tekrar bulmaya çalışır.
        /// </summary>
        private void RecoverSurface(ref Vector3 _position)
        {
            // Daha uzun ray ile tekrar dene
            Vector3 longOrigin = _position + m_SurfaceNormal * (m_RaycastLength * 3f);

            if (Physics.Raycast(longOrigin, -m_SurfaceNormal, out RaycastHit hit, m_RaycastLength * 6f, m_MountainLayer))
            {
                m_SurfaceNormal = hit.normal;
                _position = hit.point + m_SurfaceNormal * m_SurfaceOffset;
                m_IsOnSurface = true;
            }
            else
            {
                m_IsOnSurface = false;

                if (m_ShowDebugInfo)
                {
                    Debug.LogWarning($"EnemyMountainClimber [{name}]: Yüzey tespiti başarısız. " +
                                     $"Pozisyon: {_position}");
                }
            }
        }

        /// <summary>
        /// Nesneyi yüzey normaline göre döndürür.
        /// Up = surface normal, Forward = tırmanma yönü.
        /// </summary>
        private void AlignToSurface(Vector3 _climbDirection)
        {
            if (_climbDirection.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(_climbDirection, m_SurfaceNormal);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                m_RotationSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Nesneyi başlangıç pozisyonuna döndürür.
        /// </summary>
        private void ResetToStart()
        {
            transform.position = m_StartWorldPosition;
            m_SurfaceNormal = Vector3.up;
            m_IsOnSurface = false;

            InitializeSurfaceDetection();

            if (m_ShowDebugInfo)
            {
                Debug.Log($"EnemyMountainClimber [{name}]: Başlangıç noktasına döndü.");
            }
        }

        /// <summary>
        /// Animator parametrelerini günceller.
        /// </summary>
        private void AnimatorUpdate()
        {
            m_Animator.SetBool(k_IsClimbingParam, m_IsOnSurface);
        }

        #endregion
    }
}
