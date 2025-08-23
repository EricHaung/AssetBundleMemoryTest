### 規格

測試用Unity版本：2022.3.56f1

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
    "https://github.com/EricHaung/AssetBundleMemoryTest/AssetBundle/ui_v002.ab"
  ],
  "assets": [
    { "path": "UI/Prefabs/MenuRoot", "type": "GameObject" },
    { "path": "UI/Sprites/StartButton", "type": "Sprite" }
  ]
}
```