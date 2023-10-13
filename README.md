# FileDictionary
==============

运用文件系统存储数据，并添加查询功能的字典结构，拥有几乎为0的内存消耗。可用于在编辑器打包的时候预生成配置表，然后使用这个字典结构查询并使用数据。

## C#使用范例
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
```

## 添加新的字典类型
目前库里面支持的类型有 <int,string> 、 <int, int> 、 <int, string[]>、 <string, string>、<string, string[]>，如果需要支持新的kv类型，可以 继承 IFileDictionaryDelegate<TKey, TValue>接口实现对应的序列化、反序列化、hashcode。最后在在FileDictionaryDelegateFactory的GetFileDictionaryDelegate中添加对应的代码。

## 性能
在骁龙845上面的测试，100次查询 <string,string>查询 大概 1ms左右，可以满足小规模的查询</br>
例如unity 加载AB的依赖表,或者汉化表文字表，消息弹窗表