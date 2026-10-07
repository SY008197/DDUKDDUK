using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DdukDduk.Shop
{
    /// <summary>A small folded parchment corner, with an ink arrow instead of a separate UI card.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecipePageCornerGraphic : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler
    {
        public int Direction = 1;
        private float lift;
        private bool hovered;

        protected override void OnEnable()
        {
            // Repair book controls serialized before the renderer dependency was declared.
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; }

        private void Update()
        {
            var button = GetComponent<Button>();
            var target = hovered && button != null && button.IsInteractable() ? 1f : 0f;
            var next = Mathf.MoveTowards(lift, target, Time.unscaledDeltaTime * 7);
            if (Mathf.Approximately(next, lift)) return;
            lift = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            // Fold rises very slightly when touched; all geometry stays within its page corner.
            var x = r.width * (.12f - .05f * lift);
            var y = r.height * (.10f - .04f * lift);
            var a = new Vector2(x, r.height - y);
            var b = new Vector2(r.width - x, y);
            var c = new Vector2(r.width - x, r.height - y);
            var crease = new Vector2(r.width * .56f, r.height * .53f);
            Triangle(mesh, a + new Vector2(-3, -1), b + new Vector2(-3, -1), c,
                new Color(.22f, .13f, .06f, .20f * color.a));
            Triangle(mesh, a, b, crease, Tint(new Color(.86f, .70f, .47f)));
            Triangle(mesh, a, crease, c, Tint(new Color(.98f, .88f, .69f)));
            Triangle(mesh, crease, b, c, Tint(new Color(.93f, .80f, .59f)));
            Stroke(mesh, a, b, 1.1f, Tint(new Color(.55f, .37f, .19f, .5f)));
            var ink = Tint(new Color(.36f, .25f, .14f, .76f));
            var middle = new Vector2(r.width * .68f, r.height * .71f);
            Stroke(mesh, middle + new Vector2(-7, -8), middle + new Vector2(3, 0), 2.2f, ink);
            Stroke(mesh, middle + new Vector2(3, 0), middle + new Vector2(-7, 8), 2.2f, ink);
        }

        private Color Tint(Color tint)
        {
            return new Color(tint.r * color.r, tint.g * color.g, tint.b * color.b, tint.a * color.a);
        }

        private Vector3 Position(Vector2 point)
        {
            var r = rectTransform.rect;
            return new Vector3(r.xMin + (Direction < 0 ? r.width - point.x : point.x), r.yMax - point.y, 0);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(Position(a), tint, Vector2.zero);
            mesh.AddVert(Position(b), tint, Vector2.zero);
            mesh.AddVert(Position(c), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * width * .5f;
            Triangle(mesh, a - n, a + n, b + n, tint);
            Triangle(mesh, b + n, b - n, a - n, tint);
        }
    }
}
