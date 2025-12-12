using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomManager : MonoBehaviour {
    [Header("● UI ●")]
    public Button Button_Ready;
    public Button Button_Leave;
    public TextMeshProUGUI Text_Title;
    public TMP_InputField InputField_Chat;

    [Header("● Sound ●")]
    private AudioSource audioSource;
    public AudioClip mouseClick;

    [Header("● DATA ●")]
    public static string inputChat;
    public bool isAskRoomInformation = false;
    public static bool getRoomInformation = false;
    public static int roomId = 0;
    public static string roomTitle = "";
    public static int maxPlayer = 0;

    void Start() {
        InputField_Chat.onSubmit.AddListener(OnSubmitChat);
        // 씬에 들어오면 플레이어 목록을 요청
        C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
        NetworkManager.Send(requestPlayerListPacket.Write());

        audioSource = GetComponent<AudioSource>();
    }

    void Update() {
        if (!isAskRoomInformation) {
            isAskRoomInformation = true;
            C_GetRoomInformation getRoomPacket = new C_GetRoomInformation();
            NetworkManager.Send(getRoomPacket.Write());
        }

        if (getRoomInformation) {
            Text_Title.text = $"{roomId}. {roomTitle}";
            getRoomInformation = false;
        }
    }

    public static void OnSetPlayerList(S_PlayerList packet) {
        Object obj = Resources.Load("UserDataPrefab");
        Transform parentTransform = GameObject.Find("Panel_Main").transform;

        // 일단 기존 프리팹 제거
        foreach (Transform child in parentTransform) {
            GameObject.Destroy(child.gameObject);
        }
        
        // 플레이어 목록 생성
        foreach (S_PlayerList.Player p in packet.players) {
            GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
            TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            texts[0].text = p.accountId;
            texts[1].text = $"{p.win}승 {p.lose}패\n승률: {p.winP}%";

            string ready = "";
            if (p.isGameReady) {
                ready = "[준비완료]";
                texts[2].color = Color.yellow;
            } else {
                ready = "";
                texts[2].color = Color.black;
            }

            texts[2].text = ready;

            if (p.isSelf) {
                go.GetComponent<Image>().color = new Color(0,255,255,255);
            } else {
                go.GetComponent<Image>().color = Color.gray;
            }

        }

        //if (packet.players.Count < maxPlayer) {
        //    for (int i = 0; i < maxPlayer - packet.players.Count; i++) {
        //        GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
        //        TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>();
        //        texts[0].text = "";
        //        texts[1].text = "";
        //        texts[2].text = "";
        //        go.GetComponent<Image>().color = Color.black;
        //    }
        //}
    }

    public void OnButtonReadyStart() {
        audioSource.PlayOneShot(mouseClick);
        C_GameReady gameReadyPacket = new C_GameReady();
        NetworkManager.Send(gameReadyPacket.Write());
    }

    public void OnButtonLeave() {
        audioSource.PlayOneShot(mouseClick);
        SceneManager.LoadScene("LobbyScene");
        C_LeaveRoom leaveRoomPacket = new C_LeaveRoom();
        NetworkManager.Send(leaveRoomPacket.Write());
    }

    private void OnSubmitChat(string text) {
        if (!string.IsNullOrEmpty(text)) {
            inputChat = text;
            OnButtonSend();
            InputField_Chat.ActivateInputField(); // 다시 입력할 수 있도록 포커스 유지
        }
    }

    public void OnButtonSend() {
        audioSource.PlayOneShot(mouseClick);
        C_Chat chatPacket = new C_Chat();
        chatPacket.message = inputChat;
        NetworkManager.Send(chatPacket.Write());

        InputField_Chat.text = null;
        inputChat = null;
    }

}
