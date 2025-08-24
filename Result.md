# 圖片記憶體分配差異

以下為同一張圖片在 **壓縮** 與 **未壓縮** 狀態下的記憶體分配差異。

---

## 壓縮過
![壓縮過的圖片 - Unity Profiler](img/compress_texture2d_unity.png)  
![壓縮過的圖片 - 細節](img/texture_compress.png)

---

## 沒壓縮過
![沒壓縮過的圖片 - Unity Profiler](img/uncompress_texture2d_unity.png)  
![沒壓縮過的圖片 - 細節](img/texture_uncompress.png)

---

**觀察重點**  
差異主要出現在 **Graphics Size**。  
其他類型（Native Size、Managed Size）屬於貼圖的 **Meta Data**，因此不會因壓縮與否而有明顯差異。

---

## 各類別意義說明
以下爲詢問AI簡述各類別代表意義
### Native Size
- 代表物件在 Unity **C++ Engine (Native Heap)** 端所佔用的記憶體。  
- 對 `Texture2D` 而言，這部分儲存的是 **結構描述資料**，例如：
  - 貼圖寬高
  - 格式
  - Mipmap 資訊
- 通常不大（幾百 Bytes ～ 幾 KB）。

---

### Managed Size
- 代表物件在 **C# Managed Heap** 端所佔用的大小。  
- 僅是 C# 端的 **Texture2D 包裝物件**（reference/指標），不包含像素資料。  
- 通常非常小（十幾 Bytes）。

---

### Graphics Size
- 代表 **GPU 記憶體 (VRAM)** 或 **Graphics Driver 分配區塊**內，存放實際 **像素資料**的大小。  
- `Texture2D` 的像素 Buffer 在這裡存放，通常是主要的記憶體消耗來源。  
- 例如：某張壓縮後的貼圖大小為 **128 KB**，就是它在 GPU 端的實際佔用量。

---

## AssetBundle 使用情況

本次測試設計了六種不同情境：

1. **空場景**：什麼都沒有載入  
2. **單一圖片包**：載入一個只包含一張圖片素材的 AssetBundle，並載入其中的貼圖  
3. **兩個單圖包**：載入兩個分別只包含一張圖片的 AssetBundle，並載入所有貼圖  
4. **十圖大包**：載入一個包含 10 張圖片素材的 AssetBundle，並載入所有貼圖  
5. **十圖大包 (單張載入)**：載入一個包含 10 張圖片素材的 AssetBundle，但僅載入其中一張貼圖  
6. **兩個單圖包 (單張載入)**：載入兩個分別只包含一張圖片素材的 AssetBundle，但僅載入其中一個 Bundle 的貼圖  

---

## 🔎 檢驗結果與觀察

載入 AssetBundle 後，記憶體配置會出現以下變化：

5. **Managed / Managed Objects / AssetBundle**  
   - 出現 **UnityEngine.AssetBundle** 的 C# wrapper  
   - 大小固定 **12 B**

1. **Native / UnitySystem / AssetBundle**  
   - 出現 **LoadingCache**  
   - 大小固定 **1 MB**

2. **Native / UnitySystem / SerializedFile**  
   - 出現 **archive:CAB...** 開頭的記憶體區塊  
   - 大小 **不固定**

3. **Native / Native Objects / AssetBundle**  
   - 出現已載入的 **AssetBundle 物件**  
   - 大小 **不固定**

4. **Native / Native Objects / (對應素材類型)**  
   - 若載入圖片，會出現 **Texture2D**  
   - 大小對應於 **Native Size**（約 **440 B**）

5. **Managed / Managed Objects / (對應素材類型)**  
   - 若載入圖片，會出現 **Texture2D** 的 C# wrapper  
   - 大小對應於 **Managed Size**（約 **12 B**）

6. **Graphics**  
   - 若載入圖片，會額外出現 **Texture2D** 的 GPU 記憶體佔用  
   - 大小對應於 **Graphics Size**


# 各情境下的記憶體變化

| 測試類別               | AssetBundle | SerializedFile         | Native Texture2D | Managed Texture2D | Graphics Texture2D |
|------------------------|-------------|------------------------|------------------|-------------------|--------------------|
| 空場景                 | 0 KB        | 0 KB                   | 0 B              | 0 B               | 0 KB               |
| 單一圖片包             | 4.5 KB      | 172.2 KB               | 440 B            | 12 B              | 128 KB             |
| 兩個單圖包             | 9.0 KB      | 172.3 + 172.2 KB       | 440 B × 2        | 12 B × 2          | 128 KB × 2         |
| 十圖大包               | 5.2 KB      | 171.0 KB               | 440 B × 10       | 12 B × 10         | 128 KB × 10        |
| 十圖大包 (單張載入)    | 5.2 KB      | 171.0 KB               | 440 B            | 12 B              | 128 KB             |
| 兩個單圖包 (單張載入)  | 9.0 KB      | 172.3 + 172.2 KB       | 440 B            | 12 B              | 128 KB             |

---

## 總結

根據表格觀察，可得出以下結論：

1. **AssetBundle 大小**  
   - 與圖片數量無強烈線性關係。  
   - 多圖合併在同一個 Bundle 中，AssetBundle 占用反而更小（例：10 張圖片只佔 5.2 KB）。

2. **SerializedFile 占用**  
   - 每個 AssetBundle 對應一個 SerializedFile。  
   - 即使只載入部分圖片，SerializedFile 的大小仍與整個 AssetBundle 綁定，不會隨實際載入數量縮小。

3. **Native / Managed Texture2D**  
   - 只會隨實際載入的圖片數量而變化。  
   - 單張圖片固定 **440 B (Native)** + **12 B (Managed)**。

4. **Graphics Texture2D (VRAM)**  
   - 僅依實際載入的貼圖數量增加。  
   - 每張圖固定佔用 **128 KB**。  
   - 即使 AssetBundle 內有多張圖片，未載入的圖片不會佔用 GPU 記憶體。


# Prefab

## AssetBundle 使用情況

本次測試設計了六種不同情境：

1. **空場景**：什麼都沒有載入  
2. **空節點**：載入一個只包含一個Prefab的 AssetBundle，該Prefab只有一個EmptyObject，並載入AssetBundle內所有Prefab  
8. **兩個空節點**：載入兩個分別只包含一個Prefab的 AssetBundle，該Prefab只有一個EmptyObject，並載入AssetBundle內所有Prefab  
3. **三階圓滿二元樹**：載入一個只包含一個Prefab的 AssetBundle，該Prefab呈現三階圓滿二元樹的結構，每一個節點都是空物件，並載入AssetBundle內所有Prefab   
7. **三階圓滿二元樹 (帶腳本)**：載入一個只包含一個Prefab的 AssetBundle，該Prefab呈現三階圓滿二元樹的結構，每一個節點都是空物件+一個基礎的MonoBehavior Class腳本，並載入AssetBundle內所有Prefab  
4. **五階圓滿四元樹**：載入一個只包含一個Prefab的 AssetBundle，該Prefab呈現五階圓滿四元樹的結構，每一個節點都是空物件，並載入AssetBundle內所有Prefab   
5. **五階圓滿四元樹大包**：載入一個包含四個Prefab的 AssetBundle，該Prefab呈現五階圓滿四元樹的結構，每一個節點都是空物件，並載入AssetBundle內所有Prefab  
6. **五階圓滿四元樹 (單載入)**：載入一個包含四個Prefab的 AssetBundle，該Prefab呈現五階圓滿四元樹的結構，每一個節點都是空物件，但只載入其中一個Prefab 

---

## 🔎 檢驗結果與觀察

載入 AssetBundle 後，記憶體配置會出現以下變化：

<!-- 5. **Managed / Managed Objects / AssetBundle**  
   - 出現 **UnityEngine.AssetBundle** 的 C# wrapper  
   - 大小固定 **12 B**

1. **Native / UnitySystem / AssetBundle**  
   - 出現 **LoadingCache**  
   - 大小固定 **1 MB**

2. **Native / UnitySystem / SerializedFile**  
   - 出現 **archive:CAB...** 開頭的記憶體區塊  
   - 大小 **不固定**

3. **Native / Native Objects / AssetBundle**  
   - 出現已載入的 **AssetBundle 物件**  
   - 大小 **不固定**

4. **Native / Native Objects / (對應素材類型)**  
   - 若載入圖片，會出現 **Texture2D**  
   - 大小對應於 **Native Size**（約 **440 B**）

5. **Managed / Managed Objects / (對應素材類型)**  
   - 若載入圖片，會出現 **Texture2D** 的 C# wrapper  
   - 大小對應於 **Managed Size**（約 **12 B**）

6. **Graphics**  
   - 若載入圖片，會額外出現 **Texture2D** 的 GPU 記憶體佔用  
   - 大小對應於 **Graphics Size** -->


<!-- # 各情境下的記憶體變化

| 測試類別               | AssetBundle | SerializedFile         | Native Texture2D | Managed Texture2D | Graphics Texture2D |
|------------------------|-------------|------------------------|------------------|-------------------|--------------------|
| 空場景                 | 0 KB        | 0 KB                   | 0 B              | 0 B               | 0 KB               |
| 單一圖片包             | 4.5 KB      | 172.2 KB               | 440 B            | 12 B              | 128 KB             |
| 兩個單圖包             | 9.0 KB      | 172.3 + 172.2 KB       | 440 B × 2        | 12 B × 2          | 128 KB × 2         |
| 十圖大包               | 5.2 KB      | 171.0 KB               | 440 B × 10       | 12 B × 10         | 128 KB × 10        |
| 十圖大包 (單張載入)    | 5.2 KB      | 171.0 KB               | 440 B            | 12 B              | 128 KB             |
| 兩個單圖包 (單張載入)  | 9.0 KB      | 172.3 + 172.2 KB       | 440 B            | 12 B              | 128 KB             | -->



