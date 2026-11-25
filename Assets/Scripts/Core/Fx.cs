using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieFPS
{
    /// <summary>Lightweight procedural visual effects (tracers, impacts, explosions).</summary>
    public class Fx : MonoBehaviour
    {
        public Material tracerMaterial;
        public Material impactMaterial;
        public Material bloodMaterial;
        public Material explosionMaterial;

        static Fx instance;
        static Mesh cubeMesh;
        static Mesh sphereMesh;

        void Awake()
        {
            instance = this;
            cubeMesh = GetMesh(PrimitiveType.Cube);
            sphereMesh = GetMesh(PrimitiveType.Sphere);
        }

        static Mesh GetMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        public static void Tracer(Vector3 from, Vector3 to)
        {
            if (instance == null) return;
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.025f;
            line.endWidth = 0.01f;
            line.sharedMaterial = instance.tracerMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            Destroy(go, 0.04f);
        }

        public static void Impact(Vector3 point, Vector3 normal, bool flesh)
        {
            if (instance == null) return;
            Material material = flesh ? instance.bloodMaterial : instance.impactMaterial;
            float size = flesh ? 0.18f : 0.1f;
            GameObject go = CreateVisual("Impact", cubeMesh, material, point + normal * 0.02f, Vector3.one * size);
            go.AddComponent<FxAnimator>().Setup(Vector3.one * size, Vector3.zero, 0.25f);
        }

        public static void Explosion(Vector3 center, float radius)
        {
            if (instance == null) return;
            GameObject go = CreateVisual("Explosion", sphereMesh, instance.explosionMaterial, center, Vector3.one * 0.5f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = radius * 3f;
            light.intensity = 8f;
            go.AddComponent<FxAnimator>().Setup(Vector3.one * 0.5f, Vector3.one * radius * 2f, 0.3f, light);
        }

        static GameObject CreateVisual(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, Random.rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }

    internal class FxAnimator : MonoBehaviour
    {
        Vector3 fromScale;
        Vector3 toScale;
        float duration;
        float startTime;
        Light fxLight;
        float lightIntensity;

        public void Setup(Vector3 from, Vector3 to, float time, Light light = null)
        {
            fromScale = from;
            toScale = to;
            duration = time;
            startTime = Time.time;
            fxLight = light;
            if (light != null) lightIntensity = light.intensity;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.time - startTime) / duration);
            transform.localScale = Vector3.Lerp(fromScale, toScale, t);
            if (fxLight != null) fxLight.intensity = lightIntensity * (1f - t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
