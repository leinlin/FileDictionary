using System.Collections.Generic;
using System.IO;

namespace Matix.Collection
{
    public class CRC32
    {
        private static readonly uint[] table;

        static CRC32()
        {
            uint polynomial = 0xEDB88320;
            table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }

                table[i] = crc;
            }
        }

        public static uint Compute(byte[] bytes)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in bytes)
            {
                crc = (crc >> 8) ^ table[(crc ^ b) & 0xFF];
            }

            return ~crc;
        }
    }
    
    public interface IFileDictionaryDelegate<TKey, TValue>
    {
        uint GetHashCode(TKey key);

        TValue DeserializeValue(byte[] data, int start, int len);
        int SerializeValue(TValue value, byte[] moveTempBuff);

        TKey DeserializeKey(byte[] data, int start, int len);
        int SerializeKey(TKey value, byte[] moveTempBuff);
    }
    
    public class IntStringFileDictionaryDelegate : IFileDictionaryDelegate<int, string>
    {
        public static IntStringFileDictionaryDelegate Default = new IntStringFileDictionaryDelegate();
        
        public uint GetHashCode(int key)
        {
            return (uint)key * 2654435761U;
        }

        public string DeserializeValue(byte[] data, int start, int len)
        {
            string value = System.Text.Encoding.UTF8.GetString(data, start, len);
            return value;
        }

        public int SerializeValue(string value, byte[] moveTempBuff)
        {
            int len = System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, moveTempBuff, 0);
            return len;
        }

        public unsafe int DeserializeKey(byte[] data, int start, int len)
        {
            int* intb;
            fixed (byte* b = data)
            {
                intb = (int*)(b + start);
            }
            
            return *intb;
        }

        public unsafe int SerializeKey(int value, byte[] moveTempBuff)
        {
            fixed (byte* b = moveTempBuff)
            {
                int* intb = (int*)b;
                *intb = value;
            }

            return 4;
        }
    }
    
    public class StringStringFileDictionaryDelegate : IFileDictionaryDelegate<string, string>
    {
        public static StringStringFileDictionaryDelegate Default = new StringStringFileDictionaryDelegate();
        
        public uint GetHashCode(string key)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(key);
            return CRC32.Compute(data);
        }

        public string DeserializeValue(byte[] data, int start, int len)
        {
            string value = System.Text.Encoding.UTF8.GetString(data, start, len);
            return value;
        }

        public int SerializeValue(string value, byte[] moveTempBuff)
        {
            int len = System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, moveTempBuff, 0);
            return len;
        }

        public string DeserializeKey(byte[] data, int start, int len)
        {
            string value = System.Text.Encoding.UTF8.GetString(data, start, len);
            return value;
        }

        public int SerializeKey(string key, byte[] moveTempBuff)
        {
            int len = System.Text.Encoding.UTF8.GetBytes(key, 0, key.Length, moveTempBuff, 0);
            return len;
        }
    }

    public class IntIntFileDictionaryDelegate : IFileDictionaryDelegate<int, int>
    {
        public static IntIntFileDictionaryDelegate Default = new IntIntFileDictionaryDelegate();
        public static byte[] buffer_cache = new byte[4096];
        public uint GetHashCode(int key)
        {
            return (uint)key * 2654435761U;
        }
    
        public unsafe int DeserializeValue(byte[] data, int start, int len)
        {
            int* intb;
            fixed (byte* b = data)
            {
                intb = (int*)b;
            }
            
            return *intb;
        }

        public unsafe int SerializeValue(int value, byte[] moveTempBuff)
        {
            fixed (byte* b = moveTempBuff)
            {
                int* intb = (int*)b;
                *intb = value;
            }

            return 4;
        }

        public unsafe int DeserializeKey(byte[] data, int start, int len)
        {
            int* intb;
            fixed (byte* b = data)
            {
                intb = (int*)b;
            }
            
            return *intb;
        }

        public unsafe int SerializeKey(int value, byte[] moveTempBuff)
        {
            fixed (byte* b = moveTempBuff)
            {
                int* intb = (int*)b;
                *intb = value;
            }

            return 4;
        }
    }
    
    public class IntStringArrayFileDictionaryDelegate : IFileDictionaryDelegate<int, string[]>
    {
        public static IntStringArrayFileDictionaryDelegate Default = new IntStringArrayFileDictionaryDelegate();
        public static byte[] buffer_cache = new byte[4096];
        public uint GetHashCode(int key)
        {
            return (uint)key * 2654435761U;
        }
    
        public unsafe string[] DeserializeValue(byte[] data, int start, int len)
        {
            string[] result = null;
            fixed (byte* bp = data)
            {
                byte* p = (bp + start);
                int count = (*(int*)p);
                p = p + 4;

                result = new string[count];

                for (int i = 0; i < count; i++)
                {
                    int strLen = (*(int*)p);
                    p = p + 4;
                    result[i] = System.Text.Encoding.UTF8.GetString(data, (int)(p - bp), strLen);
                    p = p + strLen;
                }
            }
            
            return result;
        }

        public int SerializeValue(string[] value, byte[] moveTempBuff)
        {
            MemoryStream ms = new MemoryStream(moveTempBuff);
            BinaryWriter bw = new BinaryWriter(ms);

            int start = 0;
            bw.Write(value.Length);
            start += 4;
            
            for (int i = 0, imax = value.Length; i < imax; i++)
            {
                var data = System.Text.Encoding.UTF8.GetBytes(value[i]);
                bw.Write(data.Length);
                start += 4;
                bw.Write(data);
                start += data.Length;
            }

            return start;
        }

        public unsafe int DeserializeKey(byte[] data, int start, int len)
        {
            int* intb;
            fixed (byte* b = data)
            {
                intb = (int*)(b + start);
            }
            
            return *intb;
        }

        public unsafe int SerializeKey(int value, byte[] moveTempBuff)
        {
            fixed (byte* b = moveTempBuff)
            {
                int* intb = (int*)b;
                *intb = value;
            }

            return 4;
        }
    }
    
    public class StringStringArrayFileDictionaryDelegate : IFileDictionaryDelegate<string, string[]>
    {
        public static StringStringArrayFileDictionaryDelegate Default = new StringStringArrayFileDictionaryDelegate();
        public static byte[] buffer_cache = new byte[4096];
        public uint GetHashCode(string key)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(key);
            return CRC32.Compute(data);
        }
    
        public string[] DeserializeValue(byte[] data, int start, int len)
        {
            MemoryStream ms = new MemoryStream(data);
            BinaryReader br = new BinaryReader(ms);
            int count = br.ReadInt32();

            string[] result = new string[count];

            for (int i = 0; i < count; i++)
            {
                int strLen = br.ReadInt32();
                br.Read(buffer_cache, 0, strLen);
                result[i] = System.Text.Encoding.UTF8.GetString(buffer_cache, 0, strLen);
            }
            
            br.Close();
            
            return result;
        }

        public int SerializeValue(string[] value, byte[] moveTempBuff)
        {
            MemoryStream ms = new MemoryStream(moveTempBuff);
            BinaryWriter bw = new BinaryWriter(ms);

            int start = 0;
            bw.Write(value.Length);
            start += 4;
            
            for (int i = 0, imax = value.Length; i < imax; i++)
            {
                start += 4;
                int len = System.Text.Encoding.UTF8.GetBytes(value[i], 0, value[i].Length, moveTempBuff, start);
                bw.Write(len);
    
                bw.Seek(len, SeekOrigin.Current);
                start += len;
            }

            return start;
        }

        public string DeserializeKey(byte[] data, int start, int len)
        {
            string value = System.Text.Encoding.UTF8.GetString(data, start, len);
            return value;
        }

        public int SerializeKey(string key, byte[] moveTempBuff)
        {
            int len = System.Text.Encoding.UTF8.GetBytes(key, 0, key.Length, moveTempBuff, 0);
            return len;
        }
    }
    
    public class FileDictionaryDelegateFactory
    {
        public static IFileDictionaryDelegate<TKey, TValue> GetFileDictionaryDelegate<TKey, TValue>()
        {
            if (typeof(TKey) == typeof(string) && typeof(TValue) == typeof(string))
                return (IFileDictionaryDelegate<TKey, TValue>)StringStringFileDictionaryDelegate.Default;
            
            
            if (typeof(TKey) == typeof(string) && typeof(TValue) == typeof(string[]))
                return (IFileDictionaryDelegate<TKey, TValue>)StringStringArrayFileDictionaryDelegate.Default;

            if (typeof(TKey) == typeof(int) && typeof(TValue) == typeof(string))
                return (IFileDictionaryDelegate<TKey, TValue>)IntStringFileDictionaryDelegate.Default;

            if (typeof(TKey) == typeof(int) && typeof(TValue) == typeof(string[]))
                return (IFileDictionaryDelegate<TKey, TValue>)IntStringArrayFileDictionaryDelegate.Default;
            
            if (typeof(TKey) == typeof(int) && typeof(TValue) == typeof(int))
                return (IFileDictionaryDelegate<TKey, TValue>)IntIntFileDictionaryDelegate.Default;

            
            return null;
        }
    }

}