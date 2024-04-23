/*===========================================================================*/
/* @brief:      资源缓存脚本,用来索引资源和记录基础信息                      */
/* @details:    由GameObject Pool管理的GameObject上必须挂上本脚本            */
/* @author:     程序猿°                                                     */
/* @data:       2023/5/18                                                    */
/*===========================================================================*/
using UnityEngine;

public class CPooledGameObjectScript
{
    [System.NonSerialized] public string m_prefabPath;
    [System.NonSerialized] public bool m_isInit;
    [System.NonSerialized] public Vector3 m_defaultScale;
    [System.NonSerialized] public Vector3 m_defaultPosition;

    // 是否正在被使用
    [System.NonSerialized] public bool m_inUse;
    // 缓存的GameObj
    [System.NonSerialized] public GameObject Go;
    // GameObj的实例ID
    [System.NonSerialized] public int m_gameObjectInstanceID;
    // 缓存的Transform;
    [System.NonSerialized] public Transform Trans;
    // 缓存的拖尾 - 示例
    [System.NonSerialized] public TrailRenderer[] trailRenderer;

    //----------------------------------------------
    /// 初始化
    /// @prefabKey
    //----------------------------------------------
    public void Initialize(string prefabPath, GameObject go)
    {
        m_prefabPath = prefabPath;
        m_defaultScale = go.transform.localScale;
        m_defaultPosition = go.transform.localPosition;
        m_isInit = true;
        m_inUse = false;

        // 初始化缓存的组件
        Go = go;
        m_gameObjectInstanceID = go.GetInstanceID();
        Trans = go.transform;

        trailRenderer = go.GetComponentsInChildren<TrailRenderer>();
    }

    public void SetGameObjLayerRecursively(GameObject go, int layer, string tag)
    {
        if (go.CompareTag(tag) == false)
        {
            go.layer = layer;
        }

        Transform trans = go.transform;
        int count = trans.childCount;

        for (int i = 0; i < count; ++i)
        {
            SetGameObjLayerRecursively(trans.GetChild(i).gameObject, layer, tag);
        }
    }

    public void HideGameObjWithNoTag(string tag)
    {
        if (Go != null)
        {
            int layer = LayerMask.NameToLayer("Hide");
            SetGameObjLayerRecursively(Go, layer, tag);
        }
    }

    //----------------------------------------------
    /// GameObject第一次被创建的时候被调用
    //----------------------------------------------
    public void OnCreate()
    {
       
    }

    //----------------------------------------------
    /// 每次从GameObjectPool中返回GameObject的时候被调用
    //----------------------------------------------
    public void OnGet()
    {
        //Handle GameObject
        if (Go && !Go.activeSelf)
        {
            Go.SetActive(true);
        }

        m_inUse = true;
    }

    //----------------------------------------------
    /// 每次GameObject被回收的时候被调用
    //----------------------------------------------
    public void OnRecycle()
    {
        //Handle GameObject
        if (Go && Go.activeSelf)
        {
            Go.SetActive(false);
        }

        if (trailRenderer != null)
        {
            foreach (var render in trailRenderer)
            {
                render.Clear();
            }
        }

        m_inUse = false;
    }

    //----------------------------------------------
    /// GameObject预加载的时候被调用
    //----------------------------------------------
    public void OnPrepare()
    {
        Go.SetActive(false);
    }
};
