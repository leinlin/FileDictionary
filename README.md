![](https://socialify.git.ci/leinlin/FileDictionary/image?description=1&descriptionEditable=A%20project%20for%20ATRI%2CCSharp%20Collection.&forks=1&issues=1&language=1&logo=https%3A%2F%2Fi.loli.net%2F2020%2F11%2F12%2FYcINCkyp8vK2inD.png&owner=1&pattern=Circuit%20Board&stargazers=1&theme=Light)

### 👋 Here is ATRI FileDictionary - 一个有厨力的字典配置系统

## ✨ 特性概览 | Features
- 纯用算法实现的hash表，没有用到任何C#的字典功能便于修改优化性能
- 完全开源，没有引用任何第三方库
- 实现了几乎全部的C# 字典特性，使用起来非常方便
- 只查询的的表，内存消耗几乎为0.效率在骁龙845机器的读取上为1ms,可以读取100个数据


### 🚀 使用范例

编辑器下创建字典的文件
```
    var fileDictionary = new FileDictionary<int, string>("test.bin", 1024, true);
    // 添加数据
    for (int i = 0; i <= 100; i++)
    {
        fileDictionary[i] = "test" + i.ToString();
    }
    // 
    fileDictionary.Close()
```

runtime下读取文件
```
    var fileDictionary = new FileDictionary<int, string>("test.bin", 1024);
    // 添加
    for (int i = 0; i <= 100; i++)
    {
        fileDictionary[i] = "bf1ecbbf0a2d38e3d5c7f7ab43524985";
    }
    // 查询
    var value = fileDictionary[100];

    // 字典拥有修改和删除，但是使用的时候需要注意性能

    // 修改
    fileDictionary[100] = "new str";

    // 删除
    fileDictionary.Remove(100);

    // 遍历
    foreach (var item in fileDictionary)
    {
        // your code
    }

    // 转字典
    var dict = fileDictionary.ToDictionary();
```

### 多字典查询
```
FileDictionary<string, bool> f = new FileDictionary<string, bool>("test.bin", 1024, true);
for (int i = 0; i < 10000; i++)
{
    f.Add(i.ToString(), i % 2 == 0);
}
f.Dispose();

FileDictionary<string, bool> f2 = new FileDictionary<string, bool>("test2.bin", 1024, true);
for (int i = 0; i < 10000; i++)
{
    f2.Add(i.ToString(), i % 2 == 0);
}
f2.Dispose();

FileDictionary<int, string> f3 = new FileDictionary<int, string>("test3.bin", 1024, true);
for (int i = 0; i < 10000; i++)
{
    f3.Add(i, i.ToString());
}
f3.Dispose();

FileDB fdb = new FileDB("file.fdb");
// 编辑器下直接合并为一个大DB
fdb.MergeEditor("test.bin", "test2.bin", "test3.bin");

// 运行中合并FileDictionary 为DB，这样可以避免热更的时候更出去过大的文件
fdb.MergeRuntime("test.bin", "test2.bin", "test3.bin");

bool v;
if (fdb.TryGetValueByTableName("test", "0", out v))
{
    print("test success:" + v);
}
else
{
    print("test fail");
}

if (fdb.TryGetValueByTableName("test2", "3", out v))
{
    print("test2 success:" + v);
}
else
{
    print("test2 fail");
}

string strV;
if (fdb.TryGetValueByTableName("test3", 100, out strV))
{
    print("test3 success:" + strV);
}
else
{
    print("test3 fail");
}
```


### 📱 目前库里面支持的类型有 
- <int,string>
- <int, bool>
- <int, int>
- <int, string[]>
- <int, byte[]>
- <string,string>
- <string, bool>
- <string, int>
- <string, string[]>
- <string, byte[]>

### 📖 使用疑问
- 添加新的字典类型：如果需要支持新的kv类型，可以 继承 IFileDictionaryDelegate<TKey, TValue>接口实现对应的序列化、反序列化、hashcode。最后在在FileDictionaryDelegateFactory的GetFileDictionaryDelegate中添加对应的代码。
- probuf或者其他的二进制存储推荐使用 <int, byte[]>使用的时候反序列化，当然如果嫌弃麻烦可以自己写一个将二进制序列化为class的IFileDictionaryDelegate<TKey, TValue>接口类
- 如果需要把配置表转到内存中，推荐使用ToDictionary，然后直接放到内存的字典中，之后将本字典释放掉即可
- 如果不需要数据，建议直接Dipose掉FileDictionary
