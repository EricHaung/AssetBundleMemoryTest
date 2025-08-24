### 規格

Unity版本：2022.3.56f1  
測試用裝置：Samasug Galaxy S10+

### 支援的資產型別

目前 `config.json` 的 `"type"` 欄位支援以下類別：
```csharp
// 核心類型
GameObject
Sprite
TextAsset

// 美術資產
Texture2D
Material
Mesh
Shader

// 動畫 / 音訊 / 視訊
AnimationClip
AudioClip
VideoClip

// 字型 / TMP（需安裝 TextMeshPro 套件）
Font
TMP_FontAsset
TMP_SpriteAsset

// 場景
Scene   // 注意：需用 SceneManager.LoadSceneAsync 載入
```

### Config格式範例
```json
{
    "version": 1,
    "bundle_urls": [
        "https://raw.githubusercontent.com/EricHaung/AssetBundleMemoryTest/main/AssetBundles/goblin_sprite_20250824_125412.ab"
    ],
    "assets": [
        {
            "path": "ui/goblin_sprite",
            "type": "Texture2D"
        }
    ]
}
```