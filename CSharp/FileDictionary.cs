using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Matix.Collection
{
    public unsafe class FileDictionary<TKey, TValue> : IDictionary<TKey, TValue>
        where TKey : IEquatable<TKey>
    {
        #region member

        private IFileDictionaryDelegate<TKey, TValue> funs;

        static byte[] intBuff = new byte[4];
        const int MOVE_BUFF_SIZE = 4096;
        const int SIZE_COUNT = 2;
        const int MAX_CONFLIGCT_TIME = 8;
        static byte[] moveTempBuff = new byte[MOVE_BUFF_SIZE];

        static byte[] SerializeTempBuff = new byte[65536];
        private FileStream _fs;
        private string _fileName;
        private int _capacity = 0;
        private int _dataOffset = 0;
        private int _size = 0;

        #endregion

        ~FileDictionary()
        {
            if (_fs != null)
            {
                _fs.Flush();
                _fs.Close();
            }
        }

        #region private

        public static int FindNextPowerOfTwo(int number)
        {
            if (number <= 0)
            {
                return 1; // 最小的2幂是2^0 = 1
            }

            int result = 1;
            while (result < number)
            {
                result <<= 1; // 左移一位，相当于乘以2
            }

            return result;
        }

        private void Resize(int capacity)
        {
            _fs.Seek(0, SeekOrigin.Begin);
            // 写入capacity
            WriteInt(capacity);
            WriteInt(_size);

            int* p;
            fixed (byte* bp = intBuff)
            {
                p = (int*)bp;
            }

            *p = -1;

            if (_dataOffset != 0) // 不是第一次resize
            {
                // 移动数据区的数据
                int endIndex = (int)_fs.Length;
                int delta = (capacity - _capacity) * 4;
                int index = endIndex;

                while (index > _dataOffset)
                {
                    int newIndex = index - MOVE_BUFF_SIZE;
                    if (_dataOffset > newIndex)
                    {
                        newIndex = _dataOffset;
                    }

                    int size = index - newIndex;

                    _fs.Seek(newIndex, SeekOrigin.Begin);
                    _fs.Read(moveTempBuff, 0, size);

                    _fs.Seek(newIndex + delta, SeekOrigin.Begin);
                    _fs.Write(moveTempBuff, 0, size);

                    index = newIndex;
                }

                // 把索引区的数据都读取到内存中
                byte[] oldData = new byte[4 * _capacity];
                int* oldDataPoint = null;

                _fs.Seek(4 * SIZE_COUNT, SeekOrigin.Begin);
                _fs.Read(oldData, 0, 4 * _capacity);

                // 开辟空间,并把索引区数据全部改成-1
                _fs.Seek(4 * SIZE_COUNT, SeekOrigin.Begin);
                for (int i = 0; i < capacity; i++)
                {
                    _fs.Write(intBuff, 0, 4);
                }

                _dataOffset = (capacity + SIZE_COUNT) * 4;
                int oldCapacity = _capacity;
                _capacity = capacity;
                // 重新插入 索引区数据
                fixed (byte* b = oldData)
                {
                    oldDataPoint = (int*)b;
                }

                for (int i = 0; i < oldCapacity; i++)
                {
                    int offset = *(oldDataPoint + i);
                    ResetVal(offset);
                }
            }
            else
            {
                // 开辟空间默认值为-1
                for (int i = 0; i < capacity; i++)
                {
                    _fs.Write(intBuff, 0, 4);
                }

                _dataOffset = (capacity + SIZE_COUNT) * 4;
                _capacity = capacity;
                _size = 0;
            }

            _fs.Flush();
        }

        private int ReadInt()
        {
            _fs.Read(intBuff, 0, 4);

            int* p;
            fixed (byte* bp = intBuff)
            {
                p = (int*)bp;
            }

            return *p;
        }

        private void WriteInt(int val)
        {
            int* p;
            fixed (byte* bp = intBuff)
            {
                p = (int*)bp;
            }

            *p = val;

            _fs.Write(intBuff, 0, 4);
        }

        private void ResetVal(int offset)
        {
            if (offset < 0)
            {
                return;
            }

            // 拿到原来的string值
            TKey key = GetKey(offset);

            DoSetVal(key, offset);
        }

        private void DoSetVal(TKey key, int offset)
        {
            // 计算CRC32 并计算出来一个索引
            int index = (int)(funs.GetHashCode(key) & (_capacity - 1));

            bool isReset = false;
            for (int i = 0; i < MAX_CONFLIGCT_TIME; i++)
            {
                if (SetVal(index + i, offset))
                {
                    isReset = true;
                    break;
                }
            }

            if (!isReset)
            {
                Resize(_capacity * 2);
                DoSetVal(key, offset);
            }
        }

        private bool SetVal(int index, int offset)
        {
            _fs.Seek((index + SIZE_COUNT) * 4, SeekOrigin.Begin);
            int v = ReadInt();
            if (v < 0)
            {
                _fs.Seek(-4, SeekOrigin.Current);
                WriteInt(offset);
                return true;
            }

            return false;
        }

        private TKey GetKey(int offset)
        {
            TKey value = default(TKey);
            _fs.Seek(offset + _dataOffset, SeekOrigin.Begin);
            int len = ReadInt();
            _fs.Read(moveTempBuff, 0, len);
            value = funs.DeserializeKey(moveTempBuff, 0, len);

            return value;
        }

        private bool TryGetValue(TKey key, out TValue value, out int ofs, out int index, out int oldValueLen)
        {
            value = default(TValue);
            ofs = 0;
            // 计算CRC32 并计算出来一个索引
            index = (int)(funs.GetHashCode(key) & (_capacity - 1));
            oldValueLen = 0;

            bool isFind = false;
            TKey k;
            int offset;
            for (int i = 0; i < MAX_CONFLIGCT_TIME; i++)
            {
                _fs.Seek((index + i + SIZE_COUNT) * 4, SeekOrigin.Begin);
                offset = ReadInt();
                // 没有值
                if (offset < 0)
                {
                    return false;
                }

                k = GetKey(offset);
                if (k.Equals(key))
                {
                    ofs = offset;
                    int len = ReadInt();
                    _fs.Read(SerializeTempBuff, 0, len);
                    oldValueLen = len;
                    //value = System.Text.Encoding.UTF8.GetString(moveTempBuff, 0, len);
                    value = funs.DeserializeValue(SerializeTempBuff, 0, len);
                    return true;
                }
            }

            return isFind;
        }

        #endregion

        #region public

        // 文件存在就从文件里面读取 capacity，文件不存在就用capacity new 一个 文件hash表出来
        public FileDictionary(string fileName, int capacity, bool isClear = false)
        {
            bool fileExit = File.Exists(fileName);
            if (isClear && fileExit)
            {
                File.Delete(fileName);
                fileExit = false;
            }

            _fs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            _fileName = fileName;
            funs = FileDictionaryDelegateFactory.GetFileDictionaryDelegate<TKey, TValue>();
            if (funs == null)
            {
                throw new Exception(string.Format("Implement IFileDictionaryDelegate<{0}, {1}>", typeof(TKey).Name, typeof(TValue).Name) );
            }

            // 文件不存在就先写入点东西
            if (!fileExit)
            {
                capacity = FindNextPowerOfTwo(capacity);
                Resize(capacity);
            }
            else
            {
                _capacity = ReadInt();
                _size = ReadInt();
                _dataOffset = (_capacity + SIZE_COUNT) * 4;
            }
        }

        public void Flush()
        {
            _fs.Flush();
        }

        public void Close()
        {
            _fs.Flush();
            _fs.Close();
            _fs = null;
        }

        #endregion

        #region interface

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            _fs.Seek(_dataOffset, SeekOrigin.Begin);
            
            for (int i = 0; i < _size; i++)
            {
                int len = ReadInt();
                _fs.Read(SerializeTempBuff, 0, len);
                var key = funs.DeserializeKey(SerializeTempBuff, 0, len);
                len = ReadInt();
                _fs.Read(SerializeTempBuff, 0, len);
                var value = funs.DeserializeValue(SerializeTempBuff, 0, len);

                yield return new KeyValuePair<TKey, TValue>(key, value);
            }
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(TKey key, TValue value)
        {
            TValue oldValue;
            int offset, index, oldValueLen;
            bool hasValue = TryGetValue(key, out oldValue, out offset, out index, out oldValueLen);
            int len = 0;
            if (hasValue)
            {
                if (oldValue.Equals(value)) return;
            }
            int newValueLen = funs.SerializeValue(value, SerializeTempBuff);
            if (hasValue && oldValueLen >= newValueLen)
            {
                int utf8Len = funs.SerializeKey(key, moveTempBuff);
                _fs.Seek(offset + _dataOffset + 4 + utf8Len, SeekOrigin.Begin);

                WriteInt(newValueLen);
                _fs.Write(SerializeTempBuff, 0, newValueLen);
            }
            else if (hasValue)
            {
                offset = (int)_fs.Length - _dataOffset;
                
                len = funs.SerializeKey(key, SerializeTempBuff);
                _fs.Seek(offset + _dataOffset, SeekOrigin.Begin);
                WriteInt(len);
                _fs.Write(SerializeTempBuff, 0, len);
                
                len = funs.SerializeValue(value, SerializeTempBuff);
                WriteInt(len);
                _fs.Write(SerializeTempBuff, 0, len);

                _fs.Seek((index + SIZE_COUNT) * 4, SeekOrigin.Begin);
                WriteInt(offset);
            }
            else
            {
                ++_size;
                _fs.Seek(4, SeekOrigin.Begin);
                WriteInt(_size);
                offset = (int)_fs.Length - _dataOffset;

                //len = System.Text.Encoding.UTF8.GetBytes(key, 0, key.Length, moveTempBuff, 0);
                len = funs.SerializeKey(key, SerializeTempBuff);
                _fs.Seek(offset + _dataOffset, SeekOrigin.Begin);
                WriteInt(len);
                _fs.Write(SerializeTempBuff, 0, len);

                //len = System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, moveTempBuff, 0);
                len = funs.SerializeValue(value, SerializeTempBuff);
                WriteInt(len);
                _fs.Write(SerializeTempBuff, 0, len);

                DoSetVal(key, offset);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _fs.Close();
            File.Delete(_fileName);
            _dataOffset = 0;
            _size = 0;

            _fs = new FileStream(_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var capacity = _capacity;
            _capacity = 0;
            Resize(capacity);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ContainsKey(item.Key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public int Count
        {
            get { return _size; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool ContainsKey(TKey key)
        {
            TValue value;
            bool find = TryGetValue(key, out value);
            return find;
        }

        public bool Remove(TKey key)
        {
            TValue oldValue;
            int offset, index, oldLen;
            bool hasValue = TryGetValue(key, out oldValue, out offset, out index, out oldLen);
            if (hasValue)
            {
                _fs.Seek((index + SIZE_COUNT) * 4, SeekOrigin.Begin);
                WriteInt(-1);
            }

            return hasValue;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int ofs, index, oldLen;
            return TryGetValue(key, out value, out ofs, out index, out oldLen);
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                bool find = TryGetValue(key, out value);
                if (!find)
                {
                    value = default(TValue);
                }

                return value;
            }
            set { Add(key, value); }
        }

        public ICollection<TKey> Keys
        {
            get { throw new NotImplementedException(); }
        }

        public ICollection<TValue> Values
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}