using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;

public class UIPopupManager
{
    public class PopupStackInfo
    {
        public enum PopupState
        {
            None,
            Highest,
            Below // not top
        }

        private LinkedList<UIPopupBase> UIPopups = new LinkedList<UIPopupBase>();
        public bool isModal { get; private set; }
        public PopupState state { get; private set; } = PopupState.None;

        public PopupStackInfo(bool _isModal = true)
        {
            isModal = _isModal;
        }

        public UIPopupBase Pop()
        {
            if (UIPopups.Count == 0)
            {
                return null;
            }
            UIPopupBase popup = UIPopups.Last.Value;
            UIPopups.RemoveLast();
            return popup;
        }

        public void Push(UIPopupBase popup)
        {
            UIPopups.AddLast(popup);
        }

        public UIPopupBase Peek()
        {
            return UIPopups.Last.Value;
        }

        public UIPopupBase Peek(ePopupType popupType)
        {
            IEnumerator e = UIPopups.GetEnumerator();
            while (e.MoveNext())
            {
                UIPopupBase popup = (UIPopupBase)e.Current;
                if (popup.CurrentType == popupType)
                {
                    return popup;
                }
            }
            return null;
        }

        public List<UIPopupBase> GetPopupList()
        {
            List<UIPopupBase> popupList = new List<UIPopupBase>();
            IEnumerator e = UIPopups.GetEnumerator();
            while (e.MoveNext())
            {
                UIPopupBase popup = (UIPopupBase)e.Current;
                popupList.Add(popup);
            }
            return popupList;
        }

        public void Remove(UIPopupBase popup)
        {
            if (UIPopups.Contains(popup))
                UIPopups.Remove(popup);
        }

        public int Count()
        {
            return UIPopups.Count;
        }

        public void UpdateState(PopupState popupState)
        {
            if (state != popupState)
            {
                state = popupState;
                List<UIPopupBase> popups = GetPopupList();
                switch (state)
                {
                    case PopupState.None:
                        break;
                    case PopupState.Highest:
                        foreach (UIPopupBase popup in popups)
                        {
                            popup.OnHighest();
                        }
                        break;
                    case PopupState.Below:
                        foreach (UIPopupBase popup in popups)
                        {
                            popup.OnBelow();
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

    private bool popupWorkingFlag = false;
    private Queue<System.Action> popupWorkingQueue = new Queue<System.Action>();
    private LinkedList<PopupStackInfo> popupStack = new LinkedList<PopupStackInfo>();

    private bool topMost_PopupWorkingFlag = false;
    private Queue<System.Action> topMost_PopupWorkingQueue = new Queue<System.Action>();
    private LinkedList<PopupStackInfo> topMost_PopupStack = new LinkedList<PopupStackInfo>();

    #region Root Canvas Popup
    public void ShowPopupUI<T>(ePopupType ePopupType, UIData uiData = null, bool isModal = true) where T : UIPopupBase
    {
        if (popupWorkingFlag == false)
        {
            DoShowPopupUI<T>(ePopupType, uiData, isModal);
        }
        else
        {
            popupWorkingQueue.Enqueue(() => DoShowPopupUI<T>(ePopupType, uiData, isModal));
        }
    }

    private void DoShowPopupUI<T>(ePopupType ePopupType, UIData uiData = null, bool isModal = true) where T : UIPopupBase
    {
        bool CanShowPopup = true;

        UnityEngine.Object prefab = ResInstance.Instance.GetObject(PopupPath[ePopupType]);
        if (prefab == null || GetPopup<T>() != null || ShowPopupException(ePopupType))
        {
            CanShowPopup = false;
        }

        if (CanShowPopup)
        {
            popupWorkingFlag = true; // 동기화 플래그

            UIPopupBase _base = null;
            GameObject _instance = GameObject.Instantiate(prefab, UIRootCanvas.transform) as GameObject;
            if (_instance != null)
            {
                _base = _instance.GetComponent<T>();
            }

            if (isModal)
            {
                PopupStackInfo popupStackInfo = new PopupStackInfo();
                popupStackInfo.Push(_base);
                popupStack.AddLast(popupStackInfo);
                if (popupStack.Last.Previous != null)
                    popupStack.Last.Previous.Value.UpdateState(PopupStackInfo.PopupState.Below);
            }
            else
            {
                if (popupStack.Count == 0 || popupStack.Last.Value.isModal == true)
                {
                    PopupStackInfo popupStackInfo = new PopupStackInfo(false);
                    popupStackInfo.Push(_base);
                    popupStack.AddLast(popupStackInfo);
                    if (popupStack.Last.Previous != null)
                        popupStack.Last.Previous.Value.UpdateState(PopupStackInfo.PopupState.Below);
                }
                else if (popupStack.Last.Value.isModal == false)
                {
                    popupStack.Last.Value.Push(_base);
                }
            }

            _base.CurrentType = ePopupType;
            if (uiData == null)
            {
                _base.Init();
            }
            else
            {
                _base.Init(uiData);
            }

            _base.Show();
            _base.OnHighest();

            DoSetPlayerCanMove();

            LogFileWriter.Log($"{ePopupType}", LogFileWriter.LogType.UI_OPEN);
        }

        DoWorkNextPopupAction();
    }

    public void ClosePopupUI()
    {
        if (false == popupWorkingFlag)
        {
            DoClosePopupUI();
        }
        else
        {
            popupWorkingQueue.Enqueue(() => DoClosePopupUI());
        }
    }

    public void ClosePopupUI<T>() where T : UIPopupBase
    {
        if (false == popupWorkingFlag)
        {
            DoClosePopupUI<T>();
        }
        else
        {
            popupWorkingQueue.Enqueue(() => DoClosePopupUI<T>());
        }
    }

    private void DoClosePopupUI()
    {
        bool CanClosePopup = true;

        if (popupStack.Count == 0)
        {
            CanClosePopup = false;
        }
        if (CanClosePopup)
        {
            popupWorkingFlag = true;

            PopupStackInfo info = popupStack.Last.Value;
            if (info.isModal)
            {
                UIPopupBase popup = info.Pop();
                popup.Release();
                LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                Destroy(popup.gameObject);
                popupStack.RemoveLast();
                if (popupStack.Count != 0)
                    popupStack.Last.Value.UpdateState(PopupStackInfo.PopupState.Highest);
            }
            else
            {
                UIPopupBase popup = info.Pop();
                popup.Release();
                LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                Destroy(popup.gameObject);
                if (info.Count() == 0)
                {
                    popupStack.RemoveLast();
                    if (popupStack.Count != 0)
                        popupStack.Last.Value.UpdateState(PopupStackInfo.PopupState.Highest);
                }
            }
            DoSetPlayerCanMove();
        }

        DoWorkNextPopupAction();
    }

    private void DoClosePopupUI<T>() where T : UIPopupBase
    {
        bool CanClosePopup = true;

        if (popupStack.Count == 0)
        {
            CanClosePopup = false;
        }

        if (CanClosePopup)
        {
            popupWorkingFlag = true;

            bool isFind = false;
            IEnumerator e = popupStack.GetEnumerator();
            while (e.MoveNext())
            {
                PopupStackInfo info = (PopupStackInfo)e.Current;
                bool isLastPopup = info == popupStack.Last.Value;
                var popupList = info.GetPopupList();
                foreach (UIPopupBase popup in popupList)
                {
                    if (popup as T != null)
                    {
                        isFind = true;
                        popup.Release();
                        LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                        Destroy(popup.gameObject);
                        info.Remove(popup);
                        if (info.Count() == 0)
                        {
                            popupStack.Remove(info);
                        }
                        if (isLastPopup && popupStack.Count != 0)
                        {
                            popupStack.Last.Value.UpdateState(PopupStackInfo.PopupState.Highest);
                        }
                        break;
                    }
                }

                if (isFind)
                    break;
            }
            DoSetPlayerCanMove();
        }

        DoWorkNextPopupAction();
    }

    public void ExitPopupUI()
    {
        if (false == popupWorkingFlag)
        {
            DoExitPopupUI();
        }
        else
        {
            popupWorkingQueue.Enqueue(() => DoExitPopupUI());
        }
    }

    private void DoExitPopupUI()
    {
        popupWorkingFlag = true;

        foreach (PopupStackInfo stackInfo in popupStack)
        {
            while (stackInfo.Count() > 0)
            {
                UIPopupBase popup = stackInfo.Pop();
                popup.Release();
                LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                Destroy(popup.gameObject);
            }
        }
        popupStack.Clear();
        DoSetPlayerCanMove();
        DoWorkNextPopupAction();
    }

    private void DoWorkNextPopupAction()
    {
        if (popupWorkingQueue.Count != 0)
        {
            popupWorkingQueue.Dequeue()?.Invoke();
        }
        else
        {
            popupWorkingFlag = false;
        }
    }

    public T GetPopup<T>() where T : UIPopupBase
    {
        if (popupStack.Count == 0)
            return null;

        IEnumerator e = popupStack.GetEnumerator();
        while (e.MoveNext())
        {
            PopupStackInfo info = (PopupStackInfo)e.Current;
            var popupList = info.GetPopupList();
            foreach (UIPopupBase popup in popupList)
            {
                if (popup as T != null)
                {
                    return (T)popup;
                }
            }
        }
        return null;
    }

    public int GetPopupCount()
    {
        int count = 0;
        foreach (var info in popupStack)
        {
            count += info.GetPopupList().Count;
        }
        return count;
    }

    public void ClosePopupByBackButton()
    {
        if (popupStack.Count == 0)
            return;
        else
        {
            var info = popupStack.Last.Value;
            if (info.Peek().CanCloseByBackButton == true)
            {
                info.Peek().ExitPopup();
            }
        }
    }

    public void PostPopupNotification(ePopupNotificationType popupNotificationType)
    {
        foreach (PopupStackInfo info in popupStack)
        {
            foreach (var popup in info.GetPopupList())
            {
                popup.OnNotification(popupNotificationType);
            }
        }
    }

    private void DoSetPlayerCanMove()
    {
        if (GameInstance.Instance.MyPlayerController != null)
        {
            foreach (var popupInfo in popupStack)
            {
                if (popupInfo.isModal)
                {
                    GameInstance.Instance.MyPlayerController.bCanMove = false;
                    return;
                }
            }
            GameInstance.Instance.MyPlayerController.bCanMove = true;
        }
    }
    #endregion


    #region Top Most Canvas Popup
    public void ShowPopupUITopMost<T>(ePopupType ePopupType, UIData uiData = null, bool isModal = true) where T : UIPopupBase
    {
        if (topMost_PopupWorkingFlag == false)
        {
            DoShowPopupUITopMost<T>(ePopupType, uiData, isModal);
        }
        else
        {
            topMost_PopupWorkingQueue.Enqueue(() => DoShowPopupUITopMost<T>(ePopupType, uiData, isModal));
        }
    }

    private void DoShowPopupUITopMost<T>(ePopupType ePopupType, UIData uiData = null, bool isModal = true) where T : UIPopupBase
    {
        bool CanShowPopup = true;

        UnityEngine.Object prefab = ResInstance.Instance.GetObject(PopupPath[ePopupType]);
        if (prefab == null || GetPopupTopMost<T>() != null || ShowPopupException(ePopupType))
        {
            CanShowPopup = false;
        }

        if (CanShowPopup)
        {
            topMost_PopupWorkingFlag = true; // 동기화 플래그

            UIPopupBase _base = null;
            GameObject _instance = GameObject.Instantiate(prefab, TopMostCanvas.transform) as GameObject;
            if (_instance != null)
            {
                _base = _instance.GetComponent<T>();
            }

            if (isModal)
            {
                PopupStackInfo popupStackInfo = new PopupStackInfo();
                popupStackInfo.Push(_base);
                topMost_PopupStack.AddLast(popupStackInfo);
                if (topMost_PopupStack.Last.Previous != null)
                    topMost_PopupStack.Last.Previous.Value.UpdateState(PopupStackInfo.PopupState.Below);
            }
            else
            {
                if (topMost_PopupStack.Count == 0 || topMost_PopupStack.Last.Value.isModal == true)
                {
                    PopupStackInfo popupStackInfo = new PopupStackInfo(false);
                    popupStackInfo.Push(_base);
                    topMost_PopupStack.AddLast(popupStackInfo);
                    if (topMost_PopupStack.Last.Previous != null)
                        topMost_PopupStack.Last.Previous.Value.UpdateState(PopupStackInfo.PopupState.Below);
                }
                else if (topMost_PopupStack.Last.Value.isModal == false)
                {
                    topMost_PopupStack.Last.Value.Push(_base);
                }
            }

            _base.CurrentType = ePopupType;
            if (uiData == null)
            {
                _base.Init();
            }
            else
            {
                _base.Init(uiData);
            }

            _base.Show();
            _base.OnHighest();

            LogFileWriter.Log($"{ePopupType}", LogFileWriter.LogType.UI_OPEN);
        }

        DoWorkNextPopupActionTopMost();
    }

    public void ClosePopupUITopMost()
    {
        if (false == topMost_PopupWorkingFlag)
        {
            DoClosePopupUITopMost();
        }
        else
        {
            topMost_PopupWorkingQueue.Enqueue(() => DoClosePopupUITopMost());
        }
    }

    public void ClosePopupUITopMost<T>() where T : UIPopupBase
    {
        if (false == topMost_PopupWorkingFlag)
        {
            DoClosePopupUITopMost<T>();
        }
        else
        {
            topMost_PopupWorkingQueue.Enqueue(() => DoClosePopupUITopMost<T>());
        }
    }

    private void DoClosePopupUITopMost()
    {
        bool CanClosePopup = true;

        if (topMost_PopupStack.Count == 0)
        {
            CanClosePopup = false;
        }
        if (CanClosePopup)
        {
            topMost_PopupWorkingFlag = true;

            PopupStackInfo info = topMost_PopupStack.Last.Value;
            if (info.isModal)
            {
                UIPopupBase popup = info.Pop();
                popup.Release();
                LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                Destroy(popup.gameObject);
                topMost_PopupStack.RemoveLast();
                if (topMost_PopupStack.Count != 0)
                    topMost_PopupStack.Last.Value.UpdateState(PopupStackInfo.PopupState.Highest);
            }
            else
            {
                UIPopupBase popup = info.Pop();
                popup.Release();
                LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                Destroy(popup.gameObject);
                if (info.Count() == 0)
                {
                    topMost_PopupStack.RemoveLast();
                    if (topMost_PopupStack.Count != 0)
                        topMost_PopupStack.Last.Value.UpdateState(PopupStackInfo.PopupState.Highest);
                }
            }
        }

        DoWorkNextPopupActionTopMost();
    }

    private void DoClosePopupUITopMost<T>() where T : UIPopupBase
    {
        bool CanClosePopup = true;

        if (topMost_PopupStack.Count == 0)
        {
            CanClosePopup = false;
        }

        if (CanClosePopup)
        {
            topMost_PopupWorkingFlag = true;

            bool isFind = false;
            IEnumerator e = topMost_PopupStack.GetEnumerator();
            while (e.MoveNext())
            {
                PopupStackInfo info = (PopupStackInfo)e.Current;
                bool isLastPopup = info == topMost_PopupStack.Last.Value;
                var popupList = info.GetPopupList();
                foreach (UIPopupBase popup in popupList)
                {
                    if (popup as T != null)
                    {
                        isFind = true;
                        popup.Release();
                        LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                        Destroy(popup.gameObject);
                        info.Remove(popup);
                        if (info.Count() == 0)
                        {
                            topMost_PopupStack.Remove(info);
                        }
                        if (isLastPopup && popupStack.Count != 0)
                        {
                            topMost_PopupStack.Last.Value.UpdateState(PopupStackInfo.PopupState.Highest);
                        }
                        break;
                    }
                }

                if (isFind)
                    break;
            }
        }

        DoWorkNextPopupActionTopMost();
    }

    public void ExitPopupUITopMost()
    {
        if (false == topMost_PopupWorkingFlag)
        {
            DoExitPopupUITopMost();
        }
        else
        {
            topMost_PopupWorkingQueue.Enqueue(() => DoExitPopupUITopMost());
        }
    }

    private void DoExitPopupUITopMost()
    {
        topMost_PopupWorkingFlag = true;

        foreach (PopupStackInfo stackInfo in topMost_PopupStack)
        {
            while (stackInfo.Count() > 0)
            {
                UIPopupBase popup = stackInfo.Pop();
                popup.Release();
                LogFileWriter.Log($"{popup.gameObject.name}", LogFileWriter.LogType.UI_CLOSE);
                Destroy(popup.gameObject);
            }
        }
        topMost_PopupStack.Clear();
        DoWorkNextPopupActionTopMost();
    }

    private void DoWorkNextPopupActionTopMost()
    {
        if (topMost_PopupWorkingQueue.Count != 0)
        {
            topMost_PopupWorkingQueue.Dequeue()?.Invoke();
        }
        else
        {
            topMost_PopupWorkingFlag = false;
        }
    }

    public T GetPopupTopMost<T>() where T : UIPopupBase
    {
        if (topMost_PopupStack.Count == 0)
            return null;

        IEnumerator e = topMost_PopupStack.GetEnumerator();
        while (e.MoveNext())
        {
            PopupStackInfo info = (PopupStackInfo)e.Current;
            var popupList = info.GetPopupList();
            foreach (UIPopupBase popup in popupList)
            {
                if (popup as T != null)
                {
                    return (T)popup;
                }
            }
        }
        return null;
    }

    public int GetPopupCountTopMost()
    {
        int count = 0;
        foreach (var info in topMost_PopupStack)
        {
            count += info.GetPopupList().Count;
        }
        return count;
    }

    public void PostPopupNotificationTopMost(ePopupNotificationType popupNotificationType)
    {
        foreach (PopupStackInfo info in topMost_PopupStack)
        {
            foreach (var popup in info.GetPopupList())
            {
                popup.OnNotification(popupNotificationType);
            }
        }
    }
    #endregion
}

public class UIPopupBase : UIBase
{
    protected bool canCloseByBackButton = true;
    public bool CanCloseByBackButton
    {
        get { return canCloseByBackButton; }
    }
    protected ePopupType MyCurrentType;
    public ePopupType CurrentType
    {
        get { return MyCurrentType; }
        set { MyCurrentType = value; }
    }

    public virtual void ExitPopup()
    {
        UIPopupManager.Instance.ClosePopupUI();
    }

    public virtual void OnHighest()
    {
        Debug.Log(System.Enum.GetName(typeof(ePopupType), MyCurrentType) + " Popup Is On Top.");
    }

    public virtual void OnBelow()
    {
        Debug.Log(System.Enum.GetName(typeof(ePopupType), MyCurrentType) + " Popup Is Not Top.");
    }

    public virtual void OnNotification(ePopupNotificationType popupNotificationType)
    {
        Debug.Log(System.Enum.GetName(typeof(ePopupNotificationType), popupNotificationType) + " Popup notification invoked.");
    }
}