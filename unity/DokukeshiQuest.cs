// ============================================================
//  毒消し草クエスト  ―  Unity 単体で動く 3D RPG デモ (URP対応)
// ------------------------------------------------------------
//  使い方:
//   1. Unityで空のGameObjectを1つ作る (Hierarchyで右クリック → Create Empty)
//   2. このスクリプト(DokukeshiQuest.cs)を Assets にドラッグして入れる
//   3. 空のGameObjectに、このスクリプトをドラッグしてアタッチ
//   4. 再生(Play)ボタンを押す
//   操作: W/S=前後  A/D=旋回  E=話す・調べる  R=リセット
//
//  ※ もしキャラが動かない場合:
//     Edit > Project Settings > Player > Active Input Handling を
//     「Both」または「Input Manager (Old)」にして再生し直してください。
//
//  この世界は「箱」で作った仮組みです。町/洞窟のフィールドを、
//  あなたの3Dフィールド生成アセットで差し替えていけます。
// ============================================================
using System.Collections.Generic;
using UnityEngine;

public class DokukeshiQuest : MonoBehaviour
{
    // ▼ 差し替え用（任意）: あなたのアセットのPrefabをInspectorでドラッグすると、
    //   箱の代わりにそのモデルが同じ位置に置かれます。空なら箱の仮組みのまま。
    [Header("キャラ・アイテム差し替え (任意)")]
    public GameObject playerModel;   // 主人公（Meshyのリギング済みモデル等）
    public GameObject npcModel;      // 村人マレン
    public GameObject herbModel;     // 毒消し草

    [Header("Gaia地形を使う (任意)")]
    public bool useGaiaTerrain = false;  // Gaiaで作った地形(Unity Terrain)の上を歩く
    public bool hideBoxTown = false;     // 箱の町(地面・家・木)を隠してGaia地形だけにする

    [Header("草原マップをコードで生成 (Gaiaが無くても試せる)")]
    public bool generateGrassland = false;  // 起伏のある草原(メッシュ)＋木・花を自動生成

    enum Field { Town, Cave }
    Field field = Field.Town;

    Transform player, townRoot, caveRoot;
    Camera cam;
    Light sun;

    float yaw = Mathf.PI;          // 0 = -Z向き
    int q = 0;                     // 0:話す 1:採取 2:納品へ 3:完了
    bool herb = false;

    static readonly Vector2 NPC       = new Vector2(3, 34);
    static readonly Vector2 HERB      = new Vector2(0, -14);
    static readonly Vector2 TOWN_GATE = new Vector2(0, -30);
    static readonly Vector2 CAVE_EXIT = new Vector2(0, 12);

    readonly List<Vector4> townCols = new List<Vector4>(); // (x,z,hx,hz)
    readonly List<Vector4> caveCols = new List<Vector4>();
    GameObject herbObj, npcMark, groundMeshGO;

    // 場面転換
    bool transitioning = false;
    int  transPhase = 0;           // 1=暗転中 2=明転中
    float fade = 0f;
    Field transTo;
    Vector3 transSpawn;            // (x, z, yaw)

    // UI
    float toastT = 0f; string toastMsg = "";
    float bannerT = 0f;
    string[] dlg = null; int dlgI = 0; System.Action dlgDone = null; string dlgName = "";
    bool eEdge = false;

    Texture2D tex;
    GUIStyle stPanel, stObj, stSmall, stTitle, stDlgName, stDlgText, stPrompt, stToast, stBanner, stBtn, stBig;
    bool stylesReady = false;

    // ---------------------------------------------------------
    void Start()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("GameCamera");
            cam = cg.AddComponent<Camera>();
            cg.tag = "MainCamera";
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 400f;

        sun = FindObjectOfType<Light>();
        if (sun == null)
        {
            var lg = new GameObject("Sun");
            sun = lg.AddComponent<Light>();
        }
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        townRoot = new GameObject("TownField").transform;
        caveRoot = new GameObject("CaveField").transform;
        if (generateGrassland) { useGaiaTerrain = true; hideBoxTown = true; BuildGrassland(); }
        BuildTown();
        BuildCave();
        BuildPlayer();

        tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white); tex.Apply();

        EnterField(Field.Town, new Vector3(0, 24, Mathf.PI), false);
    }

    // ---------------------------------------------------------
    void Update()
    {
        float dt = Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)) eEdge = true;
        if (Input.GetKeyDown(KeyCode.R)) ResetGame();

        bool busy = dlg != null || q == 3 || transitioning;
        Vector3 pos = player.position;

        if (!busy)
        {
            float turn = 2.2f * dt;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) yaw -= turn;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) yaw += turn;

            float spd = 7.5f * dt, mv = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) mv = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) mv = -1f;
            if (mv != 0f)
            {
                float fx = Mathf.Sin(yaw) * spd * mv, fz = -Mathf.Cos(yaw) * spd * mv;
                if (!Collides(pos.x + fx, pos.z)) pos.x += fx;
                if (!Collides(pos.x, pos.z + fz)) pos.z += fz;
                player.position = pos;
            }

            if (field == Field.Town && pos.z < -32f && Mathf.Abs(pos.x) < 3.5f)
                BeginTransition(Field.Cave, new Vector3(0, 10, 0));
            else if (field == Field.Cave && pos.z > 11.5f && Mathf.Abs(pos.x) < 3.5f)
                BeginTransition(Field.Town, new Vector3(0, -27, Mathf.PI));
        }

        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
        player.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        // Gaia地形の高さに追従（キャラを地形の上に乗せる）
        {
            Vector3 gp = player.position;
            gp.y = GroundY(gp.x, gp.z);
            player.position = gp;
        }

        if (eEdge) { eEdge = false; Interact(); }

        if (field == Field.Cave && q == 1 && !herb && Dist(HERB) < 2.4f)
        {
            herb = true; q = 2;
            if (herbObj) herbObj.SetActive(false);
            Toast("毒消し草を手に入れた！");
        }

        // クエストマーカーの表示切替
        if (npcMark) npcMark.SetActive(field == Field.Town && (q == 0 || (q == 2 && herb)));

        // 場面転換フェード
        if (transitioning)
        {
            fade += dt / 0.35f * (transPhase == 1 ? 1f : -1f);
            if (transPhase == 1 && fade >= 1f) { fade = 1f; DoEnter(); transPhase = 2; }
            else if (transPhase == 2 && fade <= 0f) { fade = 0f; transitioning = false; }
        }

        if (toastT > 0f) toastT -= dt;
        if (bannerT > 0f) bannerT -= dt;
    }

    void LateUpdate()
    {
        if (player == null || cam == null) return;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
        Vector3 p = player.position;
        cam.transform.position = p - fwd * 9f + Vector3.up * 6.2f;
        cam.transform.LookAt(p + fwd * 2f + Vector3.up * 1.6f);
    }

    // ---------------------------------------------------------
    void BeginTransition(Field to, Vector3 spawn)
    {
        if (transitioning) return;
        transitioning = true; transPhase = 1; fade = 0f; transTo = to; transSpawn = spawn;
    }
    void DoEnter() { EnterField(transTo, transSpawn, true); }

    void EnterField(Field f, Vector3 spawn, bool banner)
    {
        field = f;
        townRoot.gameObject.SetActive(f == Field.Town);
        caveRoot.gameObject.SetActive(f == Field.Cave);
        player.position = new Vector3(spawn.x, 0f, spawn.y);
        yaw = spawn.z;

        if (f == Field.Town)
        {
            cam.backgroundColor = H("#9ecbe6");
            RenderSettings.ambientLight = H("#8fa6b8");
            sun.intensity = 1.15f;
        }
        else
        {
            cam.backgroundColor = H("#080a12");
            RenderSettings.ambientLight = H("#1b2230");
            sun.intensity = 0.3f;
        }
        if (banner) bannerT = 1.6f;
    }

    void ResetGame()
    {
        q = 0; herb = false;
        if (herbObj) herbObj.SetActive(true);
        transitioning = false; fade = 0f; dlg = null;
        EnterField(Field.Town, new Vector3(0, 24, Mathf.PI), false);
        Toast("はじめから");
    }

    // ---------------------------------------------------------
    void Interact()
    {
        if (dlg != null) { DlgAdvance(); return; }
        if (field == Field.Town && q == 0 && Dist(NPC) < 3.6f)
            Say("村人 マレン", new[] {
                "旅の方、ちょうどよかった。",
                "村の子が熱を出して、毒消し草がいるんだ。",
                "村の北にある洞窟に生えている。採ってきてくれないか？"
            }, () => { q = 1; Toast("クエスト受注"); });
        else if (field == Field.Town && q == 2 && herb && Dist(NPC) < 3.6f)
            Say("村人 マレン", new[] {
                "おお、それが毒消し草か！",
                "これで子どもも助かる。本当にありがとう。",
                "受け取ってくれ、ほんの礼だ。"
            }, () => { q = 3; });
    }

    Vector2 CurrentTarget()
    {
        if (field == Field.Town) return q >= 2 ? NPC : TOWN_GATE;
        return (q <= 1 && !herb) ? HERB : CAVE_EXIT;
    }

    void Say(string name, string[] lines, System.Action done)
    { dlgName = name; dlg = lines; dlgI = 0; dlgDone = done; }
    void DlgAdvance()
    {
        dlgI++;
        if (dlgI >= dlg.Length) { var d = dlgDone; dlg = null; dlgDone = null; if (d != null) d(); }
    }
    void Toast(string m) { toastMsg = m; toastT = 1.6f; }

    // ---------------------------------------------------------
    float Dist(Vector2 t)
    { return Vector2.Distance(new Vector2(player.position.x, player.position.z), t); }

    bool Collides(float x, float z)
    {
        var list = field == Field.Town ? townCols : caveCols;
        foreach (var c in list)
            if (Mathf.Abs(x - c.x) < c.z + 0.6f && Mathf.Abs(z - c.y) < c.w + 0.6f) return true;
        return false;
    }

    // 町フィールドの地面の高さを返す。Gaia地形(Terrain)があればそれを、
    // 無ければ生成した草原メッシュにレイキャストして高さを取る。
    float GroundY(float x, float z)
    {
        if (field == Field.Town)
        {
            var ter = Terrain.activeTerrain;
            if (useGaiaTerrain && ter != null)
                return ter.SampleHeight(new Vector3(x, 0, z)) + ter.transform.position.y;
            if (groundMeshGO != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out hit, 300f))
                    return hit.point.y;
            }
        }
        return 0f;
    }

    // ---------------------------------------------------------  builders
    static Color H(string h)
    {
        return new Color(
            System.Convert.ToInt32(h.Substring(1, 2), 16) / 255f,
            System.Convert.ToInt32(h.Substring(3, 2), 16) / 255f,
            System.Convert.ToInt32(h.Substring(5, 2), 16) / 255f);
    }
    static void SetCol(GameObject g, Color c, bool emit)
    {
        var m = g.GetComponent<Renderer>().material;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (emit)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c * 1.6f);
        }
    }
    GameObject Box(Transform parent, Vector3 p, Vector3 s, Color c, float rotY = 0f, bool emit = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = p;
        g.transform.localRotation = Quaternion.Euler(0f, rotY * Mathf.Rad2Deg, 0f);
        g.transform.localScale = s;
        Destroy(g.GetComponent<Collider>());     // 物理は使わず自前判定
        SetCol(g, c, emit);
        return g;
    }

    void BuildTown()
    {
        if (!hideBoxTown)   // Gaia地形を使うときは箱の町を隠せる
        {
            Box(townRoot, new Vector3(0, -0.15f, -6), new Vector3(160, 0.3f, 160), H("#6cae5a"));
            for (int z = 32; z >= -28; z -= 4)
                Box(townRoot, new Vector3(0, 0.02f, z), new Vector3(5.2f, 0.05f, 4.2f), H("#d8c48f"));

            AddHouse(-10, 30, 0.2f, "#d9a066", "#5b6b8a");
            AddHouse(10, 32, 0.5f, "#c9b28a", "#6b5b8a");
            AddHouse(-14, 22, -0.3f, "#cf8b5c", "#4f6b7a");
            AddHouse(13, 24, 0.4f, "#d9c48a", "#5b7a6b");
            AddHouse(-8, 16, 0.1f, "#c98f6a", "#6b6b8a");

            Box(townRoot, new Vector3(-2, 0.5f, 26), new Vector3(1.6f, 1, 1.6f), H("#8a8f99"));

            AddTree(-18, 10); AddTree(16, 14); AddTree(-20, -2); AddTree(19, -4);
            AddTree(-6, 2); AddTree(8, -8); AddTree(-16, -16); AddTree(15, -18);
        }

        // 北の岩壁 + 洞口 (洞窟フィールドへの接続点)
        Box(townRoot, new Vector3(-11, 7, -42), new Vector3(16, 16, 14), H("#4b4a56"));
        Box(townRoot, new Vector3(11, 7, -42), new Vector3(16, 16, 14), H("#4b4a56"));
        Box(townRoot, new Vector3(0, 12, -42), new Vector3(10, 8, 14), H("#40404c"));
        Box(townRoot, new Vector3(0, 3.6f, -34), new Vector3(7, 7.2f, 2), H("#0a0b10"));
        Box(townRoot, new Vector3(0, 0.06f, -31), new Vector3(5, 0.05f, 3), H("#3a4a55"), 0f, true);
        townCols.Add(new Vector4(-10, -42, 8, 7));
        townCols.Add(new Vector4(10, -42, 8, 7));

        // NPC（Prefabがあれば差し替え／Gaia地形なら地形の高さに乗せる）
        float ny = GroundY(NPC.x, NPC.y);
        if (npcModel != null)
        {
            var v = Instantiate(npcModel, townRoot);
            v.transform.position = new Vector3(NPC.x, ny, NPC.y);
        }
        else
        {
            Box(townRoot, new Vector3(NPC.x, ny + 1.0f, NPC.y), new Vector3(1.0f, 2.0f, 0.8f), H("#5a78c8"));
            Box(townRoot, new Vector3(NPC.x, ny + 2.5f, NPC.y), new Vector3(0.9f, 0.9f, 0.9f), H("#e6c9a8"));
            Box(townRoot, new Vector3(NPC.x, ny + 3.05f, NPC.y), new Vector3(1.0f, 0.35f, 1.0f), H("#3a4a7a"));
        }
        npcMark = new GameObject("QuestMark");
        npcMark.transform.SetParent(townRoot, false);
        Box(npcMark.transform, new Vector3(NPC.x, ny + 4.4f, NPC.y), new Vector3(0.22f, 0.7f, 0.22f), H("#ffd774"), 0f, true);
        Box(npcMark.transform, new Vector3(NPC.x, ny + 3.85f, NPC.y), new Vector3(0.22f, 0.22f, 0.22f), H("#ffd774"), 0f, true);
    }
    void AddHouse(float x, float z, float r, string body, string roof)
    {
        Box(townRoot, new Vector3(x, 1.6f, z), new Vector3(4, 3.2f, 4), H(body), r);
        Box(townRoot, new Vector3(x, 3.6f, z), new Vector3(4.6f, 1.1f, 4.6f), H(roof), r);
        townCols.Add(new Vector4(x, z, 2.4f, 2.4f));
    }
    void AddTree(float x, float z)
    {
        Box(townRoot, new Vector3(x, 1.1f, z), new Vector3(0.5f, 2.2f, 0.5f), H("#7a5a3a"));
        Box(townRoot, new Vector3(x, 3.0f, z), new Vector3(2.4f, 2.2f, 2.4f), H("#4e8f4a"));
        Box(townRoot, new Vector3(x, 4.4f, z), new Vector3(1.5f, 1.6f, 1.5f), H("#5aa356"));
        townCols.Add(new Vector4(x, z, 0.5f, 0.5f));
    }

    void BuildCave()
    {
        Box(caveRoot, new Vector3(0, -0.15f, -3), new Vector3(60, 0.3f, 60), H("#2a2730"));
        Box(caveRoot, new Vector3(-11, 5, -3), new Vector3(2, 12, 44), H("#211f28"));
        Box(caveRoot, new Vector3(11, 5, -3), new Vector3(2, 12, 44), H("#211f28"));
        Box(caveRoot, new Vector3(0, 5, -19), new Vector3(24, 12, 2), H("#1c1a24"));
        Box(caveRoot, new Vector3(-8, 5, 13), new Vector3(10, 12, 2), H("#211f28"));
        Box(caveRoot, new Vector3(8, 5, 13), new Vector3(10, 12, 2), H("#211f28"));
        Box(caveRoot, new Vector3(0, 3.2f, 13.3f), new Vector3(6, 6.4f, 1.0f), H("#8fd4ff"), 0f, true); // 出口の光

        AddCrystal(-8, -6, "#59d0ff"); AddCrystal(7, -10, "#b98bff");
        AddCrystal(-6, -15, "#59ffcf"); AddCrystal(8, 3, "#ff9bd0"); AddCrystal(-9, 6, "#8bd0ff");

        AddStalagmite(-4, -4); AddStalagmite(5, -8); AddStalagmite(-6, -11);
        AddStalagmite(4, 2); AddStalagmite(6, -14);

        caveCols.Add(new Vector4(-11, -3, 1, 22));
        caveCols.Add(new Vector4(11, -3, 1, 22));
        caveCols.Add(new Vector4(0, -19, 12, 1));
        caveCols.Add(new Vector4(-8, 13, 5, 1));
        caveCols.Add(new Vector4(8, 13, 5, 1));

        // 毒消し草 (発光)
        herbObj = new GameObject("Herb");
        herbObj.transform.SetParent(caveRoot, false);
        if (herbModel != null)
        {
            var v = Instantiate(herbModel, herbObj.transform);
            v.transform.position = new Vector3(HERB.x, 0f, HERB.y);
        }
        else
        {
            Box(herbObj.transform, new Vector3(HERB.x, 0.16f, HERB.y), new Vector3(2.4f, 0.06f, 2.4f), H("#2f6b45"), 0f, true);
            Box(herbObj.transform, new Vector3(HERB.x, 1.0f, HERB.y), new Vector3(0.16f, 1.1f, 0.16f), H("#3e9a52"));
            Box(herbObj.transform, new Vector3(HERB.x, 1.7f, HERB.y), new Vector3(1.0f, 0.5f, 1.0f), H("#6cf08a"), 0f, true);
            Box(herbObj.transform, new Vector3(HERB.x, 1.7f, HERB.y), new Vector3(0.5f, 1.0f, 0.5f), H("#8ff0a0"), 0.6f, true);
        }
    }
    void AddCrystal(float x, float z, string col)
    {
        Box(caveRoot, new Vector3(x, 1.2f, z), new Vector3(0.6f, 2.4f, 0.6f), H(col), (x + z) * 0.3f, true);
        Box(caveRoot, new Vector3(x, 2.5f, z), new Vector3(1.0f, 1.0f, 1.0f), H(col), (x * z) * 0.2f, true);
    }
    void AddStalagmite(float x, float z)
    {
        Box(caveRoot, new Vector3(x, 1.0f, z), new Vector3(1.2f, 2.0f, 1.2f), H("#3a3742"));
        Box(caveRoot, new Vector3(x, 2.1f, z), new Vector3(0.6f, 1.4f, 0.6f), H("#332f3a"));
        caveCols.Add(new Vector4(x, z, 0.7f, 0.7f));
    }

    // 起伏のある草原メッシュを生成（Gaia無しで草原を試すため）
    void BuildGrassland()
    {
        const int N = 64; const float size = 180f;
        float sx = UnityEngine.Random.value * 50f, sz = UnityEngine.Random.value * 50f;
        var verts = new Vector3[(N + 1) * (N + 1)];
        var uv = new Vector2[verts.Length];
        var tris = new int[N * N * 6];
        for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                float x = -size / 2f + size * i / N, z = -size / 2f + size * j / N;
                float u = (float)i / N, v = (float)j / N;
                float n = Mathf.PerlinNoise(sx + u * 4f, sz + v * 4f) * 0.6f
                        + Mathf.PerlinNoise(sx + u * 9f, sz + v * 9f) * 0.25f;
                float d = Mathf.Sqrt(x * x + z * z);
                float flat = Mathf.Clamp01((d - 12f) / 28f);   // 中央(村)は平ら、外側ほど起伏
                int k = j * (N + 1) + i;
                verts[k] = new Vector3(x, n * 4.2f * flat, z);
                uv[k] = new Vector2(u, v);
            }
        int t = 0;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int a = j * (N + 1) + i, b = a + 1, c = a + (N + 1), d2 = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d2;
            }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        groundMeshGO = new GameObject("Grassland");
        groundMeshGO.transform.SetParent(townRoot, false);
        groundMeshGO.AddComponent<MeshFilter>().mesh = mesh;
        var mr = groundMeshGO.AddComponent<MeshRenderer>();
        groundMeshGO.AddComponent<MeshCollider>().sharedMesh = mesh;
        SetCol(groundMeshGO, H("#5a9e4a"), false);

        ScatterProps();
    }

    // 草原に木と花をばらまく（地形の高さに沿って配置）
    void ScatterProps()
    {
        var rng = new System.Random(2024);
        for (int i = 0; i < 46; i++)
        {
            float x = (float)(rng.NextDouble() * 150 - 75);
            float z = (float)(rng.NextDouble() * 150 - 75);
            if (Mathf.Abs(x) < 6f && z > -34f && z < 36f) continue;       // 村・道を空ける
            if (z < -30f && z > -46f && Mathf.Abs(x) < 12f) continue;     // 洞口を空ける
            float gy = GroundY(x, z);
            Box(townRoot, new Vector3(x, gy + 1.1f, z), new Vector3(0.5f, 2.2f, 0.5f), H("#6f5230"));
            Box(townRoot, new Vector3(x, gy + 3.0f, z), new Vector3(2.4f, 2.2f, 2.4f), H("#4e8f4a"));
            Box(townRoot, new Vector3(x, gy + 4.3f, z), new Vector3(1.5f, 1.6f, 1.5f), H("#5aa356"));
        }
        string[] fc = { "#ffd35c", "#ff7b9c", "#c9a0ff", "#ffffff" };
        for (int i = 0; i < 70; i++)
        {
            float x = (float)(rng.NextDouble() * 150 - 75);
            float z = (float)(rng.NextDouble() * 150 - 75);
            float gy = GroundY(x, z);
            Box(townRoot, new Vector3(x, gy + 0.2f, z), new Vector3(0.25f, 0.35f, 0.25f), H(fc[i % 4]), 0f, true);
        }
    }

    void BuildPlayer()
    {
        var pg = new GameObject("Player");
        player = pg.transform;
        if (playerModel != null)
        {
            var v = Instantiate(playerModel, player);
            v.transform.localPosition = Vector3.zero;
            return;
        }
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(player, false);
        body.transform.localPosition = new Vector3(0, 1.1f, 0);
        body.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
        Destroy(body.GetComponent<Collider>());
        SetCol(body, H("#e8b04a"), false);
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.transform.SetParent(player, false);
        head.transform.localPosition = new Vector3(0, 2.55f, 0);
        head.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        Destroy(head.GetComponent<Collider>());
        SetCol(head, H("#f0d0a8"), false);
        var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);   // 背中側=向き表示
        pack.transform.SetParent(player, false);
        pack.transform.localPosition = new Vector3(0, 1.3f, 0.45f);
        pack.transform.localScale = new Vector3(0.8f, 1.4f, 0.25f);
        Destroy(pack.GetComponent<Collider>());
        SetCol(pack, H("#3aa0c8"), false);
    }

    // ---------------------------------------------------------  HUD (OnGUI)
    void MakeStyles()
    {
        stPanel = new GUIStyle(GUI.skin.box); stPanel.alignment = TextAnchor.UpperLeft;
        stObj = new GUIStyle(); stObj.fontSize = 17; stObj.fontStyle = FontStyle.Bold; stObj.normal.textColor = Color.white; stObj.wordWrap = true;
        stSmall = new GUIStyle(); stSmall.fontSize = 13; stSmall.normal.textColor = new Color(.75f, .85f, .9f);
        stTitle = new GUIStyle(); stTitle.fontSize = 24; stTitle.fontStyle = FontStyle.Bold; stTitle.alignment = TextAnchor.UpperCenter; stTitle.normal.textColor = Color.white;
        stDlgName = new GUIStyle(); stDlgName.fontSize = 14; stDlgName.fontStyle = FontStyle.Bold; stDlgName.normal.textColor = H("#4fe3c4");
        stDlgText = new GUIStyle(); stDlgText.fontSize = 18; stDlgText.normal.textColor = Color.white; stDlgText.wordWrap = true;
        stPrompt = new GUIStyle(GUI.skin.box); stPrompt.fontSize = 16; stPrompt.fontStyle = FontStyle.Bold; stPrompt.normal.textColor = Color.white; stPrompt.alignment = TextAnchor.MiddleCenter;
        stToast = new GUIStyle(); stToast.fontSize = 28; stToast.fontStyle = FontStyle.Bold; stToast.alignment = TextAnchor.MiddleCenter; stToast.normal.textColor = H("#ffd774");
        stBanner = new GUIStyle(); stBanner.fontSize = 40; stBanner.fontStyle = FontStyle.Bold; stBanner.alignment = TextAnchor.MiddleCenter; stBanner.normal.textColor = Color.white;
        stBtn = new GUIStyle(GUI.skin.button); stBtn.fontSize = 18; stBtn.fontStyle = FontStyle.Bold;
        stBig = new GUIStyle(); stBig.fontSize = 46; stBig.fontStyle = FontStyle.Bold; stBig.alignment = TextAnchor.MiddleCenter; stBig.normal.textColor = H("#ffd774");
        stylesReady = true;
    }
    void Bg(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, tex); GUI.color = Color.white; }

    void OnGUI()
    {
        if (!stylesReady) MakeStyles();
        float W = Screen.width, Hs = Screen.height;

        // タイトル
        GUI.Label(new Rect(0, 14, W, 30), "毒消し草クエスト", stTitle);

        // 目標カード
        Bg(new Rect(14, 52, 320, 78), new Color(.05f, .08f, .13f, .82f));
        string fname = field == Field.Town ? "エレンの村" : "北の洞窟";
        GUI.Label(new Rect(26, 60, 300, 20), fname, stDlgName);
        string[] obj = { "村人マレンに話しかけよう", "北の洞窟へ入り毒消し草を採取しよう", "毒消し草を村人マレンに届けよう", "クエスト達成！" };
        GUI.Label(new Rect(26, 80, 300, 44), obj[q], stObj);
        if (q < 3)
        {
            Vector2 tg = CurrentTarget();
            float d = Dist(tg);
            float rel = Mathf.Atan2(tg.x - player.position.x, -(tg.y - player.position.z)) - yaw;
            rel = Mathf.Atan2(Mathf.Sin(rel), Mathf.Cos(rel));  // -π..π へ正規化
            string arrow = Mathf.Abs(rel) < 0.5f ? "▲ 正面" : (rel > 0 ? "▶ 右へ" : "◀ 左へ");
            GUI.Label(new Rect(26, 108, 300, 20), arrow + "   " + Mathf.RoundToInt(d) + " m", stSmall);
        }

        // リセットボタン
        if (GUI.Button(new Rect(W - 116, 16, 100, 30), "↻ リセット")) ResetGame();

        // 操作ヒント
        Bg(new Rect(W / 2 - 240, Hs - 40, 480, 28), new Color(.05f, .08f, .13f, .8f));
        GUI.Label(new Rect(W / 2 - 230, Hs - 36, 460, 22), "W/S 前後   A/D 旋回   E 話す・調べる   R リセット", stSmall);

        // 調べる/話すプロンプト
        if (dlg == null && q != 3 && !transitioning)
        {
            bool near0 = field == Field.Town && q == 0 && Dist(NPC) < 3.6f;
            bool near2 = field == Field.Town && q == 2 && herb && Dist(NPC) < 3.6f;
            if (near0 || near2)
                GUI.Box(new Rect(W / 2 - 110, Hs - 120, 220, 34), near0 ? "[E] マレンと話す" : "[E] 毒消し草を渡す", stPrompt);
        }

        // トースト
        if (toastT > 0f) { GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastT)); GUI.Label(new Rect(0, Hs * 0.28f, W, 40), toastMsg, stToast); GUI.color = Color.white; }

        // フィールド名バナー
        if (bannerT > 0f) { GUI.color = new Color(1, 1, 1, Mathf.Clamp01(bannerT)); GUI.Label(new Rect(0, Hs * 0.4f, W, 50), fname, stBanner); GUI.color = Color.white; }

        // 会話
        if (dlg != null)
        {
            float dw = Mathf.Min(620, W - 60);
            Rect box = new Rect(W / 2 - dw / 2, Hs - 190, dw, 130);
            Bg(box, new Color(.05f, .08f, .13f, .9f));
            GUI.Label(new Rect(box.x + 20, box.y + 14, dw - 40, 20), dlgName, stDlgName);
            GUI.Label(new Rect(box.x + 20, box.y + 40, dw - 40, 70), dlg[dlgI], stDlgText);
            GUI.Label(new Rect(box.x + 20, box.y + 100, dw - 40, 20), "[E] つぎへ ▸", stSmall);
        }

        // 場面転換の暗幕
        if (fade > 0f) Bg(new Rect(0, 0, W, Hs), new Color(.02f, .03f, .05f, fade));

        // クリア画面
        if (q == 3)
        {
            Bg(new Rect(0, 0, W, Hs), new Color(.03f, .06f, .05f, .82f));
            GUI.Label(new Rect(0, Hs * 0.32f, W, 40), "QUEST COMPLETE", stDlgName);
            GUI.Label(new Rect(0, Hs * 0.36f, W, 60), "クエスト達成！", stBig);
            GUI.Label(new Rect(0, Hs * 0.48f, W, 24), "毒消し草を村へ届けた。  +120 Gold / +45 EXP", stSmall);
            if (GUI.Button(new Rect(W / 2 - 110, Hs * 0.56f, 220, 48), "もう一度あそぶ", stBtn)) ResetGame();
        }
    }
}
