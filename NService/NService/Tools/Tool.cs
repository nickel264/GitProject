﻿using System;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;

namespace NService.Tools
{
    public class Tool
    {

        #region Convert dataType
        public static decimal ToDecimal(object o)
        {
            try
            {
                return decimal.Parse(o.ToString());
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region Json相關

        public static Dictionary<string, object> ToDic(string jsonStr)
        {
            if (jsonStr == null || jsonStr.Length == 0)
                return new Dictionary<string, object>();
            return JavaScriptObjectDeserializer.DeserializeDic(jsonStr);
        }

        public static ArrayList ToList(string jsonStr)
        {
            if (jsonStr == null || jsonStr.Length == 0)
                return null;
            return JavaScriptObjectDeserializer.DeserializeArrayList(jsonStr);
        }

        public static object[] ToArray(string jsonStr)
        {
            object[] ret = null;
            ArrayList o = ToList(jsonStr);
            if(o!=null){
                ret = new object[o.Count];
                o.CopyTo(ret);
            }
            return ret;
        }

        public static List<Dictionary<string, object>> ToListDic(string jsonStr)
        {
            if (jsonStr == null || jsonStr.Length == 0)
                return null;
            return JavaScriptObjectDeserializer.DeserializeList(jsonStr);
        }

        public static object ToObject(string jsonStr)
        {
            if (jsonStr == null || jsonStr.Length == 0)
                return null;
            return JavaScriptObjectDeserializer.DeserializeObject(jsonStr);
        }
        public static Dictionary<string, object> ToDic(params object[] array)
        {
            if (array != null)
            {
                Dictionary<string, object> ret = new Dictionary<string, object>();
                for (int i = 0; i < array.Length; i++)
                {
                    ret.Add(array[i].ToString(), array[++i]);
                }
                return ret;
            }
            return null;
        }

        public static DataRow ToRow(DataSet ds)
        {
            return ToRow(ds, 0);
        }

        public static DataRow ToRow(DataSet ds, int dtIndex)
        {
            if (ds != null && ds.Tables.Count > dtIndex && ds.Tables[dtIndex].Rows.Count > 0)
            {
                return ds.Tables[dtIndex].Rows[0];
            }
            return null;
        }


        public static DataRowCollection ToRows(DataSet ds)
        {
            return ToRows(ds, 0);
        }

        public static DataRowCollection ToRows(DataSet ds, int dtIndex)
        {
            if (ds != null && ds.Tables.Count > dtIndex && ds.Tables[dtIndex].Rows.Count > 0)
            {
                return ds.Tables[dtIndex].Rows;
            }
            return null;
        }

        public static Dictionary<string, object> ToDic(DataSet ds)
        {
            return ToDic(ds, 0);
        }

        public static Dictionary<string, object> ToDic(DataSet ds, int dtIndex)
        {
            if (ds != null && ds.Tables.Count > dtIndex && ds.Tables[dtIndex].Rows.Count > 0)
            {
                return ToDic(ds.Tables[dtIndex].Rows[0]);
                /*
                Dictionary<string, object> ret = new Dictionary<string, object>();
                foreach (DataColumn col in ds.Tables[0].Columns)
                {
                    ret.Add(col.ColumnName, ds.Tables[0].Rows[0][col]);
                }
                return ret;
                */
            }
            return null;
        }
        public static Dictionary<string, object> ToDic(DataRow dr)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            foreach (DataColumn col in dr.Table.Columns)
            {
                ret.Add(col.ColumnName, dr[col]);
            }
            return ret;
        }

        public static List<Dictionary<string, object>> ToListDic(DataRow[] drs)
        {
            if (drs != null && drs.Length>0)
            {
                List<Dictionary<string, object>> ret = new List<Dictionary<string, object>>();
                foreach (DataRow dr in drs)
                {
                    ret.Add(ToDic(dr));
                }
                return ret;
            }
            return null;
        }

        public static List<Dictionary<string, object>> ToListDic(DataRowCollection drs)
        {
            if (drs != null && drs.Count > 0)
            {
                List<Dictionary<string, object>> ret = new List<Dictionary<string, object>>();
                foreach (DataRow dr in drs)
                {
                    ret.Add(ToDic(dr));
                }
                return ret;
            }
            return null;
        }

        public static List<Dictionary<string, object>> ToListDic(DataSet ds)
        {
            return ToListDic(ds,0);
        }

        public static List<Dictionary<string, object>> ToListDic(DataSet ds, int dtIndex)
        {
            if (ds != null && ds.Tables.Count > dtIndex && ds.Tables[dtIndex].Rows.Count > 0)
            {
                List<Dictionary<string, object>> ret= new List<Dictionary<string, object>>();
                foreach (DataRow dr in ds.Tables[dtIndex].Rows)
                {
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    foreach (DataColumn col in ds.Tables[dtIndex].Columns)
                    {
                        item.Add(col.ColumnName, dr[col]);
                    }
                    ret.Add(item);
                }
                return ret;
            }
            return null;
        }

        public static string ToJson(object o)
        {
            return JsonConverter.Convert2Json(o);
        }

        public static string ToHtml(object o)
        {
            return HtmlConverter.Convert2Html(o);
        }

        #endregion

       

        #region 文件上傳相關

        static Dictionary<string, string[]> filesCache
        {
            get
            {
                if(System.Web.HttpContext.Current.Cache["__FileServiceGet"] ==null)
                    System.Web.HttpContext.Current.Cache["__FileServiceGet"] = new Dictionary<string, string[]>();
                Dictionary<string, string[]> ret = System.Web.HttpContext.Current.Cache["__FileServiceGet"]  as Dictionary<string,string[]>;
                return ret;
            }
        }

      

        public static string UploadFolder
        {
            get
            {
                return System.Web.HttpContext.Current.Server.MapPath("~\\upload\\files\\");
            }
        }

        static string fileDir(string kind)
        {
            //System.Web.HttpContext.Current.Response.Write(System.Web.HttpContext.Current.Request.PhysicalApplicationPath);
            //return System.Web.HttpContext.Current.Request.PhysicalApplicationPath
            //+ "upload\\files\\" + (kind == null || kind == "" ? "" : kind + "\\");
          
            return UploadFolder//System.Web.HttpContext.Current.Server.MapPath("upload\\files\\")
            +  (kind == null || kind == "" ? "" : kind + "\\");
        }

        static string fileWebDir(string kind)
        {
            //System.Web.HttpContext.Current.Response.Write(System.Web.HttpContext.Current.Request.PhysicalApplicationPath);
            return System.Web.HttpContext.Current.Request.ApplicationPath.Replace("/","\\")
            + "\\upload\\files\\" + (kind == null || kind == "" ? "" : kind + "\\");
        }

        
             


        #endregion

        #region mail發送相關

        public static bool MailStop(string mail)
        {
            DataRowCollection drs = Tool.ToRows(DBHelper.Instance.Query("Common_Users_Query2", Tool.ToDic("FullEmail", mail)));
            //return dr!=null?dr["Enable"].ToString() == "Y":false;           //Enable實際上是停用注記
            if (drs != null && drs.Count > 0)
            {
                foreach (DataRow dr in drs)
                {
                    if(dr["Enable"].ToString() != "Y")
                        return false;
                }
                return true;        //只要有資料,而又沒有任何一個人同步
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 發送郵件
        /// </summary>
        /// <param name="body"></param>
    
        #endregion

        #region xml相關

        public static Dictionary<string, object> ToDicByXml(string xmlStr)
        {
            return XmlConvert.ParseObject(xmlStr) as Dictionary<string, object>;
        }

        public static ArrayList ToListByXml(string xmlStr)
        {
            return XmlConvert.ParseObject(xmlStr) as ArrayList;
        }


        #endregion

        #region Log,Trace相關

        public static void Trace(string msg, params object[] args)
        {
            LogHelper.Instance.Trace(msg, args);
        }

        public static void Info(string msg, params object[] args)
        {
            LogHelper.Instance.Info(msg, args);
        }

        public static void Warn(string msg, params object[] args)
        {
            LogHelper.Instance.Warn(msg, args);
        }

        public static void Error(string msg, params object[] args)
        {
            LogHelper.Instance.Error(msg, args);
        }

        #endregion

       

    }

    public class ToolInjector
    {
        public virtual bool BeforeDBExecute(string cmd, Dictionary<string, object> args)
        {
            return true;
        }

        public virtual void AfterDBExecute(int result, string cmd, Dictionary<string, object> args)
        {

        }

        public virtual bool BeforeAjaxExecute(string service, string command, object serviceObj, object[] objectParams, Dictionary<string, object> ret)
        {
            return true;
        }

        public virtual void AfterAjaxExecute(string service, string command, object serviceObj, object[] objectParams, Dictionary<string, object> ret)
        {

        }
    }


    

}