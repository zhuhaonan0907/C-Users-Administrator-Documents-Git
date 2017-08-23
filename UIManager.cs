using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

namespace UISystem
{
    /// <summary>
    /// UI类型枚举
    /// </summary>
    public enum eUIType
    {
        None = 0,
        /// <summary>
        /// 最底层基础UI   特殊功能块使用,在所有UI最下层
        /// </summary>
        BaseUI = 1,
        /// <summary>
        /// 堆栈UI    用于功能块
        /// </summary>
        StackUI = 2000,
        /// <summary>
        /// 叠加类型UI   用于通知类型窗口
        /// </summary>
        SuperpositionUI = 4000,
        /// <summary>
        /// 遮罩UI   遮挡UI点击事件
        /// </summary>
        MaskUI = 6000,
        /// <summary>
        /// 最顶层UI    用于信息显示类型UI,例如跑马灯,等待提示
        /// </summary>
        TopUI = 8000,
    }

    /// <summary>
    /// UI管理器类
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {

        private UIManager() { }
        public GameObject _mUIRoot = null; //UI根节点

        private Dictionary<string, UIBase> _mdicUIUnderRoot = new Dictionary<string, UIBase>();  //根节点下UI管理器

        private List<string> _StackUIList = new List<string>();  //堆栈UI

        private List<UIBase> _SuperpositionUIList = new List<UIBase>(); // 叠加UI


        private Dictionary<string, GameObject> _CommonUIList = new Dictionary<string, GameObject>();  //通用小UI资源名寄存器

        public GameObject PanelWindow;

        //UI摄像机
        public Camera mUICamera = null;
        //UI摄像机
        public Camera mUIEffCamera = null;

        public Vector2 mReferenceResolution = new Vector2(1280, 720);

        /// <summary>
        /// 已经打开的UIlist
        /// </summary>
        public List<UIBase> _OpenUiList = new List<UIBase>();

        //下层含有render的对象数组
        private Renderer[] mStartRenders;
        /// <summary>
        /// UI结束事件
        /// </summary>
        public UnityAction OnUICloseEvent;

        public void init(Vector2 Resolution)
        {
            mReferenceResolution = Resolution;
            if (_mUIRoot == null)
            {
                //保证场景中只有一个UIRoot下的EventSystem
                EventSystem[] evsys = Object.FindObjectsOfType<EventSystem>();
                foreach (EventSystem ev in evsys)
                {
                    Object.DestroyImmediate(ev.gameObject);
                }
                _mUIRoot = Object.Instantiate(Resources.Load("local/ui/UIRoot")) as GameObject;
                _mUIRoot.name = "UIRoot";
                Object.DontDestroyOnLoad(_mUIRoot);
                mUICamera = _mUIRoot.transform.Find("UICamera").GetComponent<Camera>();
                mUICamera.depth = 2;

                mUIEffCamera = _mUIRoot.transform.Find("EffectCamera").GetComponent<Camera>();

                PanelWindow = _mUIRoot.transform.Find("PanelWindow").gameObject;
                var _mCanvas = PanelWindow.GetComponent<Canvas>();
                var _mCanvasScaler = PanelWindow.GetComponent<CanvasScaler>();
                SetUICompent(_mCanvas, _mCanvasScaler);
                _mCanvas.sortingOrder = GetCanvasOder(eUIType.MaskUI);
            }
        }

        public void SetUICompent(Canvas _mCanvas, CanvasScaler _mCanvasScaler)
        {

            if (_mCanvas != null)
            {
                _mCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                _mCanvas.worldCamera = MainManager.UI.mUICamera;
                _mCanvas.targetDisplay = MainManager.UI.mUICamera.targetDisplay;
                _mCanvas.planeDistance = MainManager.UI.mUICamera.farClipPlane * 0.5f;
                _mCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _mCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _mCanvasScaler.matchWidthOrHeight = 0;
                _mCanvasScaler.referenceResolution = MainManager.UI.mReferenceResolution;
                _mCanvas.pixelPerfect = false;
            }
        }


        /// <summary>
        /// 加载UI资源
        /// </summary>
        private void addUIToRoot(string perfabName, string className)
        {
            System.Type uiType = System.Type.GetType(className);

            if (uiType == null)
            {
                Debug.LogError("加载UI不存在类型名：" + className);
                return;
            }

            UIBase uiobj = null;
            _mdicUIUnderRoot.TryGetValue(uiType.Name, out uiobj);

            if (uiobj == null)
            {
                GameObject obj = new GameObject(className);

                obj.AddComponent<RectTransform>();
                var uiBase = obj.AddComponent(uiType);

                obj.transform.SetParent(_mUIRoot.transform);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localScale = Vector3.one;

                var compents = obj.GetComponent<UIBase>();
                if (compents != null)
                {
                    //保存基础信息
                    _registUI(perfabName, compents);
                    compents.uiTypeName = perfabName;

                }

                ResourceManager.Load(perfabName, _OnLoadedUIAddToRoot, BundleType.UI, uiBase);
            }
        }

        /// <summary>
        /// 得到当前类型的UI对象
        /// </summary>
        public T GetTargetUI<T>() where T: UIBase
        {
            UIBase ui = null;
            _mdicUIUnderRoot.TryGetValue(typeof(T).Name, out ui);

            if (ui==null)
            {
                UTLog.Error("不存在UI："+ typeof(T).Name);
                return null;
            }

            if (!ui.bShowUILoaded)
            {
                UTLog.Error("UI未创建完成：" + typeof(T).Name);
                return null;
            }

            return ui as T;
        }

        public void SetUIActive(UIBase rUI, bool rActive)
        {
            rUI.isActive = rActive;
            rUI.gameObject.SetActive(rActive);
        }

        public void openUIInRoot<T>(eUIType rUIType = eUIType.None, bool isSwitchDes = true) where T : UIBase, new()
        {
            openUIInRoot(typeof(T).Name, rUIType, isSwitchDes);
        }
        public void openUIInRoot<T>(string perfabName, eUIType rUIType = eUIType.None, bool isSwitchDes = true) where T : UIBase, new()
        {
            openUIInRoot(typeof(T).Name, rUIType, isSwitchDes, perfabName);
        }

        public void openUIInRoot(string className, eUIType rUIType = eUIType.None, bool isSwitchDes = true, string perfabName = "")
        {
            if (string.IsNullOrEmpty(perfabName))
            {
                perfabName = className;
            }

            UIBase ui = null;
            _mdicUIUnderRoot.TryGetValue(perfabName, out ui);

            if (ui == null)
            {
                addUIToRoot(perfabName, className);
                _mdicUIUnderRoot.TryGetValue(perfabName, out ui); ;
            }
            else
            {

                switch (ui.mUIType)
                {
                    case eUIType.SuperpositionUI:
                        ui.recoverOder();
                       // _SuperpositionUIList.Remove(ui);
                        break;
                    case eUIType.StackUI:
                      //  _StackUIList.Remove(ui.uiTypeName);
                        ui.recoverOder();
                        break;
                    case eUIType.TopUI:
                    case eUIType.BaseUI:
                        ui.recoverOder();
                        break;
                    default:
                        ui.recoverOder();
                        break;
                }

                ui.mUIType = rUIType;
            }



            if (ui != null)
            {

                if (_StackUIList.Contains(ui.uiTypeName))
                {
                    _StackUIList.Remove(ui.uiTypeName);
                }

                if (_SuperpositionUIList.Contains(ui))
                {
                    _SuperpositionUIList.Remove(ui);
                }


                if (rUIType != eUIType.None)
                {
                    ui.mUIType = rUIType;
                }

                ui.isSwichSceneDestroy = isSwitchDes;

               
                //设置UI层级
                ui.freshCanvasOder();

                switch (ui.mUIType)
                {
                    case eUIType.SuperpositionUI://叠加UI
                        if (!_SuperpositionUIList.Contains(ui))
                        {
                            _SuperpositionUIList.Add(ui);
                        }
                        break;
                    case eUIType.StackUI://堆栈UI
                        _StackUIList.Remove(ui.uiTypeName);
                        if (_StackUIList.Count > 0)
                        {
                            UIBase lastUI;
                            _mdicUIUnderRoot.TryGetValue(_StackUIList[_StackUIList.Count - 1], out lastUI);
                            if (lastUI != null)
                            {
                                DesUI(lastUI);
                            }
                        }
                        _StackUIList.Add(ui.uiTypeName);
                        break;
                }


                if (!_OpenUiList.Contains(ui))
                {
                   // UTLog.Log("添加UI"+ ui.GetType());
                    _OpenUiList.Add(ui);
                }

                SetUIActive(ui, true);


                if (ui.refreshFuncCallBack)
                {
                    MessageDispatcher.SendMessage(null, ui.gameObject, "refresh", ui.refreshFuncData);
                    ui.refreshFuncData = null;
                    ui.refreshFuncCallBack = false;
                }
            }
            else
            {
                Debug.LogError("openUI failed! ==" + perfabName + "== not found!");
            }
        }

        /// <summary>
        /// 计算当前层级
        /// </summary>
        public int GetCanvasOder(eUIType rType)
        {
            //计算当前该UI的层级
            int curUIOrder = (int)rType;

            switch (rType)
            {
                case eUIType.SuperpositionUI:

                    int[] canvasSuperArr = new int[_SuperpositionUIList.Count];

                    for (int i = 0; i < _SuperpositionUIList.Count; i++)
                    {
                        canvasSuperArr[i] = _SuperpositionUIList[i]._miUIOder;
                    }

                    if (canvasSuperArr.Length > 0)
                    {
                        curUIOrder = Mathf.Max(canvasSuperArr) + 100;
                    }
                    break;
                case eUIType.StackUI:

                    int[] canvasStackArr = new int[_StackUIList.Count];

                    for (int i = 0; i < _StackUIList.Count; i++)
                    {
                        if (_mdicUIUnderRoot.ContainsKey(_StackUIList[i]))
                        {
                            canvasStackArr[i] = _mdicUIUnderRoot[_StackUIList[i]]._miUIOder;
                        }
                    }

                    if (canvasStackArr.Length > 0)
                    {
                        curUIOrder = Mathf.Max(canvasStackArr) + 100;
                    }
                    break;
            }

            return curUIOrder;
        }

        public void OpenOrRefreshUI<T>(eUIType type = eUIType.StackUI) where T : UIBase, new()
        {
            T ui = _getUIUnderRoot<T>();
            if (ui == null)
            {
                openUIInRoot<T>(type);
                return;
            }
            refreshUI<T>();
        }
        /// <summary>
        /// 调用UI对象的refresh消息
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void refreshUI<T>(object Data = null) where T : UIBase, new()
        {
            T ui = _getUIUnderRoot<T>();
            if (ui == null)
            {
               // UTLog.Error("closeUI failed! ==" + typeof(T).Name + "== not found!");
                return;
            }
            UIBase uiBase = ui as UIBase;
            if (uiBase.bShowUILoaded && uiBase.gameObject.activeInHierarchy)
            {
                MessageDispatcher.SendMessage(null, ui.gameObject, DispatcherName.UI_Refresh_Event, Data);
            }
            else
            {
                uiBase.refreshFuncData = Data;
                uiBase.refreshFuncCallBack = true;
            }
        }

        /// <summary>
        /// 得到最顶层的UI
        /// </summary>
        public UIBase getTopUI()
        {
            int maxOder = 0;
            UIBase topUI = null;
            for (int i = 0; i < _OpenUiList.Count; i++)
            {
                if (_OpenUiList[i] != null && _OpenUiList[i].isActiveAndEnabled && _OpenUiList[i].gameObject.activeInHierarchy
                    && _OpenUiList[i]._mCanvas != null && (_OpenUiList[i].mUIType == eUIType.SuperpositionUI || _OpenUiList[i].mUIType == eUIType.StackUI))
                {
                    if (_OpenUiList[i]._mCanvas.sortingOrder > maxOder)
                    {
                        maxOder = _OpenUiList[i]._mCanvas.sortingOrder;
                        topUI = _OpenUiList[i];
                    }
                }
            }
            return topUI;
        }


        
         
        /// <summary>
        /// 关闭根节点下的UI
        /// </summary>
        public void closeUIInRoot<T>() where T : UIBase, new()
        {
            T ui = _getUIUnderRoot<T>();

          
            if (ui != null)
            {
                //若UI已经关闭则跳过
                if (!ui.isActive)
                {
                    return;
                }

                switch (ui.mUIType)
                {
                    case eUIType.SuperpositionUI://叠加UI

                        ui.recoverOder();
                        _SuperpositionUIList.Remove(ui);
                        break;
                    case eUIType.StackUI://堆栈UI
                        _StackUIList.Remove(ui.uiTypeName);

                        ui.recoverOder();
                        if (_StackUIList.Count > 0)
                        {
                            openUIInRoot(_StackUIList[_StackUIList.Count - 1], eUIType.StackUI);
                        }
                        break;
                    case eUIType.TopUI:
                    case eUIType.BaseUI:

                        ui.recoverOder();
                        break;
                    default:

                        ui.recoverOder();
                        break;
                }

                SetUIActive(ui, false);
                removeUI(ui);

                if (OnUICloseEvent!=null)
                {
                    OnUICloseEvent();
                }
            }
            else
            {
                Debug.Log("closeUI failed! ==" + typeof(T).Name + "== not found!");
            }
        }

        /// <summary>
        /// 急切提示
        /// </summary>
        /// <param name="text"></param>
        public void setHurryPrompt(string text)
        {
            if (!checkTargetUIActive<Page_HurryTemplate>())
                openUIInRoot<Page_HurryTemplate>(eUIType.TopUI);


            if (checkTargetUIActive<Page_HurryTemplate>())
            {
                refreshUI<Page_HurryTemplate>(text);
            }
        }

        /// <summary>
        /// 走马灯
        /// </summary>
        /// <param name="text"></param>
        public void setHorseLamp(HorseLampDATA data)
        {
            if (!checkTargetUIActive<Page_HorseLamp>())
                openUIInRoot<Page_HorseLamp>(eUIType.TopUI);
            refreshUI<Page_HorseLamp>(data);
        }


        /// <summary>
        /// 关闭所有UI
        /// </summary>
        public void closeAllUIInRoot()
        {
            List<KeyValuePair<string, UIBase>> temp = new List<KeyValuePair<string, UIBase>>(_mdicUIUnderRoot);

            for (int i = 0; i < temp.Count; i++)
            {
                UIBase uiobj = temp[i].Value;

                //若UI已经关闭则跳过
                if (!uiobj.isActive)
                {
                    continue;
                }

                if (uiobj.isSwichSceneDestroy)
                {
                    if (_StackUIList.Contains(uiobj.uiTypeName))
                    {
                        _StackUIList.Remove(uiobj.uiTypeName);
                    }
                    if (_SuperpositionUIList.Contains(uiobj))
                    {
                        _SuperpositionUIList.Remove(uiobj);
                    }
                   

                    SetUIActive(uiobj, false);
                    removeUI(uiobj);
                }
            }
        }



        /// <summary>
        /// 获取根节点
        /// </summary>
        public GameObject UIRoot
        {
            get { return _mUIRoot; }
        }


        /// <summary>
        /// 删除根节点下所有的UI
        /// </summary>
        public void removeAllUIUnderRoot()
        {
            _StackUIList.Clear();
            _SuperpositionUIList.Clear();
            //foreach (UIBase uiobj in _mdicUIUnderRoot.Values)
            //{
            //    //removeUI(uiobj);
            //}
            UIBase[] base_ary = new UIBase[_mdicUIUnderRoot.Count];
            _mdicUIUnderRoot.Values.CopyTo(base_ary, 0);
            for (int i = 0; i < base_ary.Length; i++)
            {
                removeUI(base_ary[i]);
            }
            _mdicUIUnderRoot.Clear();
        }

        /// <summary>
        /// 删除根节点下除指定类型的所有的UI
        /// </summary>
        public void removeUIUnderRootExceptType(eUIType type)
        {

            List<KeyValuePair<string, UIBase>> temp = new List<KeyValuePair<string, UIBase>>(_mdicUIUnderRoot);
            _StackUIList.Clear();

            for (int i = 0; i < temp.Count; i++)
            {
                UIBase uiobj = temp[i].Value;
                if (uiobj.mUIType != type)
                {
                    removeUI(uiobj);
                }
            }
        }

        /// <summary>
        /// 移除UI  传入类型名
        /// </summary>
        public void removeUI(string uiName)
        {
            if (_mdicUIUnderRoot.ContainsKey(uiName))
            {
                removeUI(_mdicUIUnderRoot[uiName]);
            }
        }


        /// <summary>
        /// 移除UI
        /// </summary>
        public void removeUI(UIBase ui)
        {
            if (_StackUIList.Contains(ui.uiTypeName))
            {
                _StackUIList.Remove(ui.uiTypeName);
            }
            if (_SuperpositionUIList.Contains(ui))
            {
                _SuperpositionUIList.Remove(ui);
            }

            DesUI(ui);
        }

        private void DesUI(UIBase ui)
        {
            if (_OpenUiList.Contains(ui))
            {
              //  UTLog.Log("删除UI" + ui.GetType());
                _OpenUiList.Remove(ui);
            }


            if (_mdicUIUnderRoot.ContainsValue(ui))
            {
                string uiName = ui.uiTypeName;

                for (int i = 0; i < ui.CommonUIList.Count; ++i)
                {
                    if (ui.CommonUIList[i] != null)
                    {
                        ui.CommonUIList[i].OnAutoRelease();
                    }
                }
                ui.CommonUIList.Clear();
                if (ui.bShowUILoaded)
                {
                    ui.OnAutoRelease();
                }

                ResourceManager.Release(ui.uiTypeName, BundleType.UI);
                Object.DestroyImmediate(ui.gameObject);
                _mdicUIUnderRoot.Remove(uiName);
            }
            else
            {
                ui.OnAutoRelease();
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        /// <summary>
        /// 移除静态UI
        /// </summary>
        public void removeUIFromUIRoot(string uiname)
        {
            if (_mdicUIUnderRoot.ContainsKey(uiname))
            {
                _mdicUIUnderRoot[uiname].OnAutoRelease();
                Object.DestroyImmediate(_mdicUIUnderRoot[uiname].gameObject);
                //ResourceLoadManager.removeRes(_mdicUIUnderRoot[uiname].msPath);
                _mdicUIUnderRoot.Remove(uiname);
            }

        }



        /// <summary>
        /// 清理公共小UI缓存
        /// </summary>
        public void ClearCommonUI()
        {
            foreach (KeyValuePair<string, GameObject> kv in _CommonUIList)
            {
                ResourceManager.Release(kv.Key, BundleType.UI);
            }
            _CommonUIList.Clear();
        }


        /// <summary>
        /// 创建一个UI,但是并不管理该UI的对象,需要得到它的模块自己管理,用于公共动态UI的创建
        /// </summary>
        public T createUI<T>(string path, UIBase uiMount) where T : UIBase, new()
        {
            if (!_CommonUIList.ContainsKey(path))
            {
                UTLog.Error("公共UI:" + path + "未做预加载!!!");
                return null;
            }
            GameObject showuiobj = Object.Instantiate(_CommonUIList[path]) as GameObject;
            T t = showuiobj.AddComponent<T>();
            uiMount.CommonUIList.Add(t);
            showuiobj.name = t.name;
            t.mUIShowObj = showuiobj;
            t._OnLoadedShowUI();
            t.OnAutoLoadedUIObj();

            //ResourceManager.Load(path, _OnLoadedUI, BundleType.UI, t);
            return t;
        }

        public void AddCommonUI(string path, GameObject ui)
        {
            if (!_CommonUIList.ContainsKey(path))
            {
                _CommonUIList.Add(path, ui);
            }
        }

        public T _getUIUnderRoot<T>() where T : UIBase, new()
        {
            UIBase uiobj = null;
            _mdicUIUnderRoot.TryGetValue(typeof(T).Name, out uiobj);
            if (uiobj == null)
            {
                List<UIBase> dataList = new List<UIBase>(_mdicUIUnderRoot.Values);
                for (int i = 0; i < dataList.Count; i++)
                {
                    if (string.Compare(dataList[i].name, typeof(T).Name) == 0)
                    {
                        uiobj = dataList[i];
                        return uiobj as T;
                    }
                }
            }
            return uiobj as T;
        }


        private void _registUI(string uiname, UIBase uiobj)
        {
            removeUIFromUIRoot(uiname);
            _mdicUIUnderRoot.Add(uiname, uiobj);
        }


        //加载ui完成回调
        private void _OnLoadedUIAddToRoot(Object obj, object ob)
        {
            if (obj != null)
            {
                UIBase uibase = (UIBase)ob;
                GameObject uishowobj = GameObject.Instantiate((GameObject)obj);
                uishowobj.name = uibase.uiTypeName + "(Show)";
                UIBase ui = _mdicUIUnderRoot[uibase.uiTypeName];
                GameObject logicUIObj = ui.gameObject;
                ui.mUIShowObj = uishowobj;
                uishowobj.transform.SetParent(logicUIObj.transform);
                ui._OnLoadedShowUI();
                ui.OnAutoLoadedUIObj();
                SetUIActive(ui, false);
            }
            else
            {
                UTLog.Error("值为空");
            }
        }


        /// <summary>
        /// 判断指定UI资源是否打开
        /// </summary>
        public bool checkTargetUIActive<T>() where T : UIBase, new()
        {
            string componentName = typeof(T).Name;
            return checkTargetUIActive(componentName);
        }

        public bool checkTargetUIActive(string typeName)
        {
            if (_mdicUIUnderRoot.ContainsKey(typeName))
            {
                return _mdicUIUnderRoot[typeName].gameObject.activeInHierarchy;
            }
            return false;
        }


        /// <summary>
        /// 当A canvas中带有粒子 需要打开B canvas时 需要动态修改层级  
        /// </summary>
        /// <param name="start_canvas">A对象的canvas</param>
        /// <param name="end_canvas">B对象的canvas</param>
        public void changeSortOrder(int sortingOrder, GameObject particleObj)
        {
            //获取canvas下所有子对象的render
            mStartRenders = particleObj.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < mStartRenders.Length; i++)
            {
                if (mStartRenders[i].gameObject.layer != LayerMask.NameToLayer("UI"))
                {
                    mStartRenders[i].sortingOrder += sortingOrder + 5;
                    mStartRenders[i].gameObject.layer = LayerMask.NameToLayer("UI");
                }
            }
        }


        /// <summary>
        /// 动态创建粒子将粒子放在ui之上或者下面
        /// </summary>
        /// <param name="index">正数在上，负数在下</param>
        /// <param name="parent">在其上添加粒子 （如果该节点上设计上不需要添加canvas，该对象也可以传rootObj进来这里只是为了获取一个sortOder值设置特效）</param>
        /// <param name="rootObj">uibase </param>
        /// <param name="particleObj">粒子对象</param>
        public static void setParticleOrder(int index, GameObject parent, UIBase rootObj, GameObject particleObj)
        {
            Canvas canvas = parent.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = parent.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                parent.AddComponent<GraphicRaycaster>();
                parent.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;
            }


            //动态修改layer
            Transform[] particleTran = particleObj.GetComponentsInChildren<Transform>();

            for (int i = 0; i < particleTran.Length; i++)
            {
                particleTran[i].gameObject.layer = LayerMask.NameToLayer("UI");
            }

            Renderer[] particleRenders = particleObj.GetComponentsInChildren<Renderer>();

            //找到最大层级和最小层级
            int particleMaxOder = 0;
            int particleMinOder = particleRenders[0].sortingOrder;
            for (int i = 0; i < particleRenders.Length; i++)
            {
                System.Array.Sort(particleRenders, (a, b) =>
                {
                    int powerA = a.sortingOrder;
                    int powerB = b.sortingOrder;
                    if (powerA == powerB)
                        return 0;
                    if (powerA < powerB)
                        return -1;
                    else
                        return 1;
                });
                if (particleRenders[i].sortingOrder <= particleMinOder)
                {
                    particleMinOder = particleRenders[i].sortingOrder;
                }
                if (particleRenders[i].sortingOrder >= particleMaxOder)
                {
                    particleMaxOder = particleRenders[i].sortingOrder;
                }
            }
            //加减10 是为了校正美术做特效时  特效中本来就有层的变化
            if (canvas.name == rootObj.name)
            {
                canvas.sortingOrder = rootObj.GetCurrentCanvasOder();
            }
            else
            {
                //加1 针对的是特效夹在ui之间的情况这样使得特效最底层也在ui地板最上面 在指定对象下面(10特效相对层级)
                // if(canvas.sortingOrder == 0)
                {
                    canvas.sortingOrder = rootObj.GetCurrentCanvasOder() + (particleMaxOder - particleMinOder) + 10;
                }
            }
            if (index > 0)
            {
                for (int i = 0; i < particleRenders.Length; i++)
                {
                    //加一是为了防止特效最小值为0的情况
                    particleRenders[i].sortingOrder = canvas.sortingOrder + (particleRenders[i].sortingOrder - particleMinOder + 1);
                }
            }
            else if (index < 0)
            {
                //特效最大值减去当前层级
                for (int i = 0; i < particleRenders.Length; i++)
                {
                    particleRenders[i].sortingOrder = canvas.sortingOrder - (particleMaxOder - particleRenders[i].sortingOrder + 1);
                }
            }
        }

        private static void _OnLoadedUI(Object ob, object o)
        {
            if (ob != null)
            {
                UIBase ui = ((UIBase)o);
                GameObject logicuiobj = ui.gameObject;
                GameObject showuiobj = Object.Instantiate(ob) as GameObject;
                showuiobj.name = logicuiobj.name;
                ui.mUIShowObj = showuiobj;
                showuiobj.transform.SetParent(logicuiobj.transform);
                showuiobj.transform.localPosition = Vector3.zero;
                showuiobj.transform.localScale = Vector3.one;
                ui._OnLoadedShowUI();
                ui.OnAutoLoadedUIObj();
            }
            else
            {
                //Hashtable info = o as Hashtable;
                //UIBase ui = ((KeyValuePair<UIBase, eLoadResPath>)info["procobj"]).Key;
                //GameObject logicuiobj = ui.gameObject;
                //DLoger.LogError(logicuiobj.name + " load failed!");
            }
        }
        public void ShowPopUp(string text, PopUpData.ePopUpType Type = PopUpData.ePopUpType.TypeOK, OnVoidCallBack OKButtonClick = null, OnVoidCallBack CancelButtonClick = null)
        {
            PopUpData data = new PopUpData(text, Type, OKButtonClick, CancelButtonClick);
            ShowPopUp(data);
        }
        public void ShowPopUp(PopUpData popData)
        {
            openUIInRoot<Page_popUp>(eUIType.TopUI);
            refreshUI<Page_popUp>(popData);
        }
        public void HidePopUp()
        {
            closeUIInRoot<Page_popUp>();
        }
        /// <summary>
        /// 数字键盘
        /// </summary>
        /// <param name="parent"> 父节点 决定着出现的位置，一般为点击的按钮</param>
        /// <param name="min">最小数字</param>
        /// <param name="max">最大数字</param>
        /// <param name="showText">实时更改显示的text组件 可不填用刷新函数去更改</param>
        /// <param name="callback">刷新回调 当刷新text组件不能满足时使用</param>
        /// <param name="closeCallback">关闭回调 当刷新text组件不能满足时使用</param>
        /// <param name="type">类型 int float。float有小数点</param>
        //用于简单更改的字符和输入的字符一样
        public void ShowNumKeyBoard(GameObject parent, float min, float max, Text showText, NumerKeyBoardData.EnumNumerType type = NumerKeyBoardData.EnumNumerType.NumerTypeInt)
        {
            ShowNumKeyBoard(parent, min, max, showText, null, null, type);
        }
        //用于复杂一点输入之后会发生格式更改，或者其他组件同步更改是使用
        public void ShowNumKeyBoard(GameObject parent, float min, float max, NumerKeyBoardData.OnRefersh callback = null, NumerKeyBoardData.OnRefersh closeCallback = null, NumerKeyBoardData.EnumNumerType type = NumerKeyBoardData.EnumNumerType.NumerTypeInt)
        {
            ShowNumKeyBoard(parent, min, max, null, callback, closeCallback, type);
        }
        private void ShowNumKeyBoard(GameObject parent, float min, float max, Text showText, NumerKeyBoardData.OnRefersh callback, NumerKeyBoardData.OnRefersh closeCallback, NumerKeyBoardData.EnumNumerType type = NumerKeyBoardData.EnumNumerType.NumerTypeInt)
        {
            MainManager.UI.openUIInRoot<Page_numerKeyBoard>(eUIType.SuperpositionUI);
            NumerKeyBoardData keyBoardData = new NumerKeyBoardData();
            keyBoardData.NumerType = type;
            keyBoardData.CloseCallBack = closeCallback;
            keyBoardData.CallBack = callback;
            keyBoardData.ParentObj = parent;
            keyBoardData.MinNumer = min;
            keyBoardData.MaxNumer = max;
            keyBoardData.ShowNumerString = showText;
            MainManager.UI.refreshUI<Page_numerKeyBoard>(keyBoardData);
        }
        public void OpenUtCardMain()
        {
            UTHttp.getMyUTCard(() =>
            {
                UTHttp.CurTypeStack.Push(UTHttp.eUTHttpType.Main);
                MainManager.UI.openUIInRoot<Page_UTCard_Main>(eUIType.StackUI);
                MainManager.UI.refreshUI<Page_UTCard_Main>();
            });
        }
        public void BackLogin()
        {
            UIDataManager.Instance.IsNewbie = "";
            UIDataManager.Instance.mNewSign = true;
            UIDataManager.Instance.mguideData = null;
            PlayerPrefs.DeleteKey("clintId");
            PlayerPrefs.DeleteKey("openId");
            PlayerPrefs.DeleteKey("token");
            PlayerPrefs.DeleteKey("userId");

            closeAllUIInRoot();
            openUIInRoot<Page_Login>();
            MainManager.Scene.sceneType = GameSceneManager.SceneType.None;
            MainManager.Reconnet.CloseFunc();
            //UserDataManagerClient.Clear();
        }
    }
}



