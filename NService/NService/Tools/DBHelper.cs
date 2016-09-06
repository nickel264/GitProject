using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Web;
using System.Data.OracleClient;
using System.Data.SqlClient;

namespace NService.Tools
{
    public class DBHelper
    {
        public static readonly DBHelper OrgInstance = new DBHelper();
        public static readonly DBHelper Instance = OrgInstance;

        #region Main Method
        public virtual DataSet Query(string cmdName, Dictionary<string, object> args)
        {
            return Query(null, cmdName, args);
        }

        public virtual int Execute(string cmdName, Dictionary<string, object> args)
        {
            return this.Execute(null, cmdName, args);
        } 
        #endregion

        public virtual int Execute(string tranID, string cmdName, Dictionary<string, object> args)
        {
            return this.Execute<int>(null, cmdName, args);
        }

        public virtual T Execute<T>(string tranID, string cmdName, Dictionary<string, object> args)
        {
            if (args == null)
                args = new Dictionary<string, object>();// nullArgs;
            Database db;
            string dbName;
            DbCommand cmd = PrepareCommand(cmdName, args, out db, out dbName);
            DbTransaction tran = null;
            try
            {
                object ret;
                if (tranID == null && !args.ContainsKey("__NODEFAULTTRAN"))
                    tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;

                tran = this.getTran(db, tranID, true);
                
                if (tran == null)
                {
                    if (typeof(T) == typeof(int))
                    {
                        ret = db.ExecuteNonQuery(cmd);
                    }
                    else if (typeof(T) == typeof(DataSet))
                    {
                        ret = db.ExecuteDataSet(cmd);
                    }
                    else if (typeof(T) == typeof(IDataReader))
                    {
                        ret = db.ExecuteReader(cmd);
                    }
                    else
                    {
                        ret = db.ExecuteScalar(cmd);
                    }
                }
                else
                {
                    if (typeof(T) == typeof(int))
                    {
                        ret = db.ExecuteNonQuery(cmd, tran);
                    }
                    else if (typeof(T) == typeof(DataSet))
                    {
                        ret = db.ExecuteDataSet(cmd, tran);
                    }
                    else if (typeof(T) == typeof(IDataReader))
                    {
                        ret = db.ExecuteReader(cmd, tran);
                    }
                    else
                    {
                        ret = db.ExecuteScalar(cmd, tran);
                    }
                }
                /*
                /// <summary>
                /// 注Log的原則:
                /// 指DBHelper.Execute
                /// 如果是用戶業務過程產生的數據，如送審，退單，核准等。應該記錄
                /// 而如果是系統Job或系統行為，則不應該也不需要記錄此項Log數據(需要調用此類的NoLogResult方法停止日志過程)
                /// 而是記錄這項系統Job的行為 ,如發送Email的Job，只記錄有Email發送數據的開始和結束過程即可
                 * 以及如批次發送簡訊，執行報廢樣品單送審，批次回寫注記等過程則為同一原則
                 * 而如果是由用戶發起（如PccMessenger主動收取信息，則一般不需要記錄Log，因為訊息發送資料行會完成記錄這個過程）
                 * Kevin.zou 2010.11.25 注
                /// </summary>
                */
                if (!args.ContainsKey("__NoLogResult")
                    || args["__NoLogResult"].ToString().Trim() != "1")                    
                addOutputParameters(cmd, args);
                /*if (this.OnExecute != null)
                    this.OnExecute(ret, cmd, tran, tranID, cmdName, args);*/
                return (T)ret;
            }
            catch (Exception ex)
            {
                //Tool.Error("[DBHelper.Execute]", "tranID", tranID, "tran", tran == null ? "null" : "OK", "DBName", dbName, " cmd", cmd, "ex", ex.Message);
                throw;
                /*
                throw new ApplicationException("Execute fail(Message:" + ex.Message
                   + " cmdName:" + cmdName
                   + " tranID:" + tranID
                   + " sql:" + cmd.CommandText, ex);
               */
            }
        }


        public virtual DataSet Query(string tranID, string cmdName, Dictionary<string, object> args)
        {
            DataSet ds = null;
            if (args == null)
                args = new Dictionary<string, object>();// nullArgs;

            Database db;
            string dbName;
            DbCommand cmd = PrepareCommand(cmdName, args, out db, out dbName);
            DbTransaction tran = null;
            try
            {
                if (tranID == null && !args.ContainsKey("__NODEFAULTTRAN"))
                    tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;

                tran = this.getTran(db, tranID, false);
                
                if (tran == null)
                    ds = db.ExecuteDataSet(cmd);
                else
                    ds = db.ExecuteDataSet(cmd, tran);
                
                addOutputParameters(cmd, args);

            }
            catch (Exception ex)
            {
                Tool.Error("[DBHelper.Query]", "tranID", tranID, "tran", tran == null ? "null" : "OK", "DBName", dbName, "cmd", cmd, "ex", ex.Message);
                throw;
            }

            return ds;

        }

        DbCommand PrepareCommand(string cmdName, Dictionary<string, object> args, out Database db, out string dbName)
        {
            try
            {
                SqlHelper sqlHelper = SqlHelper.Instance;
                Dictionary<string,string> dic= sqlHelper.getCmdCfgToDic(cmdName);
                dbName = dic["DB"];
                string text = sqlHelper.CommandSql(dic["Text"], args);
               
                string type = "";
                if (dic.ContainsKey("Type"))
                    type = dic["Type"];               

                try
                {
                    db = ObjectFactory.Instance.Get<Database>(dbName);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("get db error:" + dbName + ",ex:" + ex.ToString());
                }
               
                DbCommand ret = null;
                if (type != null && type.Equals("procedure"))
                    ret = PrepareProcCommand(db, text.Trim().Replace("\r", "").Replace("\n", ""), args);
                else
                    ret = PrepareSqlCommand(db, text, args);

                if (args.ContainsKey("__TIMEOUT"))
                    ret.CommandTimeout = (int)args["__TIMEOUT"];
                              

                return ret;

            }
            catch (Exception)
            {
                
                throw;
            }    
        }


        DbCommand PrepareProcCommand(Database db, string procname, Dictionary<string, object> args)
        {
            DbCommand cmd = db.GetStoredProcCommand(procname);
            using (DbConnection conn = db.CreateConnection())
            {
                try
                {
                    cmd.Connection = conn;
                    cmd.Connection.Open();
                    if (db.DBType.Equals(DatabaseType.Oracle))
                        OracleCommandBuilder.DeriveParameters((OracleCommand)cmd);
                    else
                        SqlCommandBuilder.DeriveParameters((SqlCommand)cmd);
                    cmd.Connection.Close();
                    addProcParameters(db, cmd, args);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("PrepareProcCommand Fail(in Connection Open or DeriveParameters)(Message:" + ex.Message
                           + " procname:" + procname
                           + " db:" + db.DBType.ToString(), ex);
                }

            }
            return cmd;
        }

       

        DbCommand PrepareSqlCommand(Database db, string sql, Dictionary<string, object> args)
        {
            DbCommand cmd = db.GetSqlStringCommand("");
            SqlDyHelper dyHelper = new SqlDyHelper(db, cmd, args);
            MatchEvaluator me = new MatchEvaluator(dyHelper.capText);
            sql = Regex.Replace(sql, @"\*[0-9_a-zA-Z]*?\*", me);
            cmd.CommandText = sql;
            return cmd;
        }

        DbTransaction getTran(Database db, string tranID, bool isOpen)
        {
            DbTransaction tran = null;
            if (tranID != null)
            {
                if (HttpContext.Current == null)
                    return null;//throw new ApplicationException("Get Tran Failure,HttpContext.Current is null");
                else
                {
                    if (HttpContext.Current.Items.Contains(tranID))
                    {
                        //一次隻能執行一個事務
                        tran = HttpContext.Current.Items[tranID] as DbTransaction;
                        Database tranDB = HttpContext.Current.Items["DB_" + tranID] as Database;
                        if (tran != null && tranDB != null && db != null && (tranDB == db || tranDB.IsSameConnection(db)))
                            return tran;
                        else
                        {
                            return null;
                        }
                    }
                    else if (isOpen)
                    {
                        tran = db.BeginTransaction();
                        HttpContext.Current.Items.Add(tranID, tran);
                        HttpContext.Current.Items.Add("DB_" + tranID, db);
                       
                    }
                }
            }
            return tran;
        }

        #region Parameter
        void addOutputParameters(DbCommand cmd, Dictionary<string, object> args)
        {
            if (args.ContainsKey("__OutputParams"))
            {
                Dictionary<string, object> outputParams = new Dictionary<string, object>();
                foreach (DbParameter p in cmd.Parameters)
                {
                    if (p.Direction == ParameterDirection.InputOutput
                        || p.Direction == ParameterDirection.Output
                        || p.Direction == ParameterDirection.ReturnValue)
                        outputParams.Add(p.ParameterName, p.Value);
                }
                args["__OutputParams"] = outputParams;
            }
        }


        void addProcParameters(Database db, DbCommand cmd, Dictionary<string, object> args)
        {
            if (args != null)
            {


                if (args.ContainsKey("__ADD_PARAMETERS") && args["__ADD_PARAMETERS"].ToString() == "1")
                {
                    foreach (string key in args.Keys)
                    {
                        if (!key.StartsWith("__"))
                            cmd.Parameters.Add(db.CreateParameter(key, args[key] ?? DBNull.Value));
                    }
                }
                else
                {
                    for (int i = cmd.Parameters.Count - 1; i >= 0; i--)
                    {
                        DbParameter p = cmd.Parameters[i];
                        if (p.Direction.Equals(ParameterDirection.Input) || p.Direction.Equals(ParameterDirection.InputOutput))
                        {
                            string argKey = p.ParameterName.Replace("@", "");   //sql server的參數會帶@
                            if (args.ContainsKey(argKey))
                            {
                                p.Value = args[argKey] ?? DBNull.Value;
                            }
                            else
                            {
                                //未傳參數，則使用procedure的默認參數 
                                //目前看應該不會影響到各系統，有的話，再修改
                                //kevin.zou 2010.12.04注
                                cmd.Parameters.RemoveAt(i);
                                //p.Value = DBNull.Value;
                            }
                        }
                    }
                }

            }
        }
        #endregion


        class SqlDyHelper
        {
            public SqlDyHelper(Database db, DbCommand cmd, Dictionary<string, object> args)
            {
                _db = db;
                _cmd = cmd;
                _args = args;
            }

            Dictionary<string, object> _args;
            DbCommand _cmd;
            Database _db;

            public string capText(Match match)
            {
                string mStr = match.ToString();
                string paramName = mStr.Substring(1, mStr.Length - 2);        //*paramName*
                if (!_args.ContainsKey(paramName))
                    throw new ApplicationException("The sql paramter is not provided(Param:" + paramName + ")");
                else
                {
                    string realParamName = (_db.DBType.Equals(DatabaseType.Oracle) ? "" : "@") + paramName;
                    if (!_cmd.Parameters.Contains(realParamName))
                    {
                        //不知道，為什么，如果用這種方式加Parameter，就會讓QueryReader，oracle查詢long欄位類型超慢，所以改成下面的方式代替
                        //原因暫時不明，可能是Database.ConfigureParameter方法有動到什么吧?以后有興趣再查
                        //_db.AddInParameter(_cmd, realParamName, _args[paramName]);
                        _cmd.Parameters.Add(_db.CreateParameter(realParamName, _args[paramName] ?? DBNull.Value));
                    }
                    return (_db.DBType.Equals(DatabaseType.Oracle) ? ":" : "@") + paramName;
                }
            }
        }




       
    }


}
