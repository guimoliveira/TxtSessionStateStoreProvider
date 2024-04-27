using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.SessionState;

namespace TxtSession
{
    public class TxtSessionStateStoreProvider : SessionStateStoreProviderBase
    {
        static private String SESSIONS_FOLDER_PATH = @"C:\txt_sessions\";
        static private Dictionary<String, FileStream> lockIds = new Dictionary<String, FileStream>();

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, Int32 timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                            SessionStateUtility.GetSessionStaticObjects(context),
                            timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, String id, Int32 timeout)
        {
            try
            {
                File.WriteAllBytes(ComposePath(id), BitConverter.GetBytes(timeout));
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        public override void Dispose() {}

        public override void EndRequest(HttpContext context) {}

        public override SessionStateStoreData GetItem(HttpContext context, String id, out Boolean locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetItem(false, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, String id, out Boolean locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetItem(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override void InitializeRequest(HttpContext context) {}

        public override void ReleaseItemExclusive(HttpContext context, String id, object lockId)
        {
            try
            {
                if (lockId != null)
                {
                    ((FileStream)lockId).Close();
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        public override void RemoveItem(HttpContext context, String id, object lockId, SessionStateStoreData item)
        {
            try
            {
                if (lockId != null)
                {
                    ((FileStream)lockId).Close();
                }
                File.Move(ComposePath(id), ComposePath(id + ".abandoned"));
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        public override void ResetItemTimeout(HttpContext context, String id) {}

        public override void SetAndReleaseItemExclusive(HttpContext context, String id, SessionStateStoreData item, object lockId, Boolean newItem)
        {
            FileStream fs = (FileStream)lockId;
            if (fs == null)
            {
                fs = new FileStream(ComposePath(id), FileMode.OpenOrCreate, FileAccess.Write);
            }
            try
            {
                fs.Position = 4;
                BinaryWriter bw = new BinaryWriter(fs);
                ((SessionStateItemCollection)item.Items).Serialize(bw);
                fs.SetLength(fs.Position);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
            finally
            {
                fs.Close();
            }
        }

        public override Boolean SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        private SessionStateStoreData GetItem(Boolean exclusive, HttpContext context, String id, out Boolean locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            FileStream fs = null;
            String path = ComposePath(id);

            lockAge = DateTime.Now - File.GetLastAccessTime(path);
            lockId = lockIds.ContainsKey(id) ? lockIds[id] : null;
            actions = SessionStateActions.None;
            locked = false;

            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                fs = new FileStream(path, FileMode.Open, exclusive ? FileAccess.ReadWrite : FileAccess.Read);

                if (fs.Length < 4)
                {
                    fs.Close();
                    return null;
                }

                lockId = fs;
                lockIds[id] = fs;
                
                BinaryReader br = new BinaryReader(fs);
                Int32 timeout = br.ReadInt32();
                SessionStateItemCollection items = new SessionStateItemCollection();

                if (fs.Length > 4)
                {
                    items = SessionStateItemCollection.Deserialize(br);
                }

                return new SessionStateStoreData(items, SessionStateUtility.GetSessionStaticObjects(context), timeout);
            }
            catch
            {
                if (fs != null)
                {
                    fs.Close();
                }
                locked = true;
                return null;
            }
            finally
            {
                if (fs != null && !exclusive)
                {
                    fs.Close();
                }
            }
        }

        static private String ComposePath(String id)
        {
            return SESSIONS_FOLDER_PATH + "session_" + id + ".txt";
        }

        static private void Log(String str)
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(ComposePath("exceptions_" + DateTime.Today.ToString("yyyyMMdd")), FileMode.Append, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(str);
                sw.Flush();
            }
            finally
            {
                fs.Close();
            }
        }
    }
}