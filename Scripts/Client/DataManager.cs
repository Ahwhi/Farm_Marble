using System;
using ServerCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class DataManager : MonoBehaviour {
    public static Session session { get; set; }
    public static bool isLogin { get; set; }
    public static int playerId { get; set; }
    public static string accountId { get; set; }
    public static int win {  get; set; }
    public static int lose { get; set; }
    public static float winP {  get; set; }

    void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    void Update() {
        if (!NetworkManager.Instance.isConnected && !NetworkManager.Instance.disconnectedPopUpFlag) {
            NetworkManager.Instance.disconnectedPopUpFlag = true;
            CreatePopUpMessage("서버 연결 실패", "서버에 연결할 수 없습니다.", "종료", () => { Application.Quit(); });
        }
    }

    // 사용예시
    // DataManager.CreatePopUpMessage("ㅎㅇ", "ㅂㅇ", "ㅇㅋ", () => { Debug.Log("확인 버튼 눌림"); });
    public static void CreatePopUpMessage(string title, string message, string buttonText, Action action) {
        var prefab = Resources.Load<GameObject>("PopupCanvasPrefab");
        if (prefab == null) { Debug.LogError("PopupCanvasPrefab not found"); return; }

        var go = Object.Instantiate(prefab);
        var popup = go.transform.Find("Popup");
        if (popup == null) { Debug.LogError("Popup child not found"); return; }

        // 제목/메시지
        var t = popup.Find("Panel_T/T")?.GetComponent<TextMeshProUGUI>();
        var m = popup.Find("Panel_M/M")?.GetComponent<TextMeshProUGUI>();
        if (t) t.text = title;
        if (m) m.text = message;

        // 버튼 (Text + Button)
        var buttonObj = popup.Find("Panel_B/Button");
        if (buttonObj != null) {
            var btn = buttonObj.GetComponent<UnityEngine.UI.Button>();
            var txt = buttonObj.Find("B")?.GetComponent<TextMeshProUGUI>();

            if (txt) txt.text = buttonText;

            if (btn != null) {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    action?.Invoke();
                    GameObject.Destroy(go); // 클릭 후 팝업 닫기
                });
            }
        }
    }
}
