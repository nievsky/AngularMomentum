using UnityEngine;

namespace OrbitalPanopticon.Gameplay
{
    [CreateAssetMenu(fileName = "SO_Scrap_", menuName = "Orbital Panopticon/Scrap Data")]
    public class ScrapData : ScriptableObject
    {
        [Header("General Information")]
        [SerializeField] private string scrapName = "Scrap";
        [SerializeField, Range(1, 3)] private int tier = 1;

        [Header("Economics & Physics")]
        [Tooltip("Base score / points awarded upon black hole consumption.")]
        [SerializeField] private float baseValue = 10f;

        [Tooltip("Mass used for Rigidbody physics and cargo payload calculation.")]
        [SerializeField] private float mass = 1f;

        [Tooltip("Base visual and collider scale for the scrap object.")]
        [SerializeField] private Vector3 baseScale = Vector3.one;

        public string ScrapName => scrapName;
        public int Tier => tier;
        public float BaseValue => baseValue;
        public float Mass => mass;
        public Vector3 BaseScale => baseScale;
    }
}
