using UnityEngine;
using TMPro;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class WorldScorePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private float duration = 1.35f;
        [SerializeField] private float floatSpeed = 2.4f;

        private float _elapsed = 0f;
        private Color _baseColor = Color.white;

        private void Awake()
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMeshPro>();
                if (textMesh == null)
                {
                    textMesh = gameObject.AddComponent<TextMeshPro>();
                }
            }

            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 5.5f;
            textMesh.fontStyle = FontStyles.Bold;
        }

        public void Setup(float score, bool isKamikaze, bool isObserved)
        {
            if (textMesh == null) Awake();

            int pts = Mathf.RoundToInt(score);

            if (isKamikaze)
            {
                textMesh.text = $"+{pts}\n<size=65%><color=#00FFFF>KAMIKAZE x2.5!</color></size>";
                _baseColor = new Color(0.2f, 1f, 0.95f, 1f);
            }
            else if (isObserved)
            {
                textMesh.text = $"+{pts}\n<size=65%><color=#FFD700>OBSERVED x3!</color></size>";
                _baseColor = new Color(1f, 0.88f, 0.15f, 1f);
            }
            else
            {
                textMesh.text = $"+{pts}";
                _baseColor = new Color(0.9f, 0.95f, 1f, 1f);
            }

            textMesh.color = _baseColor;
            _elapsed = 0f;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }

            transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

            _elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (_elapsed / duration));

            if (textMesh != null)
            {
                Color c = _baseColor;
                c.a = alpha;
                textMesh.color = c;
            }

            if (_elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
