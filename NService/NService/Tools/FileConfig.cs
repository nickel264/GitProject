﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace NService.Tools
{
    public delegate void ConfigRefreshCallback(string name);

    #region Interface
    public interface IConfig
    {
        event ConfigRefreshCallback OnConfigRefresh;

        T Parse<T>(string name) where T : class;
       
    }
    public interface IParser
    {
        T Read<T>(string text) where T : class;
        //以後應該還有將dic轉成string的method(這個應該是存儲所用)     
    }       
    #endregion

    public class AConfig : IConfig
    {
        public event ConfigRefreshCallback OnConfigRefresh;
        public virtual T Parse<T>(string name) where T : class
        {
            return null;
        }       
    }


    public class FileConfig : AConfig
    {
        protected string _subDir;
        protected IParser _parser;
        protected string _path;


        public FileConfig(string subDir, IParser parser)
            : this("_service", subDir, parser)
        {
            
        }

        public FileConfig(string targetDir,string subDir, IParser parser)
        {
            this._subDir = subDir;
            _parser = parser;
            _path = System.AppDomain.CurrentDomain.BaseDirectory + targetDir + "\\" + (subDir.Length > 0 ? subDir : "");
        }

        public string getPath()
        {
            return _path;
        }

        public T Parse<T>(string name) where T : class
        {
            string path;
            T ret = null;
            ret = Parse<T>(name, out path);

            return ret;
        }

        public virtual T Parse<T>(string name, out string path) where T : class
        {

            bool noExt = this._subDir == "ObjectFactory";

            

            string fullName = "";
            if (!noExt)
            {
                int indexLast = name.LastIndexOf('.');
                string ext = name.Substring(indexLast, name.Length - name.Substring(0, indexLast).Length);
                fullName = name.Substring(0, indexLast).Replace(".", "\\") + ext;
            }
            else
            {
                fullName = name.Replace(".", "\\");
            }

            path = _path + "\\" + fullName;

            if (File.Exists(path))
            {
                return getResultFromFile<T>(path);
            }

            return null;
            //return null;

        }

        T getResultFromFile<T>(string path) where T : class
        {
            T ret = null;
            string cfgStr = File.ReadAllText(path, System.Text.Encoding.Default);
            if (cfgStr.Trim().Length > 0)
                ret= _parser.Read<T>(cfgStr);

            return ret;
            
        }



    }


}
