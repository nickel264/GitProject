using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Reflection;

namespace NService.Tools
{
    public class ObjectFactory
    {
        public static readonly ObjectFactory Instance = new ObjectFactory(new FileConfig("ObjectFactory", new JsonParser()));


        public FileConfig _config;
        Dictionary<string, List<string>> _dyDllObjects;
        FileConfig _dyDllConfig;
        public ObjectFactory(FileConfig config)
        {
            _config = config;

            _dyDllObjects = new Dictionary<string, List<string>>();
            _dyDllConfig = new FileConfig("_service\\Dll", "", new AssemblyParser());
        }
            

        public IConfig Config
        {
            get
            {
                return _config;
            }
        }

        public FileConfig DyConfig
        {
            get
            {
                return _dyDllConfig;
            }
        }

        public object Get(string name)
        {
            return this.Get<object>(name);
        }

        object[] dealParams(ArrayList targs)
        {
            if (targs == null || targs.Count == 0)
                return null;
            object[] ret = new object[targs.Count];
            int i = 0;
            foreach (object arg in targs)
            {
                ret[i++] = arg;
            }
            return ret;
        }

        public T Get<T>(string objectID) where T : class
        {
            string objectType = objectID;
            T ret = null;
            string dll = null;
            object[] args = null;
            string url = null;
            Dictionary<string, object> soapHeader = null;
            string dyDll = null;           

            Dictionary<string, object> fields = null;

            object[] cfg = objectCfg(objectID);
            if (cfg != null)
            {
                if (cfg[0] != null && cfg[0].ToString().Length > 0)
                    objectType = cfg[0].ToString();
                dll = cfg[1] as string;
                ArrayList targs = cfg[2] as ArrayList;
                args = this.dealParams(targs);
                url = cfg[3] as string;
                soapHeader = cfg[4] as Dictionary<string, object>;
                dyDll = cfg[5] as string;

                fields = cfg[6] as Dictionary<string, object>;

            }

            //TOCHECK
            ret = CreateObject<T>(objectType, dll, args, ref dyDll, objectType == objectID);   

            return ret;
        }

        public T CreateObject<T>(string objectType, string dll, object[] args, ref string dyDll, bool tryT) where T : class
        {
            T ret = null;
            Type t = GetType<T>(objectType, dll, ref dyDll, tryT);
            if (ret == null)
            {
                try
                {
                    ret = (T)Activator.CreateInstance(t, args);
                }
                catch (Exception ex)
                {
                    throw new Exception("Exception 'ObjectFactory.CreateObject': " + ex.InnerException.Message.ToString(), ex.InnerException == null ? ex : ex.InnerException);
                }
            }

            return ret;
        }

        public Type GetType<T>(string objectType, string dll, ref string dyDll, bool tryT) where T : class
        {
            Type t = null;
            string tType = tryT && typeof(T) != typeof(object) && typeof(T).FullName != objectType && !typeof(T).IsInterface && !typeof(T).IsAbstract ? typeof(T).FullName : null;

            if (_dyDllConfig != null)     //有指明dyDll，則到App_Data/Config/Dll下搜，且一定有type，也不再試T
            {
                Assembly[] asses = null;
                string []files = System.IO.Directory.GetFiles(_dyDllConfig.getPath(), "*.dll");
                for (int i = 0; i < files.Length; i++)
			    {
                    Assembly dynamicAss = Assembly.LoadFrom(files[i]);
                    t = dynamicAss.GetType(objectType,false);
                    if (t != null)
                        break;
                    
			    }
              
            }
            else
            {
                Assembly[] asses = System.AppDomain.CurrentDomain.GetAssemblies();
                Dictionary<string, int> repeatAsses = new Dictionary<string, int>();
                foreach (Assembly ass in asses)
                {
                    if (!ass.GlobalAssemblyCache)
                    {
                        if (!repeatAsses.ContainsKey(ass.FullName))
                            repeatAsses.Add(ass.FullName, 0);
                        repeatAsses[ass.FullName]++;
                    }
                }
                foreach (Assembly ass in asses)
                {
                    //不找.net本身的類作服務對象?
                    if (!ass.GlobalAssemblyCache)
                    {
                        t = ass.GetType(objectType, false);
                        if (t != null)
                        {
                            //TODO:排除那些是動態dll，但是又找不到的，說明過時了，有新版本
                            //TODO:如果需要在app或dll中引用第三方dll，又不想把它放到bin目錄下，是可以的
                            //但是要用版本號，保持一致

                            //凡是用這種直接找的方式，如果不是cache，說明過期了，應該有最新版本在
                            if (repeatAsses[ass.FullName] == 1)
                                break;
                        }
                        else if (tType != null)
                        {
                            t = ass.GetType(tType, false);
                            if (t != null)
                            {
                                if (repeatAsses[ass.FullName] == 1)
                                    break;
                            }
                        }
                    }
                }
            }           

            if (t == null && tType != null)      
                t = typeof(T);
            return t;
        }

        IEnumerable<Type> getTypes(string filePath, Type baseType)
        {
            Assembly a = Assembly.LoadFrom(filePath);
            return a.GetTypes().Where(t => t.IsSubclassOf(baseType) && !t.IsAbstract);
        }

        object[] objectCfg(string objectID)
        {
            string key = objectID;
            Dictionary<string, object> cfg = _config.Parse<Dictionary<string, object>>(key);
            if (cfg != null)
            {
                if (cfg.ContainsKey("$ref"))
                {
                    cfg = _config.Parse<Dictionary<string, object>>(cfg["$ref"].ToString());
                }
                return new object[] { 
                    cfg.ContainsKey("Type")?cfg["Type"].ToString():null
                    ,cfg.ContainsKey("Dll")?cfg["Dll"].ToString():null
                    ,cfg.ContainsKey("Args")?(ArrayList)cfg["Args"]:null
                    ,cfg.ContainsKey("Url")?cfg["Url"].ToString():null
                    ,cfg.ContainsKey("SoapHeader")?(Dictionary<string,object>)cfg["SoapHeader"]:null
                    ,cfg.ContainsKey("DyDll")?cfg["DyDll"].ToString():null
                    ,cfg.ContainsKey("Fields")?(Dictionary<string,object>)cfg["Fields"]:null
                    //不作別名，是以別名第一次創建對象會失敗，建議以資料庫形式儲存Config對象
                };
            }
            return null;
        }

    }
}
