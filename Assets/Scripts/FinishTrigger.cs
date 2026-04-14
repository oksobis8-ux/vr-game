using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlainAndSeasoned.Gameplay
{
    /// <summary>
    /// Finish objesine temas eden Player veya Enemy aktif sahneyi yeniden yükler.
    /// Bu component Trigger Collider olan Finish objesine eklenir.
    /// Inspector'dan m_TriggerTags listesini düzenleyerek hangi tag'lerin tetikleyeceği ayarlanır.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FinishTrigger : MonoBehaviour
    {
        #region Fields

        [Header("Tetikleme Ayarları")]
        [Tooltip("Sahneyi yeniden yükleyecek tag listesi (Player ve/veya Enemy)")]
        [SerializeField] private string[] m_TriggerTags = { "Player", "Enemy" };

        [Tooltip("Sahne yeniden yüklenmeden önce bekleme süresi (saniye)")]
        [SerializeField] private float m_ReloadDelay = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool m_ShowDebugInfo = false;

        private bool m_IsTriggered = false;

        #endregion

        #region Unity Events

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"FinishTrigger [{name}]: Collider otomatik olarak Trigger yapıldı.");
            }
        }

        private void OnTriggerEnter(Collider _other)
        {
            if (m_IsTriggered) return;

            foreach (string tag in m_TriggerTags)
            {
                if (_other.CompareTag(tag))
                {
                    m_IsTriggered = true;

                    if (m_ShowDebugInfo)
                        Debug.Log($"FinishTrigger: {_other.name} ({tag}) temas etti. " +
                                  $"{m_ReloadDelay}s sonra sahne yeniden yükleniyor.");

                    Invoke(nameof(ReloadScene), m_ReloadDelay);
                    return;
                }
            }
        }

        #endregion

        #region Private Methods

        private void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #endregion
    }
}
