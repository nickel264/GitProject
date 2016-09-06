using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Data;
using NService.Tools;
using System.Threading;

namespace NService
{

    public class HttpService
    {
        public static readonly HttpService Instance = new HttpService();

        public virtual void Run()
        {
            HttpContext context = HttpContext.Current;
            if (context != null)
            {                
                int remain;
                string jsonCall = getJsonCall(context, out remain);
                if (jsonCall == null || jsonCall.Trim().Length == 0)
                    dealResult(Tool.ToDic(
                        new object[]{
                            "AjaxError",remain==0?"4":"7"
                            ,"Message",remain==0?"Must provide JsonService QueryString":"wait " +remain.ToString() + " package" }));
                else
                    dealResult(ServiceCaller.Instance.CallToDic(ServiceCaller.CallType.BaseCall, jsonCall));
            }
            else
                throw new ApplicationException("這個方法必須通過http請求調用");
        }



        protected virtual void dealResult(Dictionary<string, object> ret)
        {
            HttpContext.Current.Response.CacheControl = "no-cache";
            string noJsonError = HttpContext.Current.Request.QueryString["NoJsonError"];
            if (noJsonError != null)       //不要json方式輸出，如果錯誤，則直接輸入NoJson的值
            {
                HttpContext.Current.Response.Clear();
                if (ret["AjaxError"].ToString() == "0")
                {
                    HttpContext.Current.Response.Write(ret["Result"].ToString());
                }
                else
                {
                    if (noJsonError == "ServiceMessage")
                    {
                        //HttpContext.Current.Response.Write("AjaxError:" + ret["AjaxError"].ToString());
                        HttpContext.Current.Response.Write(ret["Message"].ToString());
                    }
                    else
                    {
                        HttpContext.Current.Response.Write(noJsonError);
                    }
                }
            }
            else
            {
                this.dealJsonResult(ret);
            }
        }

        protected virtual void dealJsonResult(Dictionary<string, object> ret)
        {
            DealJsonResult(HttpContext.Current, ret);
        }

        public void DealJsonResult(HttpContext context, Dictionary<string, object> ret)
        {
            ret["EndTimeTicks"] = Math.Round(DateTime.Now.Ticks / 10000M);
            string result = "";
            try
            {
                //throw new ApplicationException("Test");
                string callbackArgs = context.Request.QueryString["callbackArgs"];      //jsonp
                ret["callbackArgs"] = callbackArgs;
                result = Tool.ToJson(ret);
            }
            catch (Exception ex2)
            {
                Tool.Warn("結果輸出json出錯", "ex2", ex2);
                //再試一次
                System.Threading.Thread.Sleep(1000);
                try
                {
                    //throw new ApplicationException("Test2");
                    result = Tool.ToJson(ret);

                }
                catch (Exception ex)
                {
                    Tool.Error("結果輸出json重試也出錯", "ex", ex);
                    result = "{\"AjaxError\":\"4\",\"Message\":\"結果轉Json出錯:" + ex.Message + "\"}";
                }
            }
            context.Response.Clear();
            //在用iframe upload時，IE會出現腳本下載
            string iframeRequest = context.Request.QueryString["iframeRequest"];      //jsonp
            if (iframeRequest != "1")
                context.Response.ContentType = "text/javascript; charset=utf-8";
            string callback = context.Request.QueryString["callback"];      //jsonp
            if (callback != null)
            {
                if (iframeRequest == "1")
                    context.Response.Write("<script type='text/javascript'>");
                if (callback.ToLower() == "sjs.run")
                {
                    context.Response.Write("sjs.run(function(){ return");
                }
                else
                {
                    context.Response.Write(callback);
                    context.Response.Write("(");
                }
            }
            context.Response.Write(result);
            if (callback != null)
            {
                if (callback == "sjs.run")
                    context.Response.Write("})");
                else
                    context.Response.Write(");");
                if (iframeRequest == "1")
                    context.Response.Write("</script>");
            }
        }

       

        string getJsonCall(HttpContext context, out int remain)
        {
            remain = 0;         //還剩多少個
            string jsonCall = context.Request["JsonService"]; //context.Request.QueryString["JsonService"];
            string jsonKey = context.Request["JsonKey"];
            if (jsonKey == null || jsonKey.Trim().Length == 0)
                return jsonCall;

            //不保證完全成功
            string cacheKey = context.Request.UserHostAddress + "_" + jsonKey;          //IP
            int jsonSeq = int.Parse(context.Request["JsonSeq"]);
            int jsonTotal = int.Parse(context.Request["JsonTotal"]);
            string[] cacheJsonStr = null;
            
            cacheJsonStr = HttpRuntime.Cache[cacheKey] as string[];
            if (cacheJsonStr == null)
            {
                cacheJsonStr = new string[jsonTotal];
                for (int i = 0; i < cacheJsonStr.Length; i++)
                {
                    cacheJsonStr[i] = null;
                }
                HttpRuntime.Cache.Insert(cacheKey, cacheJsonStr
                    , null
                    , System.Web.Caching.Cache.NoAbsoluteExpiration
                    , new TimeSpan(0, 5, 0)         //5分鐘自動刪除(也就是說5分鐘之內必須完成所有包的傳送)
                    , System.Web.Caching.CacheItemPriority.Default
                    , null
                );
                }
          
            cacheJsonStr[jsonSeq] = jsonCall;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < cacheJsonStr.Length; i++)
            {
                if (cacheJsonStr[i] == null)
                {
                    remain++;
                }
                else if (remain == 0)
                {
                    sb.Append(cacheJsonStr[i]);
                }
            }
            if (remain == 0)
            {
                HttpRuntime.Cache.Remove(cacheKey);
                return sb.ToString();
            }
            else
            {
                return null;
            }
        }

    }


    public class ServiceCaller
    {
        public enum CallType
        {
            PermissionCall          //包括權限，事務的service 呼叫
            ,
            TransactionCall         //只進行事務的service呼叫
            ,
            BaseCall          //只進行最內部的call(反射調用)
        }

        public static readonly ServiceCaller Instance = new ServiceCaller();


        public Dictionary<string, object> CallToDic(ServiceCaller.CallType callType, string jsonCall)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();

            object jsonObject = null;
            try
            {
                jsonObject = Tool.ToObject(jsonCall);
            }
            catch (Exception ex)
            {
                ret["AjaxError"] = "4";
                ret["Message"] = "Tool.ToObject failure,json format invalid(" + jsonCall + " message:" + ex.Message + ")";
                return ret;
            }

            bool isMultiple = false;     //是不是多個調用
            ArrayList items = null;
            if (jsonObject is ArrayList)
            {
                isMultiple = true;
                items = jsonObject as ArrayList;
            }
            else if (jsonObject is Dictionary<string, object>)
            {
                items = new ArrayList(new object[] { jsonObject });
            }

            List<Dictionary<string, object>> multipleRet = new List<Dictionary<string, object>>();
            if (items != null && items.Count > 0)
            {
                foreach (Dictionary<string, object> dic in items)
                    multipleRet.Add(CallToDic(callType, dic));
            }
            else
            {
                ret["AjaxError"] = "4";
                ret["Message"] = "service format(json) invalid(" + jsonCall + ")";
            }


            if (isMultiple)
            {
                ret["AjaxError"] = 0;
                ret["Result"] = multipleRet;
                ret["IsMultiple"] = true;
            }
            else
            {
                ret = multipleRet[0];
                ret["IsMultiple"] = false;
            }

            return ret;
            //return Tool.ToJson(ret);
        }


        public Dictionary<string, object> CallToDic(ServiceCaller.CallType callType, Dictionary<string, object> dic)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            if (dic != null)
            {
                string service = null;
                object[] args = null;
                //合在一起是因為，對外提供服務不一定是這樣的形式，還可能是直接一個服務名稱（如PCIUserList)
                //所以留下接口在這里
                if (dic.ContainsKey("service"))
                {
                    service = dic["service"].ToString();
                    if (dic.ContainsKey("params"))
                    {
                        ArrayList tmp = dic["params"] as ArrayList;
                        if (tmp != null)
                        {
                            args = new object[tmp.Count];
                            tmp.CopyTo(args);
                        }
                    }
                    //開發測試用，可以指定延遲，以測試網路，database繁忙情況
                    if (dic.ContainsKey("callDelay"))
                        System.Threading.Thread.Sleep(int.Parse(dic["callDelay"].ToString()));

                    ret = CallToDic(callType, service, args);
                }
                else
                {
                    ret["AjaxError"] = 4;
                    ret["Message"] = "service call description invalid(must provide service!)";
                }
            }
            else
            {
                ret["AjaxError"] = 4;
                ret["Message"] = "service call description is null(not a json object)";
            }
            return ret;
        }


        public Dictionary<string, object> CallToDic(ServiceCaller.CallType callType, string service, params object[] args)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            try
            {
                object result = Call(callType, service, args);
                ret["AjaxError"] = 0;
                ret["Result"] = result;
                ret["Params"] = args;
            }            
            catch (Exception ex)
            {
                ret["AjaxError"] = 5;
                ret["Message"] = ex.Message;
                Tool.Error("服務調用出錯(未捕獲)", "ex", ex);
            }
            ret["Service"] = service;
            return ret;
        }



        public object Call(CallType type, string service, params object[] args)
        {
            return call(type, service, args);
        }

        protected virtual object call(CallType type, string service, params object[] args)
        {
            try
            {
                int paramsIndex = service.IndexOf("$");
                if (paramsIndex > 0)
                {
                    AppEventHanlder.Instance.SetServiceVarContent(service.Substring(paramsIndex + 1));
                    service = service.Substring(0, paramsIndex);
                    
                }
                //把PermissionCall就看成一個新的Session，所以都要清空ServiceVarContent
                //多個service批次call,第一個不影響第二個
                else if (type == CallType.PermissionCall)
                {
                    AppEventHanlder.Instance.SetServiceVarContent(null);
                }


                int dotIndex = service.LastIndexOf(".");
                if (dotIndex <= 0 || dotIndex >= service.Length - 1)
                    throw new ApplicationException("Invalid service:" + service);
                string serviceId = service.Substring(0, dotIndex);
                string command = service.Substring(dotIndex + 1);
                //TODO:應該讓permissionCall先判斷一下權限，要不一直在實例化對象
                object serviceObj = ObjectFactory.Instance.Get(serviceId);
                if (serviceObj == null)
                    throw new ApplicationException("Service not found:" + serviceId);


                return baseCall(serviceObj, serviceId, command, args);
            }
            finally
            {
               
            }
        }


        protected virtual object baseCall(object serviceObj, string serviceId, string command, params object[] args)
        {
            try
            {
                
                if (serviceObj is IService)
                {
                    return (serviceObj as IService).Call(command, args);
                }
                else
                {
                    return serviceObj.GetType().InvokeMember(
                        command
                        , BindingFlags.Default | BindingFlags.InvokeMethod
                        , null
                        , serviceObj
                        , args);
                }
            }            
            catch (TargetInvocationException tex)
            {
                Exception innerEx = tex.InnerException;
                if (innerEx is NSInfoException)
                {                  
                    throw new ApplicationException(innerEx.Message.ToString(), innerEx);
                }
                else {           
                    throw new ApplicationException("Message error: " + innerEx.Message.ToString(), innerEx);
                }

               
            }            
            catch (MissingMethodException ex)
            {
                string argInfo = "";
                if (args != null)
                {
                    foreach (object arg in args)
                        argInfo += (argInfo.Length > 0 ? "," : "") + (arg == null ? "null" : arg.GetType().Name);
                }
                //Tool.Error("反射調用失敗，未找到方法" ,"類.方法名",ex.Message,"參數",argInfo);
                throw new ApplicationException("Exception Server.ServiceCaller.baseCall" + ex.Message + " Params:" + argInfo);
            }
        }
        
    }

    public interface IService
    {
        object Call(string command, object[] args);
    }
}