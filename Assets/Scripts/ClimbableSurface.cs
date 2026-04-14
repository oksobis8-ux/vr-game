using UnityEngine;

/// <summary>
/// Tırmanılabilir yüzey işaretleyici bileşeni.
/// Bu bileşene sahip ve "Climbable" layer'ında olan objeler tırmanılabilir olur.
/// Dağ objesine (ve varsa child collider'larına) eklenmelidir.
/// </summary>
public class ClimbableSurface : MonoBehaviour
{
    #region Fields

    [Header("Tırmanma Ayarları")]
    [Tooltip("Bu yüzeyde tırmanma hız çarpanı (1 = normal)")]
    [SerializeField] private float m_ClimbSpeedMultiplier = 1f;

    [Tooltip("Yüzeye tutunma sırasında uygulanan sürtünme (0 = kaygan, 1 = tam tutunma)")]
    [SerializeField] private float m_GripFriction = 1f;

    #endregion

    #region Public API

    public float ClimbSpeedMultiplier => m_ClimbSpeedMultiplier;
    public float GripFriction => m_GripFriction;

    #endregion
}
