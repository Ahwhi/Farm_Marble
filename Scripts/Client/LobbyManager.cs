using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour {
    [Header("● UI ●")]
    public TMP_InputField InputField_Chat;
    public TMP_InputField InputField_RoomTitle;
    public ScrollRect ScrollRect_Chat;
    public GameObject PopUpCanvas;

    [Header("● Sound ●")]
    private AudioSource audioSource;
    public AudioClip mouseClick;

    [Header("● DATA ●")]
    public static string inputChat;
    public static string inputRoomTitle;
    private bool isMade;
    private int maxPlayer;

    void Start() {
        // 씬에 들어오면 방 목록을 요청
        C_RequestRoom requestRoomPacket = new C_RequestRoom(); //월드룸 자동입장!!!!!!!!
        NetworkManager.Send(requestRoomPacket.Write());

        isMade = false;
        InputField_Chat.onSubmit.AddListener(OnSubmitChat);
        InputField_RoomTitle.onSubmit.AddListener(OnSubmitRoomTitle);
        PopUpCanvas.SetActive(false);

        audioSource = GetComponent<AudioSource>();
    }

    void Update() {
        ScrollRect_Chat.verticalNormalizedPosition = 0f;

        if (DataManager.isLogin && !isMade) {
            isMade = true;
            InputField_Chat.onValueChanged.AddListener((text) => {
                inputChat = text;
            });
        }
    }

    public static void OnSetPlayerList(S_PlayerList packet) {
        Object obj = Resources.Load("UserListPrefab");
        Transform parentTransform = GameObject.Find("UserListContent").transform;

        GameObject.Find("numText").GetComponent<TextMeshProUGUI>().text = $"로비 현재 인원: {packet.players.Count}명";

        // 일단 기존 프리팹 제거
        foreach (Transform child in parentTransform) {
            GameObject.Destroy(child.gameObject);
        }

        // 플레이어 목록 생성
        foreach (S_PlayerList.Player p in packet.players) {
            GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
            if (p.isSelf) {
                go.GetComponentInChildren<Image>().color = Color.green;
                DataManager.win = p.win;
                DataManager.lose = p.lose;
                DataManager.winP = p.winP;
                GameObject.Find("MyInformation").GetComponent<TextMeshProUGUI>().text = $"{DataManager.accountId}\n{DataManager.win}승 {DataManager.lose}패 승률: {DataManager.winP}%";
            } else {
                go.GetComponentInChildren<Image>().color = Color.white;
            }
            go.GetComponentInChildren<TextMeshProUGUI>().text = p.accountId;
        }
    }

    public static void OnSetRoomList(S_RoomList pkt) {
        Object obj = Resources.Load("RoomPrefab");
        Transform parentTransform = GameObject.Find("RoomContent").transform;

        // 일단 기존 프리팹 제거
        foreach (Transform child in parentTransform) {
            GameObject.Destroy(child.gameObject);
        }

        // 룸 생성
        foreach (S_RoomList.Room r in pkt.rooms) {
            if (r.roomId <= 1) {
                continue;
            }
            GameObject go = Object.Instantiate(obj, parentTransform) as GameObject;
            string status = "대기";
            if (r.isGaming) {
                go.GetComponentInChildren<Button>().interactable = false;
                status = "게임 중";
            }
            TextMeshProUGUI[] texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            texts[0].text = status;
            texts[1].text = $"No. {r.roomId}";
            texts[2].text = $"{r.roomTitle}\n[{r.playerNumInRoom}/{r.maxPlayer}명]\n[방장: {r.roomOwnerId}]";


            // 버튼 이벤트 추가
            Button btn = go.GetComponentInChildren<Button>();
            if (btn != null) {
                int roomId = r.roomId; // 클로저 문제 방지용
                btn.onClick.AddListener(() => {
                    // 서버에 입장 패킷 보내기
                    C_JoinRoom join = new C_JoinRoom();
                    join.roomId = roomId;
                    join.accountId = DataManager.accountId;
                    NetworkManager.Send(join.Write());
                });
            }
        }
    }

    private void OnSubmitChat(string text) {
        if (!string.IsNullOrEmpty(text)) {
            inputChat = text;
            OnButtonSend();
            InputField_Chat.ActivateInputField(); // 다시 입력할 수 있도록 포커스 유지
        }
    }

    private void OnSubmitRoomTitle(string text) {
        audioSource.PlayOneShot(mouseClick);
        if (!string.IsNullOrEmpty(text)) {
            inputRoomTitle = text;
            OnButtonMakeRoom();
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

    public void OnButtonCreateRoom() {
        audioSource.PlayOneShot(mouseClick);
        maxPlayer = 2;
        PopUpCanvas.SetActive(true);
        GameObject.Find("MaxText").GetComponent<TextMeshProUGUI>().text = $"{maxPlayer}명";
    }

    public void OnButtonMakeRoom() {
        audioSource.PlayOneShot(mouseClick);
        inputRoomTitle = InputField_RoomTitle.text;
        if (maxPlayer > 4 || maxPlayer < 2) {
            maxPlayer = 2;
        }
        C_CreateRoom createRoomPacket = new C_CreateRoom();
        createRoomPacket.roomTitle = inputRoomTitle;
        createRoomPacket.maxPlayer = maxPlayer;
        NetworkManager.Send(createRoomPacket.Write());
        maxPlayer = 2;
        inputRoomTitle = "";
        InputField_RoomTitle.text = inputRoomTitle;
    }

    public void OnButtonLeftPeople() {
        audioSource.PlayOneShot(mouseClick);
        if (maxPlayer <= 2) {
            maxPlayer = 2;
        } else {
            maxPlayer--;
        }
        GameObject.Find("MaxText").GetComponent<TextMeshProUGUI>().text = $"{maxPlayer}명";
    }

    public void OnButtonRightPeople() {
        audioSource.PlayOneShot(mouseClick);
        if (maxPlayer >= 4) {
            maxPlayer = 4;
        } else {
            maxPlayer++;
        }
        GameObject.Find("MaxText").GetComponent<TextMeshProUGUI>().text = $"{maxPlayer}명";
    }

    public void OnCancelMakeRoom() {
        audioSource.PlayOneShot(mouseClick);
        PopUpCanvas.SetActive(false);
        maxPlayer = 2;
        inputRoomTitle = "";
        InputField_RoomTitle.text = inputRoomTitle;
    }

    public void OnTest() {
        audioSource.PlayOneShot(mouseClick);
        DataManager.CreatePopUpMessage("구현 중", "구현 중인 기능입니다.", "닫기", () => { /*Debug.Log("확인 버튼 눌림");*/ });
    }
}