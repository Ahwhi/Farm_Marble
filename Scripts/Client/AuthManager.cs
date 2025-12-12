using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AuthManager : MonoBehaviour {
    public static AuthManager Instance { get; private set; }

    [Header("● DEBUG ●")]
    public bool isDeleteAutoLogin = false;

    [Header("● UI ●")]
    public TMP_InputField InputField_Id;
    public TMP_InputField InputField_Pw;
    public Button Button_Register;
    public Button Button_Login;
    public TextMeshProUGUI Message;
    public Toggle AutoLoginToggle;

    [Header("● Sound ●")]
    private AudioSource audioSource;
    public AudioClip mouseClick;

    [Header("● DATA ●")]
    private string inputId;
    private string inputPw;
    private bool isServerStatus;
    private readonly Regex IdRegex = new Regex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled);
    private bool isCanUseButton;

    void Awake() {
        Instance = this;
        Button_Login.interactable = false;
        Button_Register.interactable = false;
        Message.color = Color.red;
        Message.text = "서버 점검 중입니다.";
    }

    void Start() {
        if (isDeleteAutoLogin) {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        // 토큰이 있으면 자동 로그인
        string savedToken = PlayerPrefs.GetString("AccessToken", "");
        if (!string.IsNullOrEmpty(savedToken)) {
            C_AutoLogin autoLoginPacket = new C_AutoLogin();
            autoLoginPacket.accessToken = savedToken;
            NetworkManager.Send(autoLoginPacket.Write());
        }

        InputField_Id.onValueChanged.AddListener((text) => { inputId = text; });
        InputField_Pw.onValueChanged.AddListener((text) => { inputPw = text; });
        InputField_Id.onSubmit.AddListener(OnSubmitLogin);
        InputField_Pw.onSubmit.AddListener(OnSubmitLogin);

        InputField_Id.Select();
        InputField_Id.ActivateInputField();

        audioSource = GetComponent<AudioSource>();
    }

    void Update() {
        if (NetworkManager.Instance.isConnected != isServerStatus) {
            isServerStatus = NetworkManager.Instance.isConnected;
            OnApplyServerStatus(isServerStatus);
        }

        // 탭으로 아이디, 비밀번호 넘어가기
        if (Input.GetKeyDown(KeyCode.Tab)) {
            Selectable current = EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();
            if (current != null) {
                Selectable next = current.FindSelectableOnDown();
                if (next != null)
                    next.Select();
            }
        }
    }

    public void OnApplyServerStatus(bool connected) {
        if (!connected) {
            Button_Login.interactable = false;
            Button_Register.interactable = false;
            Message.color = Color.red;
            Message.text = "서버 점검 중입니다.";
        } else {
            Button_Login.interactable = true;
            Button_Register.interactable = true;
            Message.color = Color.green;
            Message.text = "";
        }
    }

    public void OnLoginResult(bool success, int failCode) {
        if (!success) {
            Button_Login.interactable = true;
            Button_Register.interactable = true;
        }
        if (failCode == 0) {
            Message.text = "로그인 완료";
            Message.color = Color.green;
        } else if (failCode == 1) {
            Message.text = "없는 ID 입니다.";
            Message.color = Color.red;
        } else if (failCode == 2) {
            Message.text = "비밀번호가 틀립니다.";
            Message.color = Color.red;
            InputField_Pw.text = "";
            inputPw = "";
        }
        isCanUseButton = false;
    }

    public void OnAutoLoginResult(bool success, int failCode) {
        if (!success) {
            Button_Login.interactable = true;
            Button_Register.interactable = true;
        }
        if (failCode == 0) {
            Message.text = "자동 로그인 완료";
            Message.color = Color.green;
        } else if (failCode == 1) {
            Message.text = "자동 로그인 토큰이 없습니다.";
            Message.color = Color.red;
        } else if (failCode == 2) {
            Message.text = "자동 로그인 토큰이 만료 되었습니다.";
            Message.color = Color.red;
        }
        isCanUseButton = false;
    }

    public void OnRegisterResult(bool success, int failCode) {
        if (failCode == 0) {
            Message.text = "회원 가입 완료";
            Message.color = Color.green;
        } else if (failCode == 1) {
            Message.text = "이미 존재하는 ID입니다.";
            Message.color = Color.red;
        }
        Button_Login.interactable = true;
        Button_Register.interactable = true;
        isCanUseButton = false;
    }

    public void OnButtonRegister() {
        audioSource.PlayOneShot(mouseClick);
        if (inputId == null || inputPw == null) {
            Message.text = "비어있는 정보가 있습니다.";
            Message.color = Color.yellow;
            return;
        }

        if (inputId.Length < 6) {
            Message.text = "아이디는 6자 이상이어야 합니다!";
            Message.color = Color.yellow;
            return;
        }

        if (inputPw.Length < 8) {
            Message.text = "비밀번호는 8자 이상이어야 합니다!";
            Message.color = Color.yellow;
            return;
        }

        // 특수문자 체크 (알파벳+숫자만 허용)
        if (!IdRegex.IsMatch(inputId)) {
            Message.text = "아이디는 영어와 숫자만 사용할 수 있습니다!";
            Message.color = Color.yellow;
            return;
        }

        Message.color = Color.black;
        Message.text = "가입 중입니다.";
        isCanUseButton = true;
        Button_Login.interactable = false;
        Button_Register.interactable = false;

        C_Register registerPacket = new C_Register();
        registerPacket.accountId = inputId;
        registerPacket.accountPw = inputPw;
        NetworkManager.Send(registerPacket.Write());
    }

    public void OnButtonLogin() {
        audioSource.PlayOneShot(mouseClick);
        if (inputId == null || inputPw == null) {
            Message.text = "비어있는 정보가 있습니다.";
            Message.color = Color.yellow;
            return;
        }

        if (inputId.Length < 6) {
            Message.text = "아이디는 6자 이상이어야 합니다!";
            Message.color = Color.yellow;
            return;
        }

        if (inputPw.Length < 8) {
            Message.text = "비밀번호는 8자 이상이어야 합니다!";
            Message.color = Color.yellow;
            return;
        }

        // 특수문자 체크 (알파벳+숫자만 허용)
        if (!IdRegex.IsMatch(inputId)) {
            Message.text = "아이디는 영어와 숫자만 사용할 수 있습니다!";
            Message.color = Color.yellow;
            return;
        }

        Message.color = Color.white;
        Message.text = "로그인 중입니다.";
        isCanUseButton = true;
        Button_Login.interactable = false;
        Button_Register.interactable = false;

        C_Login loginPacket = new C_Login();
        loginPacket.accountId = inputId;
        loginPacket.accountPw = inputPw;
        loginPacket.isAutoLogin = AutoLoginToggle.isOn;
        NetworkManager.Send(loginPacket.Write());
    }

    private void OnSubmitLogin(string text) {
        audioSource.PlayOneShot(mouseClick);
        if (!isServerStatus || isCanUseButton) {
            return;
        }
        
        if (!string.IsNullOrEmpty(text)) {
            inputPw = text;
            OnButtonLogin();

            // 다시 입력할 수 있도록 포커스 유지
            InputField_Pw.ActivateInputField();
        }
    }
}
