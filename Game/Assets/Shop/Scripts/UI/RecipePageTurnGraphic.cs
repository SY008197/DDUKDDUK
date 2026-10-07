using UnityEngine;
using UnityEngine.UI;

namespace DdukDduk.Shop
{
    /// <summary>
    /// A photographed page rotating around the binding. The cylindrical bend is
    /// constrained so its projected strips always remain in their original order.
    /// Front and back snapshots are supplied in their normal, readable orientation.
    /// </summary>
    [AddComponentMenu("UI/Recipe Page Turn Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecipePageTurnGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField] private int direction = 1;
        [SerializeField] private bool isShadow;
        [SerializeField] private Texture frontTexture;
        [SerializeField] private Texture backTexture;
        [SerializeField] private Rect frontUv = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] private Rect backUv = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] private float startWidth;
        [SerializeField] private float endWidth;
        [SerializeField, Range(0f, .25f)] private float startTopInset;
        [SerializeField, Range(0f, .25f)] private float endTopInset;

        protected override void OnEnable()
        {
            // Legacy inactive pages need their renderer before Graphic registers with its canvas.
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        public float Progress
        {
            get => progress;
            set
            {
                var next = Mathf.Clamp01(value);
                if (progress == next) return;
                var changedFace = (progress >= .5f) != (next >= .5f);
                progress = next;
                SetVerticesDirty();
                // CanvasRenderer retains its texture independently of the mesh.
                // Marking only vertices at the midpoint would leave the old face.
                if (changedFace) SetMaterialDirty();
            }
        }

        /// <summary>+1 turns a right page left; -1 turns a left page right.</summary>
        public int Direction
        {
            get => direction;
            set
            {
                var next = value < 0 ? -1 : 1;
                if (direction == next) return;
                direction = next;
                SetVerticesDirty();
            }
        }

        public bool IsShadow
        {
            get => isShadow;
            set
            {
                if (isShadow == value) return;
                isShadow = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Texture FrontTexture
        {
            get => frontTexture;
            set
            {
                if (frontTexture == value) return;
                frontTexture = value;
                SetMaterialDirty();
            }
        }

        public Texture BackTexture
        {
            get => backTexture;
            set
            {
                if (backTexture == value) return;
                backTexture = value;
                SetMaterialDirty();
            }
        }

        public Rect FrontUv
        {
            get => frontUv;
            set { if (frontUv == value) return; frontUv = value; SetVerticesDirty(); }
        }

        public Rect BackUv
        {
            get => backUv;
            set { if (backUv == value) return; backUv = value; SetVerticesDirty(); }
        }

        /// <summary>Optional photographed page widths; zero uses rect.width.</summary>
        public float StartWidth
        {
            get => startWidth;
            set { if (startWidth == value) return; startWidth = value; SetVerticesDirty(); }
        }

        public float EndWidth
        {
            get => endWidth;
            set { if (endWidth == value) return; endWidth = value; SetVerticesDirty(); }
        }

        /// <summary>Trim the photographed outer top corner without stretching its contents.</summary>
        public float StartTopInset
        {
            get => startTopInset;
            set
            {
                var next = Mathf.Clamp(value, 0f, .25f);
                if (startTopInset == next) return;
                startTopInset = next;
                SetVerticesDirty();
            }
        }

        public float EndTopInset
        {
            get => endTopInset;
            set
            {
                var next = Mathf.Clamp(value, 0f, .25f);
                if (endTopInset == next) return;
                endTopInset = next;
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (isShadow) return Texture2D.whiteTexture;
                var texture = progress < .5f ? frontTexture : backTexture;
                return texture != null ? texture : Texture2D.whiteTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = rectTransform.rect;
            var height = rect.height;
            var t = Mathf.Clamp01(progress);
            var blend = Mathf.SmoothStep(0f, 1f, t);
            var width = Mathf.Lerp(startWidth > 0f ? startWidth : rect.width,
                endWidth > 0f ? endWidth : rect.width, blend);
            var topInset = Mathf.Lerp(Mathf.Clamp(startTopInset, 0f, .25f),
                Mathf.Clamp(endTopInset, 0f, .25f), blend);
            if (width <= 0f || height <= 0f) return;

            var angle = Mathf.PI * t;
            var sine = Mathf.Sin(angle);
            var cosine = Mathf.Cos(angle);
            // A zero-width page cannot show either face. Omitting the subpixel
            // mesh also avoids unstable winding and texture aliasing at the seam.
            if (Mathf.Abs(cosine) < .0015f) return;

            // For every strip, tangent = angle + curvature * (u - .5).
            // |curvature / 2| <= .35 * |cos(angle)|. Thus no tangent can
            // cross pi/2, and projected X remains strictly monotone. The old
            // additive sine bow crossed this boundary and overlapped itself.
            var curvature = .70f * sine * cosine;
            var sign = direction < 0 ? -1f : 1f;
            var back = t >= .5f;
            var uvRect = back ? backUv : frontUv;
            var reverseUv = (direction < 0) != back;
            var clockwise = sign * cosine > 0f;
            const int strips = 64;
            for (var i = 0; i < strips; i++)
            {
                var u0 = i / (float)strips;
                var u1 = (i + 1) / (float)strips;
                var top0 = u0 * (1f - topInset);
                var top1 = u1 * (1f - topInset);
                // Upper book edges in the photograph slope inward. Sampling the
                // same shortened parameter in geometry and UVs clips the desk
                // corner out of the sheet while leaving printed contents aligned.
                var bottomPoint0 = ProjectEdge(u0, false, rect, width, height,
                    angle, curvature, sign, sine, cosine);
                var topPoint0 = ProjectEdge(top0, true, rect, width, height,
                    angle, curvature, sign, sine, cosine);
                var topPoint1 = ProjectEdge(top1, true, rect, width, height,
                    angle, curvature, sign, sine, cosine);
                var bottomPoint1 = ProjectEdge(u1, false, rect, width, height,
                    angle, curvature, sign, sine, cosine);

                var average = (u0 + u1) * .5f;
                var shade = 1f - sine * (.14f + .10f * Mathf.Sin(average * Mathf.PI));
                var tint = isShadow
                    ? new Color(.12f, .08f, .045f, .19f * sine * color.a)
                    : new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
                var ink = (Color32)tint;
                var tex0 = reverseUv ? 1f - u0 : u0;
                var tex1 = reverseUv ? 1f - u1 : u1;
                var uv0 = Mathf.Lerp(uvRect.xMin, uvRect.xMax, tex0);
                var uv1 = Mathf.Lerp(uvRect.xMin, uvRect.xMax, tex1);
                var texTop0 = reverseUv ? 1f - top0 : top0;
                var texTop1 = reverseUv ? 1f - top1 : top1;
                var uvTop0 = Mathf.Lerp(uvRect.xMin, uvRect.xMax, texTop0);
                var uvTop1 = Mathf.Lerp(uvRect.xMin, uvRect.xMax, texTop1);
                var start = vertices.currentVertCount;
                vertices.AddVert(bottomPoint0, ink,
                    new Vector2(uv0, uvRect.yMin));
                vertices.AddVert(topPoint0, ink,
                    new Vector2(uvTop0, uvRect.yMax));
                vertices.AddVert(topPoint1, ink,
                    new Vector2(uvTop1, uvRect.yMax));
                vertices.AddVert(bottomPoint1, ink,
                    new Vector2(uv1, uvRect.yMin));
                // Keep the winding consistent on both sides of the binding.
                if (clockwise)
                {
                    vertices.AddTriangle(start, start + 1, start + 2);
                    vertices.AddTriangle(start + 2, start + 3, start);
                }
                else
                {
                    vertices.AddTriangle(start, start + 2, start + 1);
                    vertices.AddTriangle(start + 2, start, start + 3);
                }
            }
        }

        private Vector3 ProjectEdge(float u, bool top, Rect rect, float width,
            float height, float angle, float curvature, float sign, float sine, float cosine)
        {
            ProjectColumn(u, angle, curvature, out var x, out var depth);
            x = rect.xMin + sign * width * x;
            // Depth changes the top/bottom perspective together. Keeping X
            // orthographic preserves the monotone projection of each edge.
            var scale = 1f - .065f * depth;
            var y = rect.center.y + height * .012f * sine * u * (1f - u);
            if (isShadow)
            {
                // Attach the shadow to the hinge and keep the same silhouette.
                x += sign * Mathf.Sign(cosine) * width * .014f * sine * u;
                y -= height * .012f * sine * u;
            }
            y += (top ? 1f : -1f) * height * .5f * scale;
            return new Vector3(x, y, 0f);
        }

        private static void ProjectColumn(float u, float angle, float curvature,
            out float x, out float depth)
        {
            // Integrate the unit tangent of a cylindrical arc. Unlike a sine
            // displacement, this preserves an attached, coherent paper surface.
            // The sinc identity avoids subtracting nearly equal sine values:
            // that subtraction loses whole strip widths in float precision when
            // the sheet is almost edge-on, even though its topology is valid.
            var halfArc = curvature * u * .5f;
            var sinc = Mathf.Abs(halfArc) < .001f
                ? 1f - halfArc * halfArc / 6f
                : Mathf.Sin(halfArc) / halfArc;
            var middleAngle = angle + curvature * (u - 1f) * .5f;
            x = u * sinc * Mathf.Cos(middleAngle);
            depth = u * sinc * Mathf.Sin(middleAngle);
        }
    }
}
