/*===========================================================================*/
/* @brief:      ResourceManager资源管理器                                    */
/* @details:    仅作为资源实例化方案的引用参考,具体实现业务自行填充          */
/* @author:     Jamin                                                        */
/* @data:       2023/5/18                                                    */
/*===========================================================================*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

public class ResourceManager 
{
    public Dictionary<string, object> _allObjecs = new Dictionary<string, object>();
    public AssetBundle uiBundle;
    public AssetBundle thirdGameBundle;
    public bool cdnResAllDone = false;
        
    private static ResourceManager _instance;
    public static ResourceManager Instance
    {
        get
        {
            _instance ??= new ResourceManager();

            return _instance;
        }
    }

    /// <summary>
    /// 目前提供GameObjectPool使用: 项目自行实现
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public T GetResource<T>(string path) where T : Object
    {
        if (_allObjecs.TryGetValue(path, out var value))
        {
            return value as T;
        }

        AssetBundle ab = null;
#if UNITY_EDITOR
        return Resources.Load<T>(path);
#endif

        return null;
    }
}