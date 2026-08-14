using UnityEngine;

namespace Sandplay.UI
{
    public class LoadingSpinner : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 180f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -_degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
