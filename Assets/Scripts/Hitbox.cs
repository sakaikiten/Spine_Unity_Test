using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hitbox : MonoBehaviour {
    public int damage = 10;
    public bool active = false;  // 攻撃フレームだけ true
    private void Reset(){
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("Hitbox");
    }

    void OnTriggerEnter2D(Collider2D other)
    {

        if (!active) return;
        var hb = other.GetComponent<Hurtbox>();
        if (hb == null || hb.owner == null) return;
        hb.owner.TakeDamage(damage);
        // 多段ヒット防止や同一フレーム複数当たりの扱いは後で
    }
    
 // ===== ここから可視化 =====
    #if UNITY_EDITOR
        void OnDrawGizmos() {
            var col = GetComponent<Collider2D>();
            if (!col) return;

            Color c = active ? new Color(1f, 0f, 0f, 0.95f) : new Color(0f, 1f, 0f, 0.45f);
            Gizmos.color = c;

            // ローカル→ワールド行列
            var m = transform.localToWorldMatrix;
            Gizmos.matrix = m;

            if (col is BoxCollider2D box) {
                Vector3 size = new Vector3(box.size.x, box.size.y, 0f);
                Vector3 center = (Vector3)box.offset;
                Gizmos.DrawWireCube(center, size);
            } else if (col is CircleCollider2D circle) {
                DrawWireCircle(circle.offset, circle.radius, 28);
            } else if (col is CapsuleCollider2D cap) {
                DrawWireCapsule2D(cap);
            } else if (col is PolygonCollider2D poly) {
                DrawWirePolygon(poly);
            } else if (col is CompositeCollider2D comp) {
                DrawWireComposite(comp);
            }

            // 行列戻す
            Gizmos.matrix = Matrix4x4.identity;
        }

        void DrawWireCircle(Vector2 center, float radius, int segments) {
            Vector3 prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++) {
                float t = (float)i / segments * Mathf.PI * 2f;
                Vector3 p = center + new Vector2(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        void DrawWireCapsule2D(CapsuleCollider2D cap) {
            // カプセルを12角形相当で近似
            int seg = 10;
            float rx, ry;
            Vector2 center = cap.offset;
            if (cap.direction == CapsuleDirection2D.Vertical) {
                rx = cap.size.x * 0.5f; ry = cap.size.y * 0.5f;
                float straight = Mathf.Max(0f, (cap.size.y - cap.size.x));
                Vector2 topCenter = center + new Vector2(0f, straight * 0.5f);
                Vector2 botCenter = center - new Vector2(0f, straight * 0.5f);
                // 上半円
                Vector3 prev = topCenter + new Vector2(rx, 0f);
                for (int i = 1; i <= seg; i++) {
                    float t = Mathf.PI * i / seg;
                    Vector3 p = topCenter + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * rx);
                    Gizmos.DrawLine(prev, p); prev = p;
                }
                // 下半円
                prev = botCenter + new Vector2(rx, 0f);
                for (int i = 1; i <= seg; i++) {
                    float t = -Mathf.PI * i / seg;
                    Vector3 p = botCenter + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * rx);
                    Gizmos.DrawLine(prev, p); prev = p;
                }
                // 直線部
                Gizmos.DrawLine(topCenter + new Vector2(rx, 0f), botCenter + new Vector2(rx, 0f));
                Gizmos.DrawLine(topCenter + new Vector2(-rx, 0f), botCenter + new Vector2(-rx, 0f));
            } else {
                ry = cap.size.y * 0.5f; rx = cap.size.x * 0.5f;
                float straight = Mathf.Max(0f, (cap.size.x - cap.size.y));
                Vector2 rightCenter = center + new Vector2(straight * 0.5f, 0f);
                Vector2 leftCenter  = center - new Vector2(straight * 0.5f, 0f);
                // 右半円
                Vector3 prev = rightCenter + new Vector2(0f, ry);
                for (int i = 1; i <= seg; i++) {
                    float t = Mathf.PI * 0.5f - Mathf.PI * i / seg;
                    Vector3 p = rightCenter + new Vector2(Mathf.Cos(t) * ry, Mathf.Sin(t) * ry);
                    Gizmos.DrawLine(prev, p); prev = p;
                }
                // 左半円
                prev = leftCenter + new Vector2(0f, ry);
                for (int i = 1; i <= seg; i++) {
                    float t = Mathf.PI * 0.5f + Mathf.PI * i / seg;
                    Vector3 p = leftCenter + new Vector2(Mathf.Cos(t) * ry, Mathf.Sin(t) * ry);
                    Gizmos.DrawLine(prev, p); prev = p;
                }
                // 直線部
                Gizmos.DrawLine(rightCenter + new Vector2(0f, ry), leftCenter + new Vector2(0f, ry));
                Gizmos.DrawLine(rightCenter + new Vector2(0f, -ry), leftCenter + new Vector2(0f, -ry));
            }
        }

        void DrawWirePolygon(PolygonCollider2D poly) {
            for (int p = 0; p < poly.pathCount; p++) {
                var path = poly.GetPath(p);
                for (int i = 0; i < path.Length; i++) {
                    var a = (Vector3)path[i];
                    var b = (Vector3)path[(i + 1) % path.Length];
                    Gizmos.DrawLine(a, b);
                }
            }
        }

        void DrawWireComposite(CompositeCollider2D comp) {
            int paths = comp.pathCount;
            for (int p = 0; p < paths; p++) {
                int count = comp.GetPathPointCount(p);
                var buf = new Vector2[count];
                comp.GetPath(p, buf);
                for (int i = 0; i < count; i++) {
                    var a = (Vector3)buf[i];
                    var b = (Vector3)buf[(i + 1) % count];
                    Gizmos.DrawLine(a, b);
                }
            }
        }
    #endif

}
