using System.Collections.Generic;
using UnityEngine;

namespace QCXR.EditorTools.Art
{
    /// Celdas de la paleta. Cada valor es una celda del atlas (8x8). El orden NO debe cambiar
    /// una vez generados los modelos: las UV apuntan al centro de cada celda.
    public enum Sw
    {
        Cream, CreamDark, Orange, OrangeDark, Graphite, GraphiteLight, Steel, SteelDark,
        Chrome, Rubber, Brass, Black, White, RobotBlue, RobotBlueDark, RadioRed,
        RadioRedDark, LampYellow, LampYellowDark, ClockGreen, ClockGreenDark, Wood, Grille, Visor,
        LedCyan, LedGreen, LedRed, LedAmber, Bulb, PcbGreen, PcbTrace, Char,
        Scorch, Ember, PaintRed, HazardYellow, Count
    }

    /// Paleta industrial estilizada: pintura crema y naranja, chasis grafito, acero, goma y LEDs emisivos.
    public static class Palette
    {
        public const int Grid = 8;

        public struct Entry
        {
            public Color albedo, emission;
            public float metallic, smoothness;
            public Entry(string hex, float metallic, float smoothness, string emissionHex = null)
            {
                ColorUtility.TryParseHtmlString(hex, out albedo);
                emission = Color.black;
                if (emissionHex != null) ColorUtility.TryParseHtmlString(emissionHex, out emission);
                this.metallic = metallic; this.smoothness = smoothness;
            }
        }

        public static Entry Get(Sw s)
        {
            switch (s)
            {
                case Sw.Cream:          return new Entry("#EAE2CC", 0f, 0.45f);
                case Sw.CreamDark:      return new Entry("#C9BFA6", 0f, 0.40f);
                case Sw.Orange:         return new Entry("#F08A2C", 0f, 0.50f);
                case Sw.OrangeDark:     return new Entry("#B9601C", 0f, 0.45f);
                case Sw.Graphite:       return new Entry("#34373C", 0.15f, 0.35f);
                case Sw.GraphiteLight:  return new Entry("#555B63", 0.15f, 0.40f);
                case Sw.Steel:          return new Entry("#A9B0B8", 0.85f, 0.55f);
                case Sw.SteelDark:      return new Entry("#6A7179", 0.85f, 0.45f);
                case Sw.Chrome:         return new Entry("#D3D9E0", 0.90f, 0.75f);
                case Sw.Rubber:         return new Entry("#1D1E21", 0f, 0.12f);
                case Sw.Brass:          return new Entry("#D4A23A", 0.90f, 0.60f);
                case Sw.Black:          return new Entry("#111214", 0f, 0.30f);
                case Sw.White:          return new Entry("#F6F4EE", 0f, 0.50f);
                case Sw.RobotBlue:      return new Entry("#3F7FE6", 0f, 0.50f);
                case Sw.RobotBlueDark:  return new Entry("#2A57A6", 0f, 0.45f);
                case Sw.RadioRed:       return new Entry("#E2582B", 0f, 0.50f);
                case Sw.RadioRedDark:   return new Entry("#A63A1A", 0f, 0.45f);
                case Sw.LampYellow:     return new Entry("#FFD447", 0f, 0.50f);
                case Sw.LampYellowDark: return new Entry("#C99A1E", 0f, 0.45f);
                case Sw.ClockGreen:     return new Entry("#4CBF66", 0f, 0.50f);
                case Sw.ClockGreenDark: return new Entry("#2E8A47", 0f, 0.45f);
                case Sw.Wood:           return new Entry("#8C5A2E", 0f, 0.30f);
                case Sw.Grille:         return new Entry("#26272B", 0.3f, 0.30f);
                case Sw.Visor:          return new Entry("#1A2433", 0.2f, 0.85f);
                case Sw.LedCyan:        return new Entry("#7FF3FF", 0f, 0.80f, "#3FD9FF");
                case Sw.LedGreen:       return new Entry("#8CFF9E", 0f, 0.80f, "#3CFF6A");
                case Sw.LedRed:         return new Entry("#FF6B5E", 0f, 0.80f, "#FF2A1A");
                case Sw.LedAmber:       return new Entry("#FFC266", 0f, 0.80f, "#FF9A1A");
                case Sw.Bulb:           return new Entry("#FFF4C2", 0f, 0.80f, "#FFE58A");
                case Sw.PcbGreen:       return new Entry("#2E7D4F", 0f, 0.55f);
                case Sw.PcbTrace:       return new Entry("#C9A44A", 0.8f, 0.55f);
                case Sw.Char:           return new Entry("#0D0B0A", 0f, 0.05f);
                case Sw.Scorch:         return new Entry("#3B2A1E", 0f, 0.10f);
                case Sw.Ember:          return new Entry("#FF6A1F", 0f, 0.30f, "#E0400A");
                case Sw.PaintRed:       return new Entry("#D8322A", 0f, 0.45f);
                case Sw.HazardYellow:   return new Entry("#F5C518", 0f, 0.45f);
                default:                return new Entry("#FF00FF", 0f, 0.5f);
            }
        }

        public static Vector2 UV(Sw s)
        {
            int i = (int)s;
            return new Vector2((i % Grid + 0.5f) / Grid, (i / Grid + 0.5f) / Grid);
        }
    }

    /// Constructor de mallas low-poly con sombreado facetado (chaflanes visibles).
    /// Todas las piezas se combinan en una sola malla por objeto para reducir draw calls.
    public class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        /// Transformación base aplicada a todo lo que se agregue (útil para sub-ensamblajes).
        public Matrix4x4 Base = Matrix4x4.identity;

        public int VertexCount => verts.Count;
        public int TriangleCount => tris.Count / 3;

        static Matrix4x4 TRS(Vector3 c, Vector3 euler) => Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);

        // ───────── Primitivas de bajo nivel ─────────

        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector2 uv, Vector3 outward)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(cross, outward) < 0f) { (b, c) = (c, b); (nb, nc) = (nc, nb); }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(na); norms.Add(nb); norms.Add(nc);
            uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// Polígono convexo plano (coordenadas locales + matriz). outward = hacia dónde mira la cara.
        public void Poly(IList<Vector3> pts, Sw s, Matrix4x4 m, Vector3 outward)
        {
            var M = Base * m;
            var w = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) w[i] = M.MultiplyPoint3x4(pts[i]);
            Vector3 o = M.MultiplyVector(outward).normalized;
            Vector3 nrm = Vector3.zero;
            for (int i = 1; i < w.Length - 1; i++) nrm += Vector3.Cross(w[i] - w[0], w[i + 1] - w[0]);
            if (Vector3.Dot(nrm, o) < 0f) nrm = -nrm;
            nrm = nrm.sqrMagnitude > 0 ? nrm.normalized : o;
            var uv = Palette.UV(s);
            for (int i = 1; i < w.Length - 1; i++) Tri(w[0], w[i], w[i + 1], nrm, nrm, nrm, uv, nrm);
        }

        // ───────── Caja con chaflán ─────────

        public void Box(Vector3 c, Vector3 size, float bevel, Sw s, Vector3 euler = default)
            => Box(TRS(c, euler), size, bevel, s);

        public void Box(Matrix4x4 m, Vector3 size, float bevel, Sw s)
        {
            Vector3 h = size * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            if (b <= 1e-5f)
            {
                for (int a = 0; a < 3; a++)
                    for (int sg = -1; sg <= 1; sg += 2) Face(m, h, a, sg, 0f, s);
                return;
            }
            Vector3 inner = new Vector3(h.x - b, h.y - b, h.z - b);
            // 6 caras
            for (int a = 0; a < 3; a++)
                for (int sg = -1; sg <= 1; sg += 2) Face(m, h, a, sg, b, s);
            // 12 aristas
            for (int a = 0; a < 3; a++)
            {
                int a2 = (a + 1) % 3, c3 = (a + 2) % 3;
                for (int sa = -1; sa <= 1; sa += 2)
                    for (int sb = -1; sb <= 1; sb += 2)
                    {
                        var p = new Vector3[4];
                        for (int k = 0; k < 2; k++)
                        {
                            float cc = (k == 0 ? -1 : 1) * inner[c3];
                            var p1 = Vector3.zero; p1[a] = sa * h[a]; p1[a2] = sb * inner[a2]; p1[c3] = cc;
                            var p2 = Vector3.zero; p2[a] = sa * inner[a]; p2[a2] = sb * h[a2]; p2[c3] = cc;
                            p[k == 0 ? 0 : 1] = p1; p[k == 0 ? 3 : 2] = p2;
                        }
                        var o = Vector3.zero; o[a] = sa; o[a2] = sb;
                        Poly(p, s, m, o);
                    }
            }
            // 8 esquinas
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var p = new[]
                        {
                            new Vector3(sx * h.x, sy * inner.y, sz * inner.z),
                            new Vector3(sx * inner.x, sy * h.y, sz * inner.z),
                            new Vector3(sx * inner.x, sy * inner.y, sz * h.z)
                        };
                        Poly(p, s, m, new Vector3(sx, sy, sz));
                    }
        }

        void Face(Matrix4x4 m, Vector3 h, int axis, int sign, float b, Sw s)
        {
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            var p = new Vector3[4];
            float[] su = { -1, 1, 1, -1 }, sv = { -1, -1, 1, 1 };
            for (int i = 0; i < 4; i++)
            {
                var q = Vector3.zero;
                q[axis] = sign * h[axis];
                q[u] = su[i] * (h[u] - b);
                q[v] = sv[i] * (h[v] - b);
                p[i] = q;
            }
            var o = Vector3.zero; o[axis] = sign;
            Poly(p, s, m, o);
        }

        // ───────── Revolución (cilindros, cúpulas, conos, aros) ─────────

        /// Perfil (r, y) recorrido de abajo-centro hacia arriba-centro. smooth = normales suaves alrededor del eje.
        public void Lathe(IList<Vector2> prof, Matrix4x4 m, Sw s, int seg = 16, bool smooth = true)
        {
            var M = Base * m;
            var uv = Palette.UV(s);
            for (int i = 0; i < prof.Count - 1; i++)
            {
                Vector2 p0 = prof[i], p1 = prof[i + 1];
                Vector2 t = p1 - p0;
                if (t.sqrMagnitude < 1e-12f) continue;
                Vector2 n2 = new Vector2(t.y, -t.x).normalized;
                for (int j = 0; j < seg; j++)
                {
                    float a0 = j * Mathf.PI * 2f / seg, a1 = (j + 1) * Mathf.PI * 2f / seg;
                    Vector3 A = P(p0, a0), B = P(p0, a1), C = P(p1, a1), D = P(p1, a0);
                    Vector3 nA = N(n2, a0), nB = N(n2, a1);
                    float am = (a0 + a1) * 0.5f;
                    Vector3 nm = N(n2, am);
                    A = M.MultiplyPoint3x4(A); B = M.MultiplyPoint3x4(B); C = M.MultiplyPoint3x4(C); D = M.MultiplyPoint3x4(D);
                    nA = M.MultiplyVector(nA).normalized; nB = M.MultiplyVector(nB).normalized; nm = M.MultiplyVector(nm).normalized;
                    if (!smooth) { nA = nB = nm; }
                    Tri(A, B, C, nA, nB, nB, uv, nm);
                    Tri(A, C, D, nA, nB, nA, uv, nm);
                }
            }
        }

        static Vector3 P(Vector2 p, float a) => new Vector3(p.x * Mathf.Cos(a), p.y, p.x * Mathf.Sin(a));
        static Vector3 N(Vector2 n, float a) => new Vector3(n.x * Mathf.Cos(a), n.y, n.x * Mathf.Sin(a)).normalized;

        public static Vector2[] CylProfile(float r, float h, float bevel)
        {
            float y0 = -h / 2, y1 = h / 2;
            if (bevel <= 0f) return new[] { new Vector2(0, y0), new Vector2(r, y0), new Vector2(r, y1), new Vector2(0, y1) };
            bevel = Mathf.Min(bevel, Mathf.Min(r, h / 2) * 0.95f);
            return new[]
            {
                new Vector2(0, y0), new Vector2(r - bevel, y0), new Vector2(r, y0 + bevel),
                new Vector2(r, y1 - bevel), new Vector2(r - bevel, y1), new Vector2(0, y1)
            };
        }

        /// Cilindro con eje Y local (rotar con euler).
        public void Cyl(Vector3 c, float r, float h, Sw s, Vector3 euler = default, int seg = 16, float bevel = 0f, bool smooth = true)
            => Lathe(CylProfile(r, h, bevel), TRS(c, euler), s, seg, smooth);

        /// Cilindro entre dos puntos.
        public void Tube(Vector3 a, Vector3 b, float r, Sw s, int seg = 10, float bevel = 0f)
        {
            Vector3 d = b - a;
            var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
            Lathe(CylProfile(r, d.magnitude, bevel), Matrix4x4.TRS((a + b) * 0.5f, rot, Vector3.one), s, seg);
        }

        public void Sphere(Vector3 c, float r, Sw s, int seg = 12, int rings = 6)
        {
            var prof = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI / 2 + Mathf.PI * i / rings;
                prof[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            prof[0].x = 0; prof[rings].x = 0;
            Lathe(prof, TRS(c, Vector3.zero), s, seg);
        }

        /// Aro (toroide) con eje Y local.
        public void Ring(Vector3 c, float radius, float thickness, Sw s, Vector3 euler = default, int seg = 20, int sides = 6)
        {
            // Se construye como lathe de un círculo pequeño desplazado (perfil cerrado).
            var prof = new List<Vector2>();
            for (int i = 0; i <= sides; i++)
            {
                float a = -Mathf.PI / 2 - Mathf.PI * 2f * i / sides;   // horario => recorrido abajo->exterior->arriba->interior
                prof.Add(new Vector2(radius + Mathf.Cos(a) * thickness * -1f, Mathf.Sin(a) * thickness));
            }
            Lathe(prof, TRS(c, euler), s, seg);
        }

        // ───────── Detalles industriales ─────────

        /// Tornillo de cabeza redonda asentado (base en y=0, eje +Y local).
        public void ScrewHead(Matrix4x4 m, Sw head = Sw.Steel, Sw slot = Sw.Graphite, float r = 0.011f)
        {
            float k = r / 0.011f;
            var prof = new[]
            {
                new Vector2(0, 0), new Vector2(r, 0), new Vector2(r, 0.0018f * k),
                new Vector2(0.0085f * k, 0.0042f * k), new Vector2(0.0045f * k, 0.0058f * k), new Vector2(0, 0.0062f * k)
            };
            Lathe(prof, m, head, 12);
            Box(m * TRS(new Vector3(0, 0.0058f * k, 0), Vector3.zero), new Vector3(0.015f * k, 0.0016f * k, 0.0026f * k), 0f, slot);
            Box(m * TRS(new Vector3(0, 0.0058f * k, 0), new Vector3(0, 90, 0)), new Vector3(0.015f * k, 0.0016f * k, 0.0026f * k), 0f, slot);
        }

        public void ScrewHead(Vector3 c, Vector3 euler, float r = 0.011f) => ScrewHead(TRS(c, euler), Sw.Steel, Sw.Graphite, r);

        /// Remache simple (cúpula baja).
        public void Rivet(Vector3 c, Vector3 euler, float r = 0.0035f, Sw s = Sw.Steel)
        {
            var prof = new[] { new Vector2(0, 0), new Vector2(r, 0), new Vector2(r * 0.85f, r * 0.45f), new Vector2(r * 0.45f, r * 0.8f), new Vector2(0, r * 0.9f) };
            Lathe(prof, TRS(c, euler), s, 8);
        }

        /// Franja de peligro en el plano XY local (mira hacia -Z local). Paralelogramos alternos.
        public void Stripes(Vector3 c, Vector3 euler, float width, float height, int count, Sw a, Sw b, float slant = 0.6f)
        {
            var m = TRS(c, euler);
            float x0 = -width / 2, x1 = width / 2, step = width / count, sh = height * slant;
            for (int i = -1; i <= count; i++)
            {
                float xa = x0 + i * step, xb = xa + step;
                var p = new[]
                {
                    new Vector3(Mathf.Clamp(xa, x0, x1), -height / 2, 0),
                    new Vector3(Mathf.Clamp(xb, x0, x1), -height / 2, 0),
                    new Vector3(Mathf.Clamp(xb + sh, x0, x1), height / 2, 0),
                    new Vector3(Mathf.Clamp(xa + sh, x0, x1), height / 2, 0)
                };
                if (Mathf.Abs(p[1].x - p[0].x) + Mathf.Abs(p[2].x - p[3].x) < 1e-5f) continue;
                Poly(p, ((i % 2) + 2) % 2 == 0 ? a : b, m, Vector3.back);
            }
        }

        /// Rejilla de ranuras (ventilación) en el plano XY local, mirando a -Z local.
        public void Vents(Vector3 c, Vector3 euler, float width, float height, int count, Sw slot, float depth = 0.002f)
        {
            var m = TRS(c, euler);
            float step = height / count;
            for (int i = 0; i < count; i++)
            {
                float y = -height / 2 + step * (i + 0.5f);
                Box(m * TRS(new Vector3(0, y, -depth / 2), Vector3.zero), new Vector3(width, step * 0.45f, depth), 0f, slot);
            }
        }

        /// Cinta plana (polilínea) sobre el plano XZ local a altura y. Para grietas y trazas.
        public void Ribbon(IList<Vector2> pts, float y, float width0, float width1, Sw s, Matrix4x4 m)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float t0 = (float)i / (pts.Count - 1), t1 = (float)(i + 1) / (pts.Count - 1);
                float w0 = Mathf.Lerp(width0, width1, t0) / 2, w1 = Mathf.Lerp(width0, width1, t1) / 2;
                Vector2 d = (pts[i + 1] - pts[i]).normalized;
                Vector2 nn = new Vector2(-d.y, d.x);
                var p = new[]
                {
                    new Vector3(pts[i].x + nn.x * w0, y, pts[i].y + nn.y * w0),
                    new Vector3(pts[i + 1].x + nn.x * w1, y, pts[i + 1].y + nn.y * w1),
                    new Vector3(pts[i + 1].x - nn.x * w1, y, pts[i + 1].y - nn.y * w1),
                    new Vector3(pts[i].x - nn.x * w0, y, pts[i].y - nn.y * w0)
                };
                Poly(p, s, m, Vector3.up);
            }
        }

        /// Mancha irregular plana (polígono estrellado) en el plano XZ local.
        public void Blotch(Vector3 c, float radius, int points, float jitter, int seed, Sw s, Matrix4x4 m)
        {
            var rnd = new System.Random(seed);
            var mm = m * TRS(c, Vector3.zero);
            var ring = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float a = Mathf.PI * 2f * i / points;
                float r = radius * (1f - jitter * 0.5f + (float)rnd.NextDouble() * jitter);
                ring[i] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < points; i++)
                Poly(new[] { Vector3.zero, ring[i], ring[(i + 1) % points] }, s, mm, Vector3.up);
        }

        // ───────── Salida ─────────

        public void Append(MeshKit other)
        {
            int o = verts.Count;
            verts.AddRange(other.verts); norms.AddRange(other.norms); uvs.AddRange(other.uvs);
            foreach (var t in other.tris) tris.Add(t + o);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            Fill(mesh);
            return mesh;
        }

        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
