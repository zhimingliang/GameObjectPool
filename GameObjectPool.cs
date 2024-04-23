/*===========================================================================*/
/* @brief:      GameObj资源对象缓存池(对比spawnPool)                         */
/* @details:    对比spawnPool更轻量,可控性更高                               */
/* @author:     Jamin                                                        */
/* @data:       2023/5/18                                                    */
/*===========================================================================*/

using UnityEngine;
using System.Collections.Generic;

public class GameObjectPool
{
    private static GameObjectPool _instance;

    // Pool中缓存的GameObject表 Key为PrefabFullPath，value为缓存对象
    private Dictionary<string, List<CPooledGameObjectScript>> m_pooledGameObjectMap = new Dictionary<string, List<CPooledGameObjectScript>>();

    //把CPooledGameObjectScript根据objectId进行缓存(包含从池子中获取的，已经在场景中使用的GameObject)
    private Dictionary<int, CPooledGameObjectScript> m_allPooledGameObjectMap = new Dictionary<int, CPooledGameObjectScript>();

    // 最大的资源预加载数量(超过这个数量的资源在局内动态加载 todo: 对极个别的资源支持白名单)
    const int MaxPreloadCount = 50;
    private Transform m_gameObjPoolRoot;

    // 标记是否在战斗中，方便一次性卸载所有战斗内资源
    public bool inGameMark = false;

    public static GameObjectPool Instance
    {
        get
        {
            _instance ??= new GameObjectPool();

            return _instance;
        }
    }

    public GameObjectPool()
    {
        Init();
    }

    public void Init()
    {
        m_gameObjPoolRoot = InitRoot("GameObjPoolRoot");
        Object.DontDestroyOnLoad(m_gameObjPoolRoot);
    }

    private Transform InitRoot(string rootName)
    {
        var go = new GameObject(rootName);

        return go.transform;
    }

    public Dictionary<string, List<CPooledGameObjectScript>> GetPool()
    {
        return m_pooledGameObjectMap;
    }

    /// <summary>
    /// 获取资源缓存表
    /// </summary>
    public Dictionary<int, CPooledGameObjectScript> GetAllPooledGameObjectMap()
    {
        return m_allPooledGameObjectMap;
    }

    /// <summary>
    /// 清理
    /// todo:讲道理EffectFactory和InGameObj的清理启用一个就够了,现在两个都调用是和以往的写法兼容
    /// </summary>
    /// <param name="clearAllPooledData"></param>
    public void ClearPooledObjects(bool clearAllPooledData = false)
    {
        ExecuteClearPooledObjects(clearAllPooledData);
    }

    /// <summary>
    /// 执行清理Pooled GameObjects
    /// </summary>
    /// <param name="clearAllPooledData"></param>
    private void ExecuteClearPooledObjects(bool clearAllPooledData)
    {
        // 执行全部清理,应该从m_allPooledGameObjectMap里面获取Go并清理.
        Dictionary<int, CPooledGameObjectScript>.Enumerator iter = m_allPooledGameObjectMap.GetEnumerator();
        List<int> pooledGameObjectScriptList = new List<int>();
        foreach (var objs in m_allPooledGameObjectMap)
        {
            pooledGameObjectScriptList.Add(objs.Key);
        }

        int count = pooledGameObjectScriptList.Count;
        for (int i = 0; i < count; i++)
        {
            var gpScript = m_allPooledGameObjectMap[pooledGameObjectScriptList[i]];
            m_allPooledGameObjectMap.Remove(pooledGameObjectScriptList[i]);
            DestroyGameObject(gpScript);
        }

        //这里需要清理掉所有的pooledObject残留信息
        if (clearAllPooledData)
        {
            pooledGameObjectScriptList.Clear();
            m_pooledGameObjectMap.Clear();
            m_allPooledGameObjectMap.Clear();
        }
    }
    
    /// <summary>
    /// 从Pool获取GameObject
    /// </summary>
    /// <param name="prefabFullPath"></param>
    /// <returns></returns>
    public GameObject GetGameObject(string prefabFullPath)
    {
        CPooledGameObjectScript ret = GetGameObjectScript(prefabFullPath);
        if (ret == null)
        {
            return null;
        }

        return ret.Go;
    }


    /// <summary>
    /// 从Pool获取GameObjectScript
    /// </summary>
    /// <param name="prefabFullPath"></param>
    /// <returns></returns>
    public CPooledGameObjectScript GetGameObjectScript(string prefabFullPath)
    {
        CPooledGameObjectScript ret = GetGameObjectInner(prefabFullPath, Vector3.zero, Quaternion.identity, false);
        return ret;
    }

    /// <summary>
    /// 从Pool获取GameObject
    /// </summary>
    /// <param name="prefabFullPath"></param>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    /// <param name="useRotation"></param>
    /// <returns></returns>
    private CPooledGameObjectScript GetGameObjectInner(string prefabFullPath, Vector3 pos, Quaternion rot, bool useRotation)
    {
        if (string.IsNullOrEmpty(prefabFullPath))
        {
            return null;
        }

        CPooledGameObjectScript pooledGameObject = TryToGetUnUsedPooledGameObjectFromPool(prefabFullPath);

        // 取到了，直接用
        if (pooledGameObject != null)
        {
            pooledGameObject.Trans.localPosition = pos;
            pooledGameObject.Trans.localRotation = rot;
        }
        else
        {
            // 尝试创建
            pooledGameObject = CreateGameObject(prefabFullPath, pos, rot, useRotation);
        }

        // 获取/创建失败，直接返回null
        if (pooledGameObject == null)
        {
            return null;
        }

        OnGetPooledGameObject(pooledGameObject);

        return pooledGameObject;
    }

    /// <summary>
    /// CreateGameObject: 创建GameObject， 与下面的DestroyGameObject是一对，注意对称处理
    /// </summary>
    /// <param name="prefabFullPath"></param>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    /// <param name="useRotation"></param>
    /// <returns></returns>
    private CPooledGameObjectScript CreateGameObject(string prefabFullPath, Vector3 pos, Quaternion rot, bool useRotation)
    {
        var handler = GetHandler(prefabFullPath);
        var pooledGameObject = (GameObject)GameObject.Instantiate(handler);
        var ret = PoolGameObject(prefabFullPath, pooledGameObject);
        pooledGameObject.transform.position = pos;

        return ret;
    }
    
    /// <summary>
    /// 获取池化游戏对象后处理
    /// </summary>
    /// <param name="pooledGameObject"></param>
    public void OnGetPooledGameObject(CPooledGameObjectScript pooledGameObject)
    {
        pooledGameObject.OnGet();

        // todo: 统计
    }

    /// <summary>
    /// 池化游戏对象
    /// </summary>
    /// <param name="prefabFullPath"></param>
    /// <param name="pooledGameObject">池化前的对象GameObject</param>
    /// <returns>池化后的对象CPooledGameObjectScript</returns>
    public CPooledGameObjectScript PoolGameObject(string prefabFullPath, GameObject pooledGameObject)
    {
        //添加CPooledGameObjectScript
        CPooledGameObjectScript pooledGameObjectScript = new CPooledGameObjectScript();

        //初始化参数
        pooledGameObjectScript.Initialize(prefabFullPath, pooledGameObject);

        //OnCreate
        pooledGameObjectScript.OnCreate();

        // 添加到队列
        m_allPooledGameObjectMap[pooledGameObject.GetInstanceID()] = pooledGameObjectScript;

        // Editor下设置父物体
        SetParent(ref pooledGameObjectScript);

        return pooledGameObjectScript;
    }

    /// <summary>
    /// 判断Tag 用来放到对应的父节点下面
    /// </summary>
    /// <param name="goScript"></param>
    public void SetParent(ref CPooledGameObjectScript goScript)
    {
       
    }

    /// <summary>
    /// 通过instanceId查询对应对象
    /// </summary>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    public CPooledGameObjectScript GetPooledGameObj(int instanceId)
    {
        CPooledGameObjectScript goScript;
        m_allPooledGameObjectMap.TryGetValue(instanceId, out goScript);

        return goScript;
    }

    /// <summary>
    /// 销毁GameObject相关缓存,从GameInstanceID开始传递
    /// 销毁instance对应缓存.销毁<path,goList>缓存
    /// </summary>
    /// <returns></returns>
    public void DestroyGameObjectByID(int instanceId)
    {
        var goScript = GetPooledGameObj(instanceId);
        if (goScript != null)
        {
            // 清理instanceID映射缓存
            if (GetAllPooledGameObjectMap().TryGetValue(instanceId, out var value))
            {
                GetAllPooledGameObjectMap().Remove(instanceId);
            }
            // 清理path,list映射缓存
            var goList = GetPooledGameObjectList(goScript.m_prefabPath, false);
            if (goList != null)
            {
                for (int i = goList.Count - 1; i >= 0; i--)
                {
                    if (goList[i].m_gameObjectInstanceID == instanceId)
                    {
                        goList.RemoveAt(i);
                        break;
                    }
                }

                // 释放list结构体
                if (goList.Count <= 0)
                {
                    DestroyPooledGameObjectList(goScript.m_prefabPath);
                }
            }

            // 释放资源本身
            DestroyGameObject(goScript);
        }
    }

    /// <summary>
    /// 真正的销毁GameObject
    /// </summary>
    /// <returns></returns>
    private void DestroyGameObject(CPooledGameObjectScript pooledGameObjectScript)
    {
        GameObject.Destroy(pooledGameObjectScript.Go);
    }

    /// <summary>
    /// 尝试根据ResourceKey获取现存的未使用的GameObj
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private CPooledGameObjectScript TryToGetUnUsedPooledGameObjectFromPool(string path)
    {
        List<CPooledGameObjectScript> pooledGameObjectList = GetPooledGameObjectList(path, true);
        if (pooledGameObjectList == null)
        {
            return null;
        }

        //尝试从缓存的队列中获取GameObject
        while (pooledGameObjectList.Count > 0)
        {
            //每次从尾部获取，后进先出
            int index = pooledGameObjectList.Count - 1;

            //获取元素并从list移除
            CPooledGameObjectScript go = pooledGameObjectList[index];
            pooledGameObjectList.RemoveAt(index);

            return go;
        }

        return null;
    }

    /// <summary>
    /// 根据ResourcePath获取PooledGameObjectList
    /// </summary>
    /// <param name="path"></param>
    /// <param name="createIfNotExist"></param>
    /// <returns></returns>
    private List<CPooledGameObjectScript> GetPooledGameObjectList(string path, bool createIfNotExist)
    {
        List<CPooledGameObjectScript> pooledGameObjectList = null;
        if (m_pooledGameObjectMap.TryGetValue(path, out pooledGameObjectList))
        {
            return pooledGameObjectList;
        }
        else if (createIfNotExist)
        {
            pooledGameObjectList = new List<CPooledGameObjectScript>();
            m_pooledGameObjectMap.Add(path, pooledGameObjectList);

            return pooledGameObjectList;
        }

        return null;
    }

    /// <summary>
    /// 根据path销毁PooledGameObjectList
    /// </summary>
    /// <param name="path"></param>
    public void DestroyPooledGameObjectList(string path)
    {
        if (path == null)
        {
            return;
        }

        // 深度清理
        if (!m_pooledGameObjectMap.ContainsKey(path))
        {
            return;
        }

        var list = m_pooledGameObjectMap[path];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            DestroyGameObjectByID(list[i].m_gameObjectInstanceID);
        }

        return;
    }

    /// <summary>
    /// 当前资源路径的缓存池是否已经生成过
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool HasPooledGameObjectCache(string path)
    {
        return m_pooledGameObjectMap.ContainsKey(path);
    }

    /// <summary>
    /// 回收对象
    /// </summary>
    /// <param name="refGo"></param>
    public void RecycleGameObject(GameObject refGo)
    {
        if (refGo != null)
        {
            RecycleGameObject(refGo.GetInstanceID());
        }
    }

    /// <summary>
    /// 回收对象
    /// </summary>
    /// <param name="gameInstanceID"></param>
    public void RecycleGameObject(int gameInstanceID)
    {
        // 回收的对象可能已经被提前释放掉了
        if (!m_allPooledGameObjectMap.ContainsKey(gameInstanceID))
        {
            return;
        }

        RecycleGameObjectInner(m_allPooledGameObjectMap[gameInstanceID], false);
    }

    /// <summary>
    /// 回收对象
    /// </summary>
    /// <param name="pooledGameObject"></param>
    public void RecycleGameObject(CPooledGameObjectScript pooledGameObject)
    {
        RecycleGameObjectInner(pooledGameObject, false);
    }

    /// <summary>
    /// 回收预加载的对象
    /// </summary>
    /// <param name="pooledGameObject"></param>
    public void RecyclePreparedGameObject(CPooledGameObjectScript pooledGameObject)
    {
        RecycleGameObjectInner(pooledGameObject, true, true);
    }


    /// <summary>
    /// 回收一个对象，如果该对象是由对象池创建的，则不立即销毁，而是放到池中
    /// <param name="pooledGameObject"></param>
    /// <param name="setIsInit"></param>
    /// <param name="setParentPoolRoot"></param>
    private void RecycleGameObjectInner(CPooledGameObjectScript pooledGameObject, bool setIsInit, bool setParentPoolRoot = false)
    {
        if (pooledGameObject == null || pooledGameObject.Trans == null || !pooledGameObject.m_inUse)
        {
            return;
        }

        List<CPooledGameObjectScript> pooledGameObjectScriptList = GetPooledGameObjectList(pooledGameObject.m_prefabPath, true);

        pooledGameObject.OnRecycle();

#if UNITY_EDITOR
        // 判断Tag 用来放到对应的父节点下面
        SetParent(ref pooledGameObject);
#endif

        //添加到对应的script列表
        pooledGameObjectScriptList.Add(pooledGameObject);
    }

    /// <summary>
    /// 在池子中准备一定数量指定类型的对象
    /// </summary>
    /// <param name="prefabFullPath"></param>
    /// <param name="amount"></param>
    public bool PrepareGameObject(string path, int amount)
    {
        // 预创建资源的最大数量限制
        amount = amount > MaxPreloadCount ? MaxPreloadCount : amount;

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        List<CPooledGameObjectScript> pooledGameObjectScriptList = GetPooledGameObjectList(path, true);

        if (pooledGameObjectScriptList.Count >= amount)
        {
            return true;
        }

        amount -= pooledGameObjectScriptList.Count;
        for (int i = 0; i < amount; i++)
        {
            CPooledGameObjectScript pooledGameObjectScript = CreateGameObject(path, Vector3.zero, Quaternion.identity, false);

            if (pooledGameObjectScript != null)
            {
                pooledGameObjectScript.OnPrepare();

                //添加到对应的script列表
                pooledGameObjectScriptList.Add(pooledGameObjectScript);
            }
            else
            {
                return false;
            }
        }

        return true;
    }


    /// <summary>
    /// 创建对象池
    /// </summary>
    /// <param name="name"></param>
    /// <param name="count"></param>
    public void CreateObjectPool(string name, int count)
    {
        PrepareGameObject(name, count);
    }


    /// <summary>
    /// 资源母本.不会被copy: 使用请注意!!! 就是来拿底层的母本资源
    /// </summary>
    /// <param name="path"></param>
    /// <param name="isAsync"></param>
    /// <returns></returns>
    public GameObject GetHandler(string path, bool isAsync = false)
    {
        // todo: 资源母本应该从CDN路径获取
        var handler = (GameObject)ResourceManager.Instance.GetResource<GameObject>(path);

        return handler;
    }
    
    /// <summary>
    /// 重置缓存池
    /// </summary>
    public void Reset()
    {
        inGameMark = false;
        ClearPooledObjects(true);
    }
}


