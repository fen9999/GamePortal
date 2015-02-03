using UnityEngine;
using System.Collections;
using Electrotank.Electroserver5.Api;
using System.Collections.Generic;
using Electrotank.Electroserver5.Core;
using System;

public class LoginScreenView : MonoBehaviour
{
    #region UnityEditor
    public CUIHandle btLogin, btForgotPass, btRegister, btnHideRegisterForm, btnLogo, btnCalling;
    public UIInput lableUsername, lablePassword;
    public UIToggle cbSavePass;
    public CUIHandle facebook;
    public UITable listGameLogo;
    public Transform formLogin, formRegister;
    public Wifi statusWifi;
    public BroadcastView broadcast;
    #endregion
    private string cookie = "";

    bool IsClickButtonLogin = false;
	bool hasAccessToken = false;
    [HideInInspector]
    public bool isFormRegisterHide = true;

    public bool IsFormRegisterHide
    {
        get { return isFormRegisterHide; }
        set
        {
            if (value == isFormRegisterHide) return;
            isFormRegisterHide = value; if (isFormRegisterHide) HideFormRegister(); 
        }
    }
    private bool isInit = false;
    private bool isLoadedListGame = false;
    void OnGUI()
    {
        float height = 35 * 2;

        if (!Common.CanTestMode)
            return;
        float width = Screen.width / 4;
        Rect rectServer = new Rect(Screen.width - width, 0, width, 35f);
        GUI.Label(new Rect(rectServer.x, rectServer.y, rectServer.width * 1 / 3, rectServer.height), "SERVER: ");
        CServer.HOST_NAME = GUI.TextArea(new Rect(rectServer.x + rectServer.width * 1 / 3, rectServer.y, rectServer.width * 2 / 3, rectServer.height), CServer.HOST_NAME, 15);
        Rect rect = rectServer;
        rect.y += height / 2;
        rect.width = rectServer.width / 3;
        if (GUI.Button(rect, "Local"))
            CServer.HOST_NAME = "127.0.0.1";
        rect.x += rectServer.width / 3;
        if (GUI.Button(rect, "Web"))
            CServer.HOST_NAME = "210.211.102.97";
        rect.x += rectServer.width / 3;
        if (GUI.Button(rect, "Test"))
            CServer.HOST_NAME = "210.211.102.121";
    }
    void Update()
    {
        if (NotificationView.isHide)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                EnterLogin();
            }
        }
    }
    void FixedUpdate() 
    {
        GameObject obj = GameObject.Find("__HeaderMenu");
        if (obj != null)
            GameObject.Destroy(obj);
    }
    // Use this for initialization
    void Awake()
    {
        CUIHandle.AddClick(btLogin, OnClickButtonLogin);
        CUIHandle.AddClick(btForgotPass, OnClickButtonForgot);
        CUIHandle.AddClick(btRegister, OnClickButtonRegister);
        CUIHandle.AddClick(facebook, OnBtnFacebookClick);
        CUIHandle.AddClick(btnHideRegisterForm, OnHideRegisterForm);
        CUIHandle.AddClick(btnCalling, OnClickCalling);
        CUIHandle.AddClick(btnLogo, OnClickLogo);
        GameManager.Instance.applicationStart.EventLoadConfig += OnLoadBroadcast_Messeage;
        //khi disconnect mọi event sẽ bị mất
        if (GameManager.Server!=null)
        {
            ServerCreateInstance();
        }
        GameManager.Instance.EventServerCreateInstance += ServerCreateInstance;

        //GameManager.Server.EventLoginResponse += OnLogin;
        //GameManager.Server.EventJoinRoom += OnAfterJoinRoom;
        //GameManager.Server.EventPluginMessageOnProcess += PluginMessageOnProcess;
        //fix when reload sences login applicationstart event not start
        OnLoadBroadcast_Messeage();
        LoadListGameLogo();
        if(GameManager.Setting.AllowLockScreen)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void PluginMessageOnProcess(string command, string action, EsObject PluginMessageParameters)
    {
        if (command == Fields.RESPONSE.LOGIN_RESPONSE)
        {
            Debug.Log("OnLoginResponse " + PluginMessageParameters);
            Debug.Log("Đã nhận được thông tin server");
            EsObject esoResponce = PluginMessageParameters;

            if (esoResponce.getBoolean("loginResult"))
            {
                if (esoResponce.variableExists("gifts"))
                {
                    EsObject[] esoGifts = esoResponce.getEsObjectArray("gifts");
                    int index = Array.FindIndex(esoGifts, eI => eI.getBoolean("currentDate") == true);
                    if (esoGifts.Length > 0)
                    {
                        for (int i = 0; i < esoGifts.Length; i++)
                        {
                            Announcement ann = new Announcement(esoGifts[i]);
                            ann.index = i;
                            ann.description = "Ngày " + (i + 1);
                            if (i < index)
                                ann.receivered = true;
                            GameManager.Instance.ListAnnouncement.Add(ann);
                        }
                    }
                }
                if (esoResponce.variableExists("countUnReadMessage"))
                    GameManager.Server.totalMesseageCount = esoResponce.getInteger("countUnReadMessage");

                GameManager.Setting.BroadcastMessage = esoResponce.getString("broadCastMessage");
                //GameManager.Instance.channelRoom = new RoomInfo(esoResponce.getEsObject("firstRoomToJoin"));
                GameManager.Instance.hallRoom = new RoomInfo(esoResponce.getEsObject("firstRoomToJoin"));
                GameManager.Instance.mInfo = new User(esoResponce.getEsObject("userInfo"));

                if (esoResponce.variableExists("ceo_chan"))
                    Common.RULE_CHIP_COMPARE_BETTING = esoResponce.getInteger("ceo_chan");
                if (esoResponce.variableExists("pingRequire"))
                    GameManager.Setting.IsPingRequire = esoResponce.getBoolean("pingRequire");



                if (esoResponce.variableExists("gameRoom"))
                {
                    ((LobbyChan)GameManager.Instance.selectedLobby).SetDataJoinLobby(esoResponce.getEsObject("gameRoom"));
                    GameManager.Instance.selectedChannel.SetDataRoom(esoResponce.getEsObject("gameRoom").getEsObject("gameDetails").getEsObject("parent"));
                    GameManager.Instance.currentRoom = new RoomInfo(GameManager.Instance.hallRoom.zoneId, GameManager.Instance.hallRoom.roomId);
                    GameManager.Server.DoJoinGame(((LobbyChan)GameManager.Instance.selectedLobby).config.password);
                    GameManager.LoadScene(ESceneName.GameplayChan);
                    return;
                }

                ServerWeb.StartThread(ServerWeb.URL_REQUEST_USER, new object[] { "username", GameManager.Instance.mInfo.username }, delegate(bool isDone, WWW res, IDictionary json)
                {

                });
                GameManager.Instance.mInfo.password = lablePassword.value;
                GameManager.Server.DoJoinRoom(GameManager.Instance.hallRoom.zoneId, GameManager.Instance.hallRoom.roomId);
#if UNITY_WEBPLAYER
                if (!hasAccessToken)
                    Application.ExternalEval("ajaxLoginUnity(\"" + GameManager.Instance.userNameLogin + "\", \"" + GameManager.Instance.passwordLogin + "\");");
#endif
                if (!cbSavePass.value)
                {
                    StoreGame.Remove(StoreGame.EType.SAVE_USERNAME);
                    StoreGame.Remove(StoreGame.EType.SAVE_PASSWORD);
                    StoreGame.Remove(StoreGame.EType.SAVE_ACCESSTOKEN);
                    GameManager.Instance.userNameLogin = GameManager.Instance.passwordLogin = GameManager.Instance.accessToken = "";
                }
                else
                {
                    StoreGame.SaveString(StoreGame.EType.SAVE_USERNAME, GameManager.Instance.userNameLogin);
                    StoreGame.SaveString(StoreGame.EType.SAVE_PASSWORD, GameManager.Instance.passwordLogin);
                    StoreGame.SaveString(StoreGame.EType.SAVE_ACCESSTOKEN, GameManager.Instance.accessToken);
                }
            }
            else
            {
                Debug.Log("Login false");
                IsClickButtonLogin = false;
                StoreGame.Remove(StoreGame.EType.SAVE_USERNAME);
                StoreGame.Remove(StoreGame.EType.SAVE_PASSWORD);
                StoreGame.Remove(StoreGame.EType.SAVE_ACCESSTOKEN);
                GameManager.Instance.userNameLogin = GameManager.Instance.passwordLogin = GameManager.Instance.accessToken = "";

                string message = "Thông tin đăng nhập không hợp lệ. Yêu cầu nhập lại thông tin truy cập.";
                if (!string.IsNullOrEmpty(esoResponce.getString("reason")))
                {
                    message = esoResponce.getString("reason");
                }
                NotificationView.ShowMessage(message);
            }
        }

        WaitingView.Instance.Close();
    }

    void ServerCreateInstance()
    {
        Debug.LogWarning("ServerCreateInstance");
        //GameManager.Server.EventLoginResponse += OnLogin;
        GameManager.Server.EventJoinRoom += OnAfterJoinRoom;
        GameManager.Server.EventPluginMessageOnProcess += PluginMessageOnProcess;
        //fix when reload sences login applicationstart event not start
        OnLoadBroadcast_Messeage();
    }

    void OnDestroy()
    {
        CUIHandle.RemoveClick(btLogin, OnClickButtonLogin);
        CUIHandle.RemoveClick(btForgotPass, OnClickButtonForgot);
        CUIHandle.RemoveClick(btRegister, OnClickButtonRegister);
        CUIHandle.RemoveClick(facebook, OnBtnFacebookClick);
        CUIHandle.RemoveClick(btnHideRegisterForm, OnHideRegisterForm);
        if (!GameManager.IsExist)
            return;
        //GameManager.Server.EventLoginResponse -= OnLogin;
        GameManager.Server.EventJoinRoom -= OnAfterJoinRoom;
        GameManager.Instance.EventServerCreateInstance -= ServerCreateInstance;
        GameManager.Server.EventPluginMessageOnProcess -= PluginMessageOnProcess;
        GameManager.Instance.applicationStart.EventLoadConfig -= OnLoadBroadcast_Messeage;
    }

    private void OnLoadBroadcast_Messeage()
    {
        if (GameManager.Setting.Platform.GetConfigByType(PlatformType.broadcast_message) != null)
        {
            if (broadcast)
            {
                NGUITools.SetActive(broadcast.gameObject, true);
                broadcast.Show(GameManager.Setting.Platform.GetConfigByType(PlatformType.broadcast_message).Value);
            }
        }
    }

    void OnBtnFacebookClick(GameObject go)
    {
        if (!string.IsNullOrEmpty(GameManager.Setting.IsMustUpdate))
        {
            Common.MustUpdateGame();
            return;
        }

#if UNITY_ANDROID || UNITY_IPHONE|| UNITY_WEBPLAYER
        SocialCommon.Instance.LoginFaceBook(cbSavePass.value);
#else
        Common.VersionNotSupport("Đăng nhập bằng Facebook");
#endif
        //NotificationView.ShowMessage("Chức năng tạm thời tạm khóa, vui lòng đăng nhập bằng tài khoản thường", 4f);
    }
    void OnClickButtonLogin(GameObject go)
    {
        EnterLogin();
    }
    void EnterLogin()
    {
        btLogin.StopImpact(1f);

        if (!string.IsNullOrEmpty(GameManager.Setting.IsMustUpdate))
        {
            Common.MustUpdateGame();
            return;
        }

        if (!Common.IsRelease)
        {
            string testUsername = "test";
            string testPassword = "trieuphi";

#if UNITY_STANDALONE_WIN
            testUsername = "trieuphi0";
#elif UNITY_ANDROID
            testUsername = "test1";
#else
            testUsername = "test";
#endif
            if (Application.isEditor)
            {
                testUsername = "trieuphi07021";
                testPassword = "trieuphi";
            }

            if (string.IsNullOrEmpty(lableUsername.value) && string.IsNullOrEmpty(lablePassword.value))
            {
                lableUsername.value = testUsername;
                lablePassword.value = testPassword;
            }
        }

        if (string.IsNullOrEmpty(lableUsername.value))
        {
            NotificationView.ShowMessage("Tên truy cập không được phép để trống");
            return;
        }
        if (string.IsNullOrEmpty(lablePassword.value))
        {
            NotificationView.ShowMessage("Mật khẩu không được phép để trống.");
            return;
        }

        IsClickButtonLogin = true;
        GameManager.Instance.userNameLogin = lableUsername.value;
        GameManager.Instance.passwordLogin = lablePassword.value;
        GameManager.Server.DoLogin();

        if (!Common.IsRelease)
            StoreGame.SaveString(StoreGame.EType.SAVE_SERVER, CServer.HOST_NAME);
		
    }

    void OnOkUpdateVersion()
    {
        Application.OpenURL(GameManager.Setting.IsMustUpdate);
    }

    void OnCancelUpdateVersion()
    { 

    }

    IEnumerator Start()
    {
        Debug.Log(GameManager.Setting.IsMustUpdate);

		if (!Common.IsRelease)
			CServer.HOST_NAME = StoreGame.LoadString(StoreGame.EType.SAVE_SERVER);

        if (!string.IsNullOrEmpty(GameManager.Setting.IsMustUpdate))
        {
            NotificationView.ShowConfirm("Thông báo", "Có bản cập nhật quan trọng. Bạn có muốn tải về?", OnOkUpdateVersion, OnCancelUpdateVersion);
            yield break;
        }
        else
        {
            if (Common.IsRelease && GameManager.Instance.currentRoom != null)
            {

#if UNITY_WEBPLAYER
                    switch (GameSettings.Instance.TypeBuildFor)
                    {
                        case GameSettings.EBuildType.esimo:
                        case GameSettings.EBuildType.web_esimo:
                            //UpdateCookieString();
                            //StartCoroutine(_LoginInWebPlayer());
                            break;
                        case GameSettings.EBuildType.web_facebook:
                            while (string.IsNullOrEmpty(CServer.HOST_NAME))
                                yield return new WaitForEndOfFrame();
                            WaitingView.Show("Đang lấy thông tin");
                            SocialCommon.Instance.LoginFaceBook(cbSavePass.value);
                            break;

                    }
#endif
                GameManager.Instance.userNameLogin = StoreGame.Contains(StoreGame.EType.SAVE_USERNAME) ? StoreGame.LoadString(StoreGame.EType.SAVE_USERNAME) : "";
                GameManager.Instance.passwordLogin = StoreGame.Contains(StoreGame.EType.SAVE_PASSWORD) ? StoreGame.LoadString(StoreGame.EType.SAVE_PASSWORD) : "";
                if (!string.IsNullOrEmpty(GameManager.Instance.userNameLogin) && !string.IsNullOrEmpty(GameManager.Instance.passwordLogin))
                {
                    while (string.IsNullOrEmpty(CServer.HOST_NAME))
                        yield return new WaitForEndOfFrame();
                    yield return new WaitForSeconds(0.1f);
                    WaitingView.Show("Đang đăng nhập");
                    GameManager.Server.DoLogin();
                }
            }
            else if (Common.IsRelease)
            {
                if (StoreGame.Contains(StoreGame.EType.SAVE_USERNAME) && StoreGame.Contains(StoreGame.EType.SAVE_PASSWORD))
                {
                    if (!string.IsNullOrEmpty(StoreGame.LoadString(StoreGame.EType.SAVE_USERNAME)) && !string.IsNullOrEmpty(StoreGame.LoadString(StoreGame.EType.SAVE_PASSWORD)))
                    {
                        lableUsername.value = StoreGame.LoadString(StoreGame.EType.SAVE_USERNAME);
                        lablePassword.value = StoreGame.LoadString(StoreGame.EType.SAVE_PASSWORD);
                    }
                }
            }
        }
        AudioManager.Instance.PlayBackground();


    }
    public void LoadListGameLogo()
    {
        if (!isLoadedListGame)
        {
            if (GameManager.Instance.ListGameLogo.Count > 0)
            {
                isLoadedListGame = true;
                foreach (GameLogoModel model in GameManager.Instance.ListGameLogo)
                {
                    GameLogo.Create(model, listGameLogo.transform);
                }
            }
        }
    }
    IEnumerator _LoginInWebPlayer()
    {
        Debug.Log("===> Call Login web");
        string accessToken = "";
        while (cookie.Length == 0)
            yield return new WaitForEndOfFrame();
        Debug.Log("===> End Call Login web cookie:"+cookie);
        string[] array = cookie.Split(';');

        Array.ForEach(array, c => { if (c.Contains("accessToken")) { accessToken = c; } });

        if (accessToken.Length > 0)
        {
            while (string.IsNullOrEmpty(CServer.HOST_NAME))
                yield return new WaitForEndOfFrame();
            string[] arrayAccessToken = accessToken.Split('=');
			hasAccessToken = true;

            GameManager.Server.DoLogin(arrayAccessToken[1], GameManager.Instance.deviceToken);
            if (cbSavePass.value)
                StoreGame.SaveString(StoreGame.EType.SAVE_ACCESSTOKEN, arrayAccessToken[1]);
        }
        else
        {
            //NotificationView.ShowConfirm("Lỗi", "Bạn chưa đăng nhập vào hệ thống , vui lòng vào trang chủ đăng nhập sau đó quay lại chơi game", delegate() { Application.OpenURL(ServerWeb.URL_BASE); }, delegate() { Application.OpenURL(ServerWeb.URL_BASE); });
        }

    }


    void OnClickButtonForgot(GameObject go)
    {
        ForgotPasswordView.Create();
    }
    
    void OnHideRegisterForm(GameObject targetObject)
    {
        if (!isFormRegisterHide)
            HideFormRegister();
    }

    void OnClickLogo(GameObject go)
    {
       Application.OpenURL("http://chieuvuong.com/");
    }

    void OnClickCalling(GameObject go)
    {
#if UNITY_IPHONE || UNITY_ANDROID
        Application.OpenURL(GameManager.Setting.Platform.GetConfigByType(PlatformType.support_phone).Value);
#endif
    }

    void OnClickButtonRegister(GameObject go)
    {
        if (!string.IsNullOrEmpty(GameManager.Setting.IsMustUpdate))
        {
            Common.MustUpdateGame();
            return;
        }
        if (isFormRegisterHide)
            ShowFormRegister();
    }
    public void ShowFormRegister()
    {
        isFormRegisterHide = false;
        Hashtable hash = iTween.Hash("islocal", true, "time", 1f, "position", Vector3.zero);
        formRegister.transform.localPosition = new Vector3(440f, 0f, -1f);
        btnHideRegisterForm.transform.localPosition = new Vector3(-100f, 0f, -1f);
        formLogin.gameObject.SetActive(false);
        formRegister.gameObject.SetActive(true);
        btnHideRegisterForm.gameObject.SetActive(true);

        iTween.MoveTo(formRegister.gameObject, hash);
        iTween.MoveTo(btnHideRegisterForm.gameObject, hash);
        //GameManager.Instance.FunctionDelay(delegate() 
        //{
          //  formRegister.GetComponent<RegisterView>().iUsername.isSelected = true;
        //},1f);
    }
    public void HideFormRegister() 
    {
        isFormRegisterHide = true;
        Hashtable hash = iTween.Hash("islocal", true, "time", 1f, "position", new Vector3(440f, 0f, -1f));
        Hashtable hashBtn = iTween.Hash("islocal", true, "time", 1f, "position", new Vector3(-100f, 0f, -1f));
        iTween.MoveTo(formRegister.gameObject, hash);
        iTween.MoveTo(btnHideRegisterForm.gameObject, hashBtn);
        GameManager.Instance.FunctionDelay(delegate() 
        {
            formLogin.gameObject.SetActive(true);
            formRegister.gameObject.SetActive(false);
            btnHideRegisterForm.gameObject.SetActive(false);
        }, 1f);
    }
    void OnLogin(LoginResponse response)
    {
        if (response.Successful)
        {
			ServerWeb.StartThread(ServerWeb.URL_REQUEST_USER,new object[]{"username",GameManager.Instance.mInfo.username},delegate(bool isDone, WWW res, IDictionary json) {

			});
            GameManager.Instance.mInfo.password = lablePassword.value;
            GameManager.Server.DoJoinRoom(GameManager.Instance.hallRoom.zoneId, GameManager.Instance.hallRoom.roomId);

#if UNITY_WEBPLAYER
            if (!hasAccessToken)
                Application.ExternalEval("ajaxLoginUnity(\"" + lableUsername.text + "\", \"" + lablePassword.text + "\");");
#endif
        }else
		{
			IsClickButtonLogin = false;
			string message = "Thông tin đăng nhập không hợp lệ. Yêu cầu nhập lại thông tin truy cập.";
			if(response.EsObject.variableExists("reason")){
				message = response.EsObject.getString("reason");
			}
			NotificationView.ShowMessage(message);
		}
    }

    void OnAfterJoinRoom(JoinRoomEvent e)
    {
        if (cbSavePass.value)
        {
            if (IsClickButtonLogin)
            {
                StoreGame.SaveString(StoreGame.EType.SAVE_USERNAME, lableUsername.value);
                StoreGame.SaveString(StoreGame.EType.SAVE_PASSWORD, lablePassword.value);
            }
        }
        //else
        //{
        //    StoreGame.Remove(StoreGame.EType.SAVE_ACCESSTOKEN);
        //    StoreGame.Remove(StoreGame.EType.SAVE_USERNAME);
        //    StoreGame.SaveString(StoreGame.EType.SAVE_PASSWORD, lablePassword.text);
        //}
        //OK Next Scene

        if (GameManager.Instance.selectedLobby.gameId != 0)
        {
            return;
        }
        WaitingView.Hide();
        NotificationView.Instance.HideMessageView();
        GameManager.LoadScene(ESceneName.HallSceen);
    }
    void OnClickAds(GameObject go)
    {
        Announcement ads = GameManager.Instance.ListAnnouncement.Find(a => a.show == Announcement.Scene.login && a.type == Announcement.Type.Advertisement);
        if (ads != null)
            ads.OpenUrl();
    }
    public void UpdateCookieString()
    {
        Application.ExternalEval("if (typeof window.getCookie != \"function\") { window.getCookie = function () {u.getUnity().SendMessage(\"" + gameObject.name + "\", \"OnGetCookie\", document.cookie);}}");
        //Application.ExternalEval("var unity = document.getElementById(\"unityPlayer\"); console.log(unity.SendMessage); ");
        Application.ExternalCall("getCookie");
    }

    void OnGetCookie(string cookie)
    {
        this.cookie = cookie;
    }
}

