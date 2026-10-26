using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Hooks imported models' loose PBR maps (albedo/metallic/roughness/AO/normal/emissive/opacity)
// up to their extracted URP Lit materials. Run via Tools > Setup Imported Materials.
public static class ImportedMaterialSetup
{
    // Where a model's materials and textures live, and how its texture files are named
    class TextureSet
    {
        public string Label;
        public string MaterialsFolder;
        public string TexturesFolder;
        // {0} = material name, {1} = map suffix
        public string NameFormat = "{0}_{1}";
        public string Albedo = "albedo";
        public string Metallic = "metallic";
        public string Roughness = "roughness";
        public string AO = "AO";
        public string Normal = "normal";
        public string Emissive = "emissive";
        public string Opacity = "opacity";
    }

    static readonly TextureSet[] Sets =
    {
        new TextureSet
        {
            Label = "Lighthouse",
            MaterialsFolder = "Assets/Models/lighthouse/source/Export/Materials",
            TexturesFolder = "Assets/Models/lighthouse/source/Export/Textures",
        },
        new TextureSet
        {
            Label = "Cargo ship",
            MaterialsFolder = "Assets/Models/cargo-ship/source/Cargo ship/Materials",
            TexturesFolder = "Assets/Models/cargo-ship/textures",
            NameFormat = "cargo_fin1_lp_{0}_{1}",
            Albedo = "BaseColor",
            Metallic = "Metallic",
            Roughness = "Roughness",
            Normal = "Normal",
        },
    };

    static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    [MenuItem("Tools/Setup Imported Materials")]
    public static void Setup()
    {
        foreach (TextureSet set in Sets)
        {
            if (!AssetDatabase.IsValidFolder(set.MaterialsFolder))
                continue;

            int configured = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { set.MaterialsFolder }))
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (SetupMaterial(mat, set))
                    configured++;
            }
            Debug.Log($"{set.Label} material setup: configured {configured} materials.");
        }

        AssetDatabase.SaveAssets();
    }

    static bool SetupMaterial(Material mat, TextureSet set)
    {
        string name = mat.name;
        string albedo = FindTexture(set, name, set.Albedo);
        string metallic = FindTexture(set, name, set.Metallic);
        string roughness = FindTexture(set, name, set.Roughness);
        string ao = FindTexture(set, name, set.AO);
        string normal = FindTexture(set, name, set.Normal);
        string emissive = FindTexture(set, name, set.Emissive);
        string opacity = FindTexture(set, name, set.Opacity);

        if (albedo == null && normal == null && roughness == null)
            return false;

        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", Color.white);

        // Opacity lives in the albedo's alpha channel in URP
        if (albedo != null && opacity != null)
            albedo = PackAlbedoAlpha(set, name, albedo, opacity);
        if (albedo != null)
            mat.SetTexture("_BaseMap", LoadTexture(albedo, srgb: true));

        // URP wants metallic in R and smoothness (1 - roughness) in A of a single map
        if (metallic != null || roughness != null)
        {
            string packed = PackMetallicSmoothness(set, name, metallic, roughness);
            mat.SetTexture("_MetallicGlossMap", LoadTexture(packed, srgb: false));
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", LoadTexture(normal, srgb: false, isNormal: true));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (ao != null)
        {
            mat.SetTexture("_OcclusionMap", LoadTexture(ao, srgb: false));
            mat.EnableKeyword("_OCCLUSIONMAP");
        }

        if (emissive != null)
        {
            mat.SetTexture("_EmissionMap", LoadTexture(emissive, srgb: true));
            mat.SetColor("_EmissionColor", Color.white);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        if (opacity != null)
        {
            if (name.ToLower().Contains("glass"))
                MakeTransparent(mat);
            else
                MakeCutout(mat);
        }

        EditorUtility.SetDirty(mat);
        return true;
    }

    static string FindTexture(TextureSet set, string materialName, string suffix)
    {
        // Prefer png when the export contains both png and jpg versions
        foreach (string ext in Extensions)
        {
            string path = $"{set.TexturesFolder}/{string.Format(set.NameFormat, materialName, suffix)}{ext}";
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    static Texture2D LoadTexture(string path, bool srgb, bool isNormal = false)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool changed = false;
        if (isNormal && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }
        if (!isNormal && importer.sRGBTexture != srgb)
        {
            importer.sRGBTexture = srgb;
            changed = true;
        }
        if (changed)
            importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Reads the raw file so we get uncompressed pixels without touching import settings
    static Texture2D ReadPixels(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        tex.LoadImage(File.ReadAllBytes(path));
        return tex;
    }

    static string PackMetallicSmoothness(TextureSet set, string name, string metallicPath, string roughnessPath)
    {
        Texture2D metallic = metallicPath != null ? ReadPixels(metallicPath) : null;
        Texture2D roughness = roughnessPath != null ? ReadPixels(roughnessPath) : null;
        Texture2D size = roughness != null ? roughness : metallic;

        var output = new Texture2D(size.width, size.height, TextureFormat.RGBA32, false, true);
        var pixels = new Color[size.width * size.height];
        for (int y = 0; y < size.height; y++)
        {
            for (int x = 0; x < size.width; x++)
            {
                float u = (x + 0.5f) / size.width;
                float v = (y + 0.5f) / size.height;
                float m = metallic != null ? metallic.GetPixelBilinear(u, v).r : 0f;
                float r = roughness != null ? roughness.GetPixelBilinear(u, v).r : 0.5f;
                pixels[y * size.width + x] = new Color(m, m, m, 1f - r);
            }
        }
        output.SetPixels(pixels);

        return WritePng(output, $"{set.TexturesFolder}/{name}_MetallicSmoothness.png", srgb: false);
    }

    static string PackAlbedoAlpha(TextureSet set, string name, string albedoPath, string opacityPath)
    {
        Texture2D albedo = ReadPixels(albedoPath);
        Texture2D opacity = ReadPixels(opacityPath);

        var output = new Texture2D(albedo.width, albedo.height, TextureFormat.RGBA32, false, true);
        Color[] pixels = albedo.GetPixels();
        for (int y = 0; y < albedo.height; y++)
        {
            for (int x = 0; x < albedo.width; x++)
            {
                float u = (x + 0.5f) / albedo.width;
                float v = (y + 0.5f) / albedo.height;
                pixels[y * albedo.width + x].a = opacity.GetPixelBilinear(u, v).r;
            }
        }
        output.SetPixels(pixels);

        return WritePng(output, $"{set.TexturesFolder}/{name}_albedoAlpha.png", srgb: true);
    }

    static string WritePng(Texture2D texture, string path, bool srgb)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = srgb;
        importer.alphaIsTransparency = srgb;
        importer.SaveAndReimport();
        return path;
    }

    static void MakeCutout(Material mat)
    {
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
    }

    static void MakeTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }
}
