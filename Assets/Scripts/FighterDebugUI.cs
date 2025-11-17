using UnityEngine;
using System;
using UnityEngine.InputSystem;   // 新InputSystem

public class FighterDebugUI : MonoBehaviour {
    [Header("Targets")]
    public FighterCoreManager[] cores;   // Player / Enemy など
    public int selectedIndex = 0;        // ForceState などをかける対象

    [Header("UI")]
    public KeyCode toggleKey = KeyCode.F3; // F3で表示/非表示切り替え

    bool visible = true;

    void Reset() {
        // シーン内の FighterCoreManager を自動検出（足りなければ手動で詰める）
        cores = UnityEngine.Object.FindObjectsByType<FighterCoreManager>(FindObjectsSortMode.None);
    }

    void Update() {
        // 新InputSystemでのキー判定
        var kb = Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) {
            visible = !visible;
        }

        // indexの安全確保
        if (cores != null && cores.Length > 0) {
            if (selectedIndex < 0 || selectedIndex >= cores.Length) {
                selectedIndex = 0;
            }
        }
    }

    void OnGUI() {
        if (!visible) return;

        if (cores == null || cores.Length == 0) {
            GUILayout.BeginArea(new Rect(400, 10, 800, 60), "FCM Debug", GUI.skin.window);
            GUILayout.Label("FighterCoreManager がシーンに見つかりません。");
            GUILayout.EndArea();
            return;
        }

        // 画面上部に横長のエリアを確保（高さは少し余裕を見て 240）
        GUILayout.BeginArea(
            new Rect(400, 10, 800, 240),
            "FCM Debug",
            GUI.skin.window
        );

        GUILayout.Label("Targets:");

        // ───────── ターゲット一覧：状態＋操作モードボタン ─────────
        for (int i = 0; i < cores.Length; i++) {
            var core = cores[i];
            if (core == null) continue;

            var playerInput = core.GetComponent<PlayerInput>();
            var aiCtrl      = core.GetComponent<AIFighterController>();

            GUILayout.BeginHorizontal();

            string label = $"[{i}] {core.gameObject.name}";
            if (i == selectedIndex) label += "  <== SELECTED";

            GUILayout.Label(label, GUILayout.Width(220));
            GUILayout.Label($"State: {core.State}", GUILayout.Width(140));
            GUILayout.Label($"Attack: {core.CurrentAttackKind}", GUILayout.Width(160));

            GUILayout.Label("Control:", GUILayout.Width(60));

            // ★ Humanボタン：このキャラだけHuman操作、他はAIにする
            if (GUILayout.Button("Human", GUILayout.Width(60))) {
                for (int j = 0; j < cores.Length; j++) {
                    var c  = cores[j];
                    if (c == null) continue;

                    var pi = c.GetComponent<PlayerInput>();
                    var ai = c.GetComponent<AIFighterController>();

                    bool isThis = (j == i);

                    if (pi) pi.enabled = isThis;     // この1体だけ PlayerInput ON
                    if (ai) ai.enabled = !isThis;    // 他は AI ON（任意）
                }
            }

            // ★ AIボタン：このキャラもAIに戻す（全員AIで観察したいとき用）
            if (GUILayout.Button("AI", GUILayout.Width(60))) {
                if (playerInput) playerInput.enabled = false;
                if (aiCtrl)      aiCtrl.enabled      = true;
            }

            // デバッグ対象の選択
            if (GUILayout.Button("Select", GUILayout.Width(60))) {
                selectedIndex = i;
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // ───────── Freeze FSM（選択中の1体） ─────────
        var current = cores[selectedIndex];
        if (current != null) {
            bool freeze = GUILayout.Toggle(
                current.debugFreezeFSM,
                $"Freeze FSM of [{selectedIndex}] {current.gameObject.name}"
            );
            if (freeze != current.debugFreezeFSM) {
                current.debugFreezeFSM = freeze;
            }
        }

        GUILayout.Space(5);

        // ───────── Force State（選択中の1体のFCMを強制変更）─────────
        GUILayout.Label(
            $"Force State for [{selectedIndex}] {cores[selectedIndex].gameObject.name}:"
        );

        GUILayout.BeginHorizontal();

        foreach (FighterCoreManager.FighterState st
                 in Enum.GetValues(typeof(FighterCoreManager.FighterState))) {

            if (GUILayout.Button(st.ToString(), GUILayout.Width(60))) {
                var core = cores[selectedIndex];
                if (core != null) {
                    core.ForceStateDebug(st);   // ★ ここでFCMへ強制遷移命令
                }
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
