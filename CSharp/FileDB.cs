using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ATRI.Collection
{
    public class FileDB : IDisposable
    {
        #region member
        // fileOffset, subFileLen, subFile isCopyTo File
        private Dictionary<string, ValueTuple<int, int, bool, string>> tableInfo = new Dictionary<string, (int, int, bool, string)>();
        private Dictionary<string, object> tableDict = new Dictionary<string, object>();
        private FileStream _fs;
        private string _path;
        private bool _inited = false;
        #endregion

        public FileDB(string filePath)
        {
            _path = filePath;
        }

        #region public

        public bool TryGetValueByTableName<TKey, TValue>(string tableName, TKey key, out TValue value) where TKey : IEquatable<TKey>
        {
            Init();
            object dict;
            if (!tableDict.TryGetValue(tableName, out dict))
            {
                ValueTuple<int, int, bool, string> info;
                if (tableInfo.TryGetValue(tableName, out info))
                {
                    // copy value to mergefile
                    if (!info.Item3)
                    {
                        var tempBuff = FileDictionaryUtils.moveTempBuff;
                        FileStream subFile = new FileStream(info.Item4, FileMode.Open, FileAccess.Read);
                        int bytesRead;
                        _fs.Seek(info.Item1, SeekOrigin.Begin);
                        while ((bytesRead = subFile.Read(tempBuff, 0, tempBuff.Length)) > 0)
                        {
                            _fs.Write(tempBuff, 0, bytesRead);
                        }
                    }
                    SubFileStream subFileStream = new SubFileStream(_fs, info.Item1, info.Item2);
                    dict = new FileDictionary<TKey, TValue>(subFileStream);
                    tableInfo.Remove(tableName);
                    tableDict.Add(tableName, dict);
                }
                else
                {
                    Debug.LogError("tableInfo not init");
                    value = default(TValue);
                    return false;
                }
            }

            FileDictionary<TKey, TValue> fd = (FileDictionary<TKey, TValue>)dict;
            return fd.TryGetValue(key, out value);
        }

        public void Dispose()
        {
            if (_fs != null)
            {
                _fs.Dispose();
            }
        }

        public void MergeRuntime(params string[] paths)
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
            
            tableInfo.Clear();
            _inited = false;

            int len = 0;
            foreach (var filePath in paths)
            {
                FileInfo fi = new FileInfo(filePath);
                if (!fi.Exists)
                {
                    throw new Exception(filePath + " not exit! please check");
                }
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (tableInfo.ContainsKey(fileName))
                {
                    Debug.LogError(fileName + " already exit!plz check in:[\n"+ string.Concat(paths, "\n") +"]");
                    return;
                }

                tableInfo.Add(fileName, (len, (int)fi.Length, false, filePath));
                len += (int)fi.Length;
            }
        }

        #if UNITY_EDITOR
        public void MergeEditor(params string[] paths)
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            tableInfo.Clear();
            _inited = false;
            
            FileStream fs = new FileStream(_path, FileMode.CreateNew, FileAccess.Write);
            BinaryWriter bfw = new BinaryWriter(fs);

            MemoryStream headMs = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(headMs);
            
            int len = 0;
            bw.Write(paths.Length);
            Dictionary<string, string> closeSet = new Dictionary<string, string>();
            foreach (var filePath in paths)
            {
                FileInfo fi = new FileInfo(filePath);
                if (!fi.Exists)
                {
                    throw new Exception(filePath + "not eixt! please check");
                }

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (closeSet.ContainsKey(fileName))
                {
                    throw new Exception(filePath + "file name the same with path:" + closeSet[fileName]);
                }

                byte[] strData = UTF8Encoding.UTF8.GetBytes(fileName);
                bw.Write(strData.Length);
                bw.Write(strData);
                bw.Write(len);
                bw.Write((int)fi.Length);

                len += (int)fi.Length;
            }

            int totalHeadLen = (int)headMs.Length;
            bw.Flush();
            bw.Close();

            bfw.Write(totalHeadLen);
            bfw.Write(headMs.ToArray());

            var tempBuff = FileDictionaryUtils.moveTempBuff;
            
            foreach (var filePath in paths)
            {
                int bytesRead;
                FileStream subFile = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                while ((bytesRead = subFile.Read(tempBuff, 0, tempBuff.Length)) > 0)
                {
                    fs.Write(tempBuff, 0, bytesRead);
                }
            }
            
            bfw.Flush();
            bfw.Close();
            
        }
        #endif
        
        #endregion

        private void Init()
        {
            if (_inited) return;
            _inited = true;
            
            // merge in runtime
            if (tableInfo.Count <= 0)
            {
                _fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.Read);
                BinaryReader br = new BinaryReader(_fs);
                int totalHeadLen = br.ReadInt32();
                int fileLen = br.ReadInt32();

                for (int i = 0; i < fileLen; i++)
                {
                    int strLen = br.ReadInt32();
                    byte[] datas = br.ReadBytes(strLen);
                    string fileName = UTF8Encoding.UTF8.GetString(datas);
                    int offset = br.ReadInt32();
                    int onefileLen = br.ReadInt32();

                    tableInfo.Add(fileName, (offset + totalHeadLen + 4, onefileLen, true, ""));
                }
            }
            else
            {
                _fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
        }
        
    }
}