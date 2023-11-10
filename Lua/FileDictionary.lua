
local function class(className, super)
    -- 构建类
    local clazz = { __cname = className, super = super }
    if super then
        -- 设置类的元表，此类中没有的，可以查找父类是否含有
        setmetatable(clazz, { __index = super })
    end
    -- new 方法创建类对象
    clazz.new = function(...)
        -- 构造一个对象
        local instance = {}
        -- 设置对象的元表为当前类，这样，对象就可以调用当前类生命的方法了
        setmetatable(instance, { __index = clazz })
        if clazz.ctor then
            clazz.ctor(instance, ...)
        end
        return instance
    end
    return clazz
end

local lshift
local rshift
local bxor
local band
local bnot

local wb = "w+b"
local rb = "r+b"

if jit then
    local bit32 = require("bit")
    lshift = bit32.lshift
    rshift = bit32.rshift
    bxor = bit32.bxor
    band = bit32.band
    bnot = bit32.bnot
    wb = "wb+"
    rb = "rb+"
else
    -- lua 5.1写 移位计算 会报语法错误，所以直接用dostring绕过去
    lshift =  load([[return function(a, b) return a << b end]])()
    rshift = load([[return function(a, b) return a >> b end]])()
    bxor = load([[return function(a, b) return a ~ b end]])()
    band = load([[return function(a, b) return a & b end]])()
    bnot = load([[return function(a) return ~a end]])()
end
local strByte = string.byte
local strChar = string.char
local strLen = string.len
local type = type

-- CRC32表
local crc32_table = {}
for i = 0, 255 do
    local crc = i
    for j = 1, 8 do
        if crc % 2 == 1 then
            crc = bxor(0xEDB88320, rshift(crc, 1))
        else
            crc = rshift(crc, 1)
        end
    end
    crc32_table[i] = crc
end

-- 计算CRC32值
local function crc32(str)
    local crc = 0xFFFFFFFF
    for i = 1, #str do
        local byte = strByte(str, i)
        crc = bxor(crc32_table[bxor(byte, band(crc, 0xFF))], rshift(crc, 8))
    end
    local n = bnot(crc)

    if n < 0 then
        return 4294967296 + n  -- 4294967296 = 2^32
    else
        return n  -- 如果n是非负数，直接返回
    end
end

local function int32Hash(value)
    -- 使用位运算和一些常数来计算哈希码
    local hash = (value * 2654435761) % 4294967296
    return hash
end

local function calHash(value)
    local t = type(value)
    if t == "string" then
        return crc32(value)
    elseif t == "number" then
        return int32Hash(value)
    end
end

local function String2IntEx(str, index)
    if not index then
        index = 1
    end
    return strByte(str, index) +   strByte(str, index + 1) * 0X100 
    + strByte(str, index + 2)  * 0X10000 + strByte(str, index + 3) * 0X1000000
end

local function String2Int(str)
    return strByte(str, 1) +   strByte(str, 2) * 0X100 
    + strByte(str, 3)  * 0X10000 + strByte(str, 4) * 0X1000000
end

local function Int2String(num)
    local b1,b2,b3,b4 = 0
    b1 = num % 0X100
    num = (num - b1) / 0X100
    b2 = num % 0X100
    num = (num - b2) / 0X100
    b3 = num % 0X100
    num = (num - b3) / 0X100
    b4 = num % 0X100
    return strChar(b1, b2, b3, b4) 
end

local SIZE_COUNT = 2
local MAX_CONFLIGCT_TIME = 8
local MOVE_BUFF_SIZE = 4096
local INIT_BUFF = Int2String(0xFFFFFFFF)
local DEFAULT_VALUE = 0xFFFFFFFF

---@class FileDictionary
---@field protected _capacity number
---@field protected _size number
---@field protected _dataOffset number

---@type FileDictionary
local FileDictionary = class("FileDictionary")

local function FileExit(path)
    local fs = io.open(path, "rb")
    if fs then
        fs:close()
    end
    return fs
end


function FileDictionary:ctor(path, capacity, isClear)
    self:Open(path, capacity, isClear)
end

function FileDictionary:Open(path, capacity, isClear)
    local fileExit = FileExit(path)
    ---@type file
    local fs
    if capacity then
        if not fileExit or isClear then
            fs = io.open(path, wb)
            fileExit = false
        else
            fs = io.open(path, rb)
        end
    else
        fs = io.open(path, "rb")
    end

    self.isNumberKey = true
    if fileExit then
        self._fs = fs
        self._capacity = self:ReadInt()
        self._capacity_ = self._capacity - 1
        self._size = self:ReadInt()
        self._dataOffset = (self._capacity + SIZE_COUNT + MAX_CONFLIGCT_TIME) * 4
    elseif capacity then
        self._fs = fs
        self._capacity = 0
        self._size = 0
        self._dataOffset = 0
        capacity = self:FindNextPowerOfTwo(capacity)
        self:Resize(capacity)
    end
end

function FileDictionary:SetKeyString()
    self.isNumberKey = false
end

---@private
---@return number
function FileDictionary:ReadInt()
    return String2Int(self._fs:read(4))
end

---@param val string
function FileDictionary:WriteInt(val)
    self._fs:write(Int2String(val))
end

local ReadInt = FileDictionary.ReadInt
local WriteInt = FileDictionary.WriteInt

---@private
---@return boolean
function FileDictionary:GetKey(offset)
    local fs = self._fs
    fs:seek("set", offset + self._dataOffset)
    local len = ReadInt(self)

    return fs:read(len)
end

local GetKey = FileDictionary.GetKey

---@private
function FileDictionary:FindNextPowerOfTwo(number)
    if (number <= 0) then
        return 1 -- 最小的2幂是2^0 = 1
    end

    local result = 1
    while (result < number) do
        result = lshift(result, 1) -- 左移一位，相当于乘以2
    end

    return result
end

---@private
function FileDictionary:Resize(capacity)
    local _fs = self._fs
    local _dataOffset = self._dataOffset
    local _capacity = self._capacity

    _fs:seek("set", 0)
    -- 写入capacity
    WriteInt(self, capacity)
    WriteInt(self, self._size)

    if (_dataOffset ~= 0) then
        -- 移动数据区的数据
        local endIndex = _fs:seek("end");
        local delta = (capacity - _capacity) * 4;
        local index = endIndex;

        while (index > _dataOffset) do
            local newIndex = index - MOVE_BUFF_SIZE;
            if (_dataOffset > newIndex) then
                newIndex = _dataOffset
            end
            local size = index - newIndex;

            _fs:seek("set", newIndex)
            local tmp = _fs:read(size)

            _fs:seek("set", newIndex + delta)
            _fs:write(tmp)

            index = newIndex
        end

        -- 把索引区的数据都读取到内存中

        _fs:seek("set", 4 * SIZE_COUNT)
        local oldData = _fs:read(4 * (_capacity + MAX_CONFLIGCT_TIME))
        -- 开辟空间,并把索引区数据全部改成-1
        _fs:seek("set", 4 * SIZE_COUNT)
        for _ = 1,capacity + MAX_CONFLIGCT_TIME do
            _fs:write(INIT_BUFF)
        end

        self._dataOffset = (capacity + SIZE_COUNT + MAX_CONFLIGCT_TIME) * 4
        self._capacity = capacity
        self._capacity_ = self._capacity - 1
        -- 重新插入 索引区数据

        for i = 0,_capacity - 1 do
            local offset = String2IntEx(oldData, i * 4 + 1)
            if (offset ~= DEFAULT_VALUE) then
                self:ResetVal(offset)
            end
        end
    else
        -- 开辟空间默认值为-1
        for _ = 1,capacity + MAX_CONFLIGCT_TIME do
            _fs:write(INIT_BUFF)
        end
        self._dataOffset = (capacity + SIZE_COUNT + MAX_CONFLIGCT_TIME) * 4;
        self._capacity = capacity
        self._capacity_ = self._capacity - 1
        self._size = 0;
    end

    _fs:flush()
end

---@param offset number
function FileDictionary:ResetVal(offset)
    if (offset == DEFAULT_VALUE) then
        return
    end

    -- 拿到原来的string值
    local key = GetKey(self, offset)
    self:DoSetVal(key, offset)
end

---@private
---@param key string
---@param offset number
function FileDictionary:DoSetVal(key, offset)
    -- 计算CRC32 并计算出来一个索引
    local index = band(calHash(key), self._capacity_)

    local isReset = false
    for i = 0,MAX_CONFLIGCT_TIME do
        if (self:SetVal(index + i, offset)) then
            isReset = true
            break
        end
    end

    if (not isReset) then
        self:Resize(self._capacity * 2);
        self:DoSetVal(key, offset);
    end
end

---@private
---@param index number
---@param offset number
function FileDictionary:SetVal(index, offset)
    local fs = self._fs
    fs:seek("set", (index + SIZE_COUNT) * 4)
    local v = ReadInt(self)
    if (v == DEFAULT_VALUE) then
        fs:seek("cur", -4)
        WriteInt(self, offset)
        return true
    end
    return false
end

---region public

function FileDictionary:GetSize()
    return self._size
end

---@param key string
function FileDictionary:TryGetValue(key)
    if not self._fs then
        error("file not exit")
        return false
    end

    if type(key) == "number" then 
        key = Int2String(key)
    end

    local fs = self._fs
    local isFind = false
    local value = ""
    local offset = 0
    local index = calHash(key) % self._capacity

    for i = 0,MAX_CONFLIGCT_TIME do
        fs:seek("set", (index + i + SIZE_COUNT) * 4)
        offset = ReadInt(self)

        if (offset == DEFAULT_VALUE) then
            return false
        end
        local k = GetKey(self, offset)
        if (k == key) then
            local len = ReadInt(self)
            value = fs:read(len)
            isFind = true
            break
        end
    end

    return isFind,value,offset,index
end

---@param value string
---@param key string
function FileDictionary:SetValue(key, value)
    if not self._fs then
        error("file not exit")
        return false
    end

    -- 只支持int的key，不支持long的
    if type(key) == "number" then 
        key = Int2String(key)
    end

    local hasValue,oldValue,offset,index = self:TryGetValue(key)

    local len = 0;
    if (hasValue) then
        if (oldValue == value) then return end
    end
    local fs = self._fs
    local _dataOffset = self._dataOffset

    if (hasValue and strLen(oldValue) >= strLen(value)) then
        local utf8Len = strLen(key)
        fs:seek("set", offset + _dataOffset + 4 + utf8Len);

        WriteInt(self, strLen(value))
        fs:write(value)
    elseif (hasValue) then
        offset = fs:seek("end") - _dataOffset

        len = strLen(key)
        fs:seek("set", offset + _dataOffset)
        WriteInt(self, len)
        fs:write(key)

        len = strLen(value)
        WriteInt(self, len)
        fs:write(value)

        fs:seek("set", (index + SIZE_COUNT) * 4)
        WriteInt(self, offset)
    else
        self._size = self._size + 1
        fs:seek("set", 4)
        WriteInt(self, self._size)
        offset = fs:seek("end") - _dataOffset

        len = strLen(key)
        fs:seek("set", offset + _dataOffset)
        WriteInt(self, len)
        fs:write(key)

        len = strLen(value)
        WriteInt(self, len)
        fs:write(value)

        self:DoSetVal(key, offset);
    end
end


function FileDictionary:RemoveKey(key)
    if not self._fs then
        error("file not exit")
        return false
    end

    -- 只支持int的key，不支持long的
    if type(key) == "number" then 
        key = Int2String(key)
    end
    local hasValue,oldValue,offset,index = self:TryGetValue(key)

    if (hasValue) then
        self._fs:seek("set", (index + SIZE_COUNT) * 4)
        WriteInt(self, DEFAULT_VALUE)
        self._size = self._size - 1
        self._fs:seek("set",  4)
        WriteInt(self, self._size)
    end
end

function FileDictionary:ToLuaTable()
    if not self._fs then
        error("file not exit")
        return false
    end

    local fs = self._fs
    local _capacity = self._capacity
    local _dataOffset = self._dataOffset

    local tb = {}
    for i = 0,_capacity - 1 do
        fs:seek("set", 4 * (SIZE_COUNT + i))
        local offset = String2Int(fs:read(4))
        if (offset ~= DEFAULT_VALUE) then
            fs:seek("set", offset + _dataOffset)
            local len = ReadInt(self)
            local key = fs:read(len)
            len = ReadInt(self)
            local value = fs:read(len)
            tb[key] = value
        end
    end

    return tb
end

function FileDictionary:ToLuaTable()
    if not self._fs then
        error("file not exit")
        return false
    end

    local fs = self._fs
    local _capacity = self._capacity
    local _dataOffset = self._dataOffset

    local tb = {}
    for i = 0,_capacity - 1 do
        fs:seek("set", 4 * (SIZE_COUNT + i))
        local offset = String2Int(fs:read(4))
        if (offset ~= DEFAULT_VALUE) then
            fs:seek("set", offset + _dataOffset)
            local len = ReadInt(self)
            local key = fs:read(len)
            len = ReadInt(self)
            local value = fs:read(len)
            tb[key] = value
        end
    end

    return tb
end

-- 注意这里面如果数据被修改 删除过 会报错
function FileDictionary:pairs()
    if not self._fs then
        error("file not exit")
        return false
    end

    local fs = self._fs
    local _dataOffset = self._dataOffset

    local i = 1
    local count = self._size
    local offset = _dataOffset
    -- 闭包函数
    return function ()
        if i > count then
            return
        end
        fs:seek("set", offset)
        local len = ReadInt(self)
        local key = fs:read(len)
        if self.isNumberKey then
            key = String2Int(key)
        end
        offset = offset + len + 4
        len = ReadInt(self)
        local value = fs:read(len)
        offset = offset + len + 4
        i = i + 1

        return key,value
    end
 end

function FileDictionary:Close()
    if self._fs then
        self._fs:close()
    end
end


---endregion

return FileDictionary
