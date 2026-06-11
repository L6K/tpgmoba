using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// エニグマ・コア用のプロシージャルメッシュ（クリスタル両錐・リングトーラス）を生成し、
/// アセットとして保存する。エディタ専用ヘルパー。
/// </summary>
public static class ProceduralBossMeshes
{
    private const string ModelDir = "Assets/_Project/Models";

    /// <summary>
    /// 六角両錐（クリスタル本体）を生成して保存する。
    /// 上下の頂点と中間リングからなり、面ごとに頂点を分離してフラットシェーディングにする。
    /// </summary>
    public static Mesh CreateBipyramid(string assetName, float radius, float height, int sides = 6)
    {
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        float halfH    = height * 0.5f;
        var   apexTop  = new Vector3(0f,  halfH, 0f);
        var   apexBot  = new Vector3(0f, -halfH, 0f);

        // 中間リングの座標を先に求めておく
        var ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float ang = (float)i / sides * Mathf.PI * 2f;
            ring[i] = new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
        }

        // 上半分（頂点 → リング）。フラット法線のため面ごとに頂点を複製する
        for (int i = 0; i < sides; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % sides];
            AddTriangle(verts, tris, apexTop, a, b);
        }

        // 下半分（リング → 下頂点）。下向き面なので巻き順を反転する
        for (int i = 0; i < sides; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % sides];
            AddTriangle(verts, tris, apexBot, b, a);
        }

        var mesh = new Mesh { name = assetName };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return SaveMesh(mesh, assetName);
    }

    /// <summary>
    /// リング用トーラスを生成して保存する。
    /// </summary>
    public static Mesh CreateTorus(string assetName, float majorR, float minorR, int majorSeg = 48, int minorSeg = 10)
    {
        var verts   = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris    = new List<int>();

        // 頂点リング: majorSeg+1 × minorSeg+1（継ぎ目のUV/法線連続性のため端を重複させる）
        for (int i = 0; i <= majorSeg; i++)
        {
            float u   = (float)i / majorSeg * Mathf.PI * 2f;
            var   dir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u)); // 主円の中心からの放射方向
            var   center = dir * majorR;

            for (int j = 0; j <= minorSeg; j++)
            {
                float v = (float)j / minorSeg * Mathf.PI * 2f;
                var   normal = dir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                verts.Add(center + normal * minorR);
                normals.Add(normal.normalized);
            }
        }

        int stride = minorSeg + 1;
        for (int i = 0; i < majorSeg; i++)
        {
            for (int j = 0; j < minorSeg; j++)
            {
                int a = i * stride + j;
                int b = (i + 1) * stride + j;
                int c = (i + 1) * stride + (j + 1);
                int d = i * stride + (j + 1);

                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }

        var mesh = new Mesh { name = assetName };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        return SaveMesh(mesh, assetName);
    }

    private static void AddTriangle(List<Vector3> verts, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        int baseIdx = verts.Count;
        verts.Add(v0);
        verts.Add(v1);
        verts.Add(v2);
        tris.Add(baseIdx);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
    }

    private static Mesh SaveMesh(Mesh mesh, string assetName)
    {
        if (!Directory.Exists(ModelDir))
            Directory.CreateDirectory(ModelDir);

        var path     = $"{ModelDir}/{assetName}.asset";
        // 既存があると AddComponent 時の参照が壊れるので作り直す
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
