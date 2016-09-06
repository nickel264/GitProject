using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.IO;


namespace NService
{
    //認証模組
    public interface IAuthenticateModule
    {
        string GetUserID();

        void SetUserID(string userID, string loginInfo);

        void ClearUserID();

    }

    public class SessionAuthenticateModule : IAuthenticateModule
    {
        public string GetUserID()
        {
            if(HttpContext.Current.Session!=null)
                return HttpContext.Current.Session["UserID"] as string;
            return null;
        }

        public void SetUserID(string userID, string loginInfo)
        {
            if (HttpContext.Current.Session != null)
                HttpContext.Current.Session["UserID"] = userID;
        }

        public void ClearUserID()
        {
            //HttpContext.Current.Session.Remove("UserID");
            if (HttpContext.Current.Session != null)
            {
                HttpContext.Current.Session.Clear();
                HttpContext.Current.Session.Abandon();
            }
        }
    }

    public class CookieAuthenticateModule : IAuthenticateModule
    {

        #region cookie加解密

        const string _KEY_64 = "a4G-8=Jk"; //必須是8個字符（64Bit)
        const string _IV_64 = "JKbN=5[?";  //必須是8個字符（64Bit)

        public static string Encrypt(string PlainText, string KEY_64, string IV_64)
        {
            byte[] byKey = System.Text.ASCIIEncoding.ASCII.GetBytes(KEY_64);
            byte[] byIV = System.Text.ASCIIEncoding.ASCII.GetBytes(IV_64);

            DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
            int i = cryptoProvider.KeySize;
            MemoryStream ms = new MemoryStream();
            CryptoStream cst = new CryptoStream(ms, cryptoProvider.CreateEncryptor(byKey, byIV), CryptoStreamMode.Write);

            StreamWriter sw = new StreamWriter(cst);
            sw.Write(PlainText);
            sw.Flush();
            cst.FlushFinalBlock();
            sw.Flush();
            return Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);

        }

        public static string Decrypt(string CypherText, string KEY_64, string IV_64)
        {
            byte[] byKey = System.Text.ASCIIEncoding.ASCII.GetBytes(KEY_64);
            byte[] byIV = System.Text.ASCIIEncoding.ASCII.GetBytes(IV_64);

            byte[] byEnc;
            try
            {
                byEnc = Convert.FromBase64String(CypherText);
            }
            catch
            {
                return null;
            }

            DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
            MemoryStream ms = new MemoryStream(byEnc);
            CryptoStream cst = new CryptoStream(ms, cryptoProvider.CreateDecryptor(byKey, byIV), CryptoStreamMode.Read);
            StreamReader sr = new StreamReader(cst);
            return sr.ReadToEnd();
        }

        #endregion

        public const string LOGINCOOKIEKEY = "PCINewWebUserID";

        public string GetUserID()
        {
            //記得還要加密哦
            HttpCookie cookie = HttpContext.Current.Request.Cookies[LOGINCOOKIEKEY];
            if (cookie != null && cookie.Value != null)
            {
                try
                {
                    string ret = Decrypt(cookie.Value, _KEY_64, _IV_64);
                    return ret;
                }
                catch(Exception ex)
                {                   
                    return null;
                }
            }          

            return null;
        }

        public void SetUserID(string userID, string loginInfo)
        {
            try
            {
                userID = Encrypt(userID, _KEY_64, _IV_64);
            }
            catch
            {
                userID = null;
            }
            HttpCookie loginCookie = new HttpCookie(LOGINCOOKIEKEY, userID);
            loginCookie.HttpOnly = true;
            loginCookie.Path = "/";
            if (loginInfo != null && loginInfo.Trim().Length > 0)
            {
                string[] tmp = loginInfo.Split(new char[] { '|' });
                if (tmp.Length > 0 && tmp[0] == "Forever")
                    loginCookie.Expires = DateTime.Now.AddYears(50);
            }
            HttpContext.Current.Response.AddHeader("P3P", "CP=CURa ADMa DEVa PSAo PSDo OUR BUS UNI PUR INT DEM STA PRE COM NAV OTC NOI DSP COR");
            HttpContext.Current.Response.Cookies.Add(loginCookie);
            //loginInfo(可設定是否永久登錄或暫時或幾十分鐘等)
        }

        public void ClearUserID()
        {
            HttpCookie cookie = HttpContext.Current.Request.Cookies[LOGINCOOKIEKEY];
            if (cookie != null)
            {
                cookie.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookie);
            }
        }

    }


    public class AuthenticateHelper
    {
        public static readonly AuthenticateHelper _instance = new AuthenticateHelper();

        static AuthenticateHelper()
        {
            //其實如果是隻有方法，沒有內部數據時，可以不注冊
            //ObjectFactory.Default.Register(_instance);
        }

        public static AuthenticateHelper Instance
        {
            get
            {
                return _instance;
            }
        }

        List<IAuthenticateModule> _authModules;

        public AuthenticateHelper()
        {
            _authModules = new List<IAuthenticateModule>();
            //因現在不用Session服務了
            //_authModules.Add(new SessionAuthenticateModule());     //用這個可以提高效率(避免每次cookie解密)
            _authModules.Add(new CookieAuthenticateModule());      //用這個可以避免Session過期
        }

        public const string UserIDKey = "_AuthenticateHelper_UserID";
        public const string UserKey = "_AuthenticateHelper_User";
        public const string UserInfoMethod = "UserInfo";

        public string UserID
        {
            get
            {
                return HttpContext.Current.Items[UserIDKey] as string;
            }
        }
    }
}