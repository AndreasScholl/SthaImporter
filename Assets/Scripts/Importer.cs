using ModelBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Util;

public class Importer : MonoBehaviour
{
    const int HWRAM = 0x06000000;
    const int LWRAM = 0x00200000;

    MemoryManager _dataHW;
    MemoryManager _dataLW;
    byte[] _colorRam = null;

    public int _subDivide = 1;

    public bool _debugOutput = true;

    public Material _objectMaterial = null;
    public Material _objectTransparentMaterial = null;
    public Material _mapMaterial = null;
    public Material _mapTransparentMaterial = null;

    public List<Texture2D> Textures = new List<Texture2D>();
    private List<Texture2D> _groundTextures = null;
    private List<int> _groundPalettes = null;

    List<int> _paletteReferences;

    static public Importer Instance = null;

    private void Awake()
    {
        Instance = this;    
    }

    void Start()
    {
        if (VersionChecker.CheckImagePath() == false)
        {
            return; // no valid image
        }

        //string mapfile = "F:/M621.mdx";       // mine map
        //string mapFile = "F:/M101.MDX";       // tent town
        //string mapFile = "F:/M121.MDX";       // castle
        //string mapFile = "F:/M161.MDX";       // town
        //string mapFile = "F:/M201.MDX";       // church
        //string mapFile = "F:/M501.mdx";       // forest
        //string mapFile = "F:/M513.mdx";       // mine entrance
        //string mapFile = "F:/M553.mdx";       // clock room

        StartCoroutine(ImportAllMaps());
    }

    IEnumerator ImportAllMaps()
    {
        _dataHW = new MemoryManager(HWRAM);
        _dataLW = new MemoryManager(LWRAM);

        // get all map files
        string[] mapFiles = FileSystemHelper.GetFiles(VersionChecker.ImagePath, "*.mdx", SearchOption.AllDirectories);

        const float gridSize = 600f;
        const int gridRow = 10;
        int index = 0;
        foreach (string mapFile in mapFiles)
        {
            Debug.Log("MAP: " + mapFile);

            if (Path.GetFileNameWithoutExtension(mapFile).EndsWith("E") ||
                Path.GetFileNameWithoutExtension(mapFile).EndsWith("A") ||
                Path.GetFileNameWithoutExtension(mapFile).EndsWith("B"))
            {
                continue;   // skip .mdx files ending with E
            }

            GameObject map = ImportMap(mapFile);

            map.transform.position = new Vector3((index % gridRow) * gridSize, 0f, (index / gridRow) * gridSize);

            index++;

            //if (index > 3)
            //{
            //    break;
            //}

            yield return null;
        }
    }
    
    private GameObject ImportMap(string mapFile)
    {
        // get textures from chunk2 of mdx file
        //
        Textures.Clear();
        
        string textureFolder = "textures/" + Path.GetFileNameWithoutExtension(mapFile);
        Directory.CreateDirectory(textureFolder);

        byte[] mdxData = File.ReadAllBytes(mapFile);

        int chunk2Data = ByteArray.GetInt32(mdxData, 0x08);
        int chunk2Size = ByteArray.GetInt32(mdxData, 0x0c);

        Debug.Log("data: " + chunk2Data.ToString("X8"));
        Debug.Log("size: " + chunk2Size.ToString("X8"));

        int fileMemoryOffset = 0x00200000;

        MemoryManager memory = Importer.Instance.GetMemoryManager(fileMemoryOffset);
        int chunk2DataLocation = 0x0022c000;
        memory.LoadArray(mdxData, chunk2Data, chunk2Size, chunk2DataLocation);

        int chunk2HeaderMemory = memory.GetInt32(chunk2DataLocation);
        Debug.Log("chunk2Header: " + chunk2HeaderMemory.ToString("X8"));

        int textureIndex = 0;

        int dataPointer = memory.GetInt32(chunk2HeaderMemory + 0x84);
        byte[] decompressedTextures = Decompression.DecompressData(memory, dataPointer);
        if (decompressedTextures != null)
        {
            //File.WriteAllBytes("textures/tex1.bin", decompressedTextures);
            textureIndex = Loader.ImportTextureList(decompressedTextures, textureIndex, textureFolder);
        }

        dataPointer = memory.GetInt32(chunk2HeaderMemory + 0x88);
        decompressedTextures = Decompression.DecompressData(memory, dataPointer);
        //File.WriteAllBytes("textures/tex2.bin", decompressedTextures);
        if (decompressedTextures != null)
        {
            textureIndex = Loader.ImportTextureList(decompressedTextures, textureIndex, textureFolder);
        }

        dataPointer = memory.GetInt32(chunk2HeaderMemory + 0x8c);
        decompressedTextures = Decompression.DecompressData(memory, dataPointer);
        //File.WriteAllBytes("textures/tex3.bin", decompressedTextures);
        if (decompressedTextures != null)
        {
            textureIndex = Loader.ImportTextureList(decompressedTextures, textureIndex, textureFolder);
        }

        //dataPointer = memory.GetInt32(chunk2HeaderMemory + 0x90);
        //decompressedTextures = Decompression.DecompressData(memory, dataPointer);
        ////File.WriteAllBytes("textures/tex4.bin", decompressedTextures);
        //if (decompressedTextures != null)
        //{
        //    textureIndex = Loader.ImportTextureList(decompressedTextures, textureIndex, textureFolder);
        //}

        // import 3d models from chunk 5 of mdx file
        //
        GameObject map = Loader.ImportModel(mapFile);

        return map;
    }

    public MemoryManager GetMemoryManager(int address)
    {
        if (_dataLW.IsAddressValid(address))
        {
            return _dataLW;
        }
        else if (_dataHW.IsAddressValid(address))
        {
            return _dataHW;
        }

        return null;
    }

    public ModelData ImportModelsFromMemory(List<int> xpData, bool pointHeaderCheck = false, int romOffset = HWRAM, int offsetTranslations = 0, float gridOffset = 24f, int width = 256, int height = 256, bool debugOutput = false, string modelName = "map")
    {
        ModelData model = new ModelData();
        model.Init();

        ModelTexture modelTexture = new ModelTexture();
        modelTexture.Init(false, width, height);
        model.ModelTexture = modelTexture;

        for (int count = 0; count < xpData.Count; count++)
        {
            if (debugOutput)
            {
                Debug.Log("--------------------");
                Debug.Log("OBJNR: " + count);
            }

            try
            {
                ModelPart part = new ModelPart();
                part.Init();

                int offsetXPData = xpData[count];
                MemoryManager memory = GetMemoryManager(offsetXPData);

                int offsetPoints = memory.GetInt32(offsetXPData);
                int numPoints = memory.GetInt32(offsetXPData + 4);
                int offsetPolygons = memory.GetInt32(offsetXPData + 8);
                int numPolygons = memory.GetInt32(offsetXPData + 12);
                int offsetAttributes = memory.GetInt32(offsetXPData + 16);
                //int offsetNormals = memory.GetInt32(offsetXPData + 20);

                //if (pointHeaderCheck)
                //{
                //    int deltaPoints = offsetPolygons - offsetPoints;
                //    if (debugOutput)
                //    {
                //        Debug.Log("delta.points: " + deltaPoints + " should be: " + (numPoints * 12));
                //    }

                //    if (deltaPoints > 0)
                //    {
                //        if (deltaPoints != (numPoints * 12))
                //        {
                //            int value1 = memory.GetInt32(offsetPoints);
                //            int value2 = memory.GetInt32(offsetPoints + 4);
                //            int value3 = memory.GetInt32(offsetPoints + 8);

                //            if (debugOutput)
                //            {
                //                Debug.Log("  POINT-HEADER_1: " + value1.ToString("X8"));
                //                Debug.Log("  POINT-HEADER_2: " + value2.ToString("X8"));
                //                Debug.Log("  POINT-HEADER_3: " + value3.ToString("X8"));
                //            }

                //            offsetPoints += 12;
                //        }
                //    }
                //}

                if (debugOutput)
                {
                    Debug.Log("  points: " + numPoints + " offs: " + offsetPoints.ToString("X6"));
                    Debug.Log("  polys:  " + numPolygons + " offs: " + offsetPolygons.ToString("X6"));
                    Debug.Log("  attrib:  " + offsetAttributes.ToString("X6"));
                    //Debug.Log("  normals:  " + offsetNormals.ToString("X6"));
                }

                List<Vector3> points = new List<Vector3>();

                // read POINTS
                //
                for (int countPoints = 0; countPoints < numPoints; countPoints++)
                {
                    float x, y, z;

                    x = memory.GetFloat(offsetPoints);
                    y = memory.GetFloat(offsetPoints + 4);
                    z = memory.GetFloat(offsetPoints + 8);

                    offsetPoints += 12;

                    Vector3 point = new Vector3(x, y, z);

                    // scale down point
                    //point *= 0.0039f;

                    points.Add(point);

                    //Debug.Log(countPoints + ": " + x + " | " + y + " | " + z);
                }

                // read NORMALS
                //
                List<Vector3> normals = new List<Vector3>();

                //bool gotNormals = false;
                //if (memory.IsAddressValid(offsetNormals))
                //{
                //    gotNormals = true;
                //}
                //else
                //{
                //    Debug.Log(" MISSING NORMALS!");
                //}

                //for (int countPoints = 0; countPoints < numPoints; countPoints++)
                //{
                //if (gotNormals)
                //{
                //    float x = memory.GetFloat(offsetNormals);
                //    float y = memory.GetFloat(offsetNormals + 4);
                //    float z = memory.GetFloat(offsetNormals + 8);

                //    offsetNormals += 12;

                //    Vector3 normal = new Vector3(x, y, z) * -1f;
                //    normals.Add(normal);

                //    //Debug.Log(countPoints + ": " + x + " | " + y + " | " + z);
                //}
                //else
                //{
                //    normals.Add(Vector3.up);
                //}
                //}

                // read POLYGONS
                //
                //                Debug.Log(offsetPoints.ToString("X8") + " num: " + numPoints);
                //                Debug.Log(offsetPolygons.ToString("X8") + " num: " + numPolygons);

                //Debug.Log(offsetAttributes.ToString("X8") + " num: " + numPoints);

                for (int countPolygons = 0; countPolygons < numPolygons; countPolygons++)
                {
                    //                    Debug.Log(a + ", " + b + ", " + c + ", " + d);

                    ushort flag_sort = (ushort)memory.GetInt16(offsetAttributes);
                    ushort texno = (ushort)memory.GetInt16(offsetAttributes + 2);

                    ushort atrb = (ushort)memory.GetInt16(offsetAttributes + 4);
                    ushort color = (ushort)memory.GetInt16(offsetAttributes + 6);

                    ushort gstb = (ushort)memory.GetInt16(offsetAttributes + 8);
                    ushort dir = (ushort)memory.GetInt16(offsetAttributes + 10);

                    //ushort v1 = (ushort)memory.GetInt16(offsetAttributes + 12);
                    //ushort v2 = (ushort)memory.GetInt16(offsetAttributes + 14);

                    if (debugOutput)
                    {
                        Debug.Log("   FLAG_SORT: " + flag_sort.ToString("X4") +
                                  " TEXNO: " + (texno - 1) +
                                    " atrb: " + atrb.ToString("X4") +
                                    " color: " + color.ToString("X4") +
                                    " gstb: " + gstb.ToString("X4") +
                                    " dir: " + dir.ToString("X4")
                                    //" v1: " + v1.ToString("X4") +
                                    //" v2: " + v2.ToString("X4")
                                    );
                    }

                    bool cutOut = false;
                    bool halftransparent = false;

                    //const int CL_Shadow = 1;
                    //const int CL_Half = 2;
                    //const int CL_Trans = 3;
                    //const int CL_Gouraud = 4;

                    //int clBits = atrb & 3;
                    //if (clBits == CL_Trans)
                    //{
                    //    transparent = true;
                    //}
                    //else if (clBits == CL_Half)
                    //{
                    //    halftransparent = true;
                    //}

                    // INFO FROM SGL
                    //
                    // ATTRB bits:
                    //      MSBon (1 << 15)     MSB, write to frame buffer
                    //      HSSon (1 << 12)     high speed shrink on, should always be set
                    //      Window_In  (2 << 9)	display in window
                    //      Window_Out (3 << 9)	display outside window
                    //      MESHon(1 << 8)      display as mesh
                    //      ECdis (1 << 7)      use endcode as palette -> meaning?
                    //      SPdis (1 << 6)    d isplay clear pixels (disable cutout)

                    int sPdis = 1 << 6;
                    if ((atrb & sPdis) == 0)
                    {
                        cutOut = true;
                    }

                    bool wireFrame = false;
                    //int mESHon = 1 << 8;
                    //if ((atrb & mESHon) != 0)
                    //{
                    //    wireFrame = true;
                    //    halftransparent = true;
                    //    if (debugOutput)
                    //    {
                    //        Debug.Log("WIREFRAME! at " + offsetAttributes.ToString("X6"));
                    //    }
                    //}

                    //if ((flag_sort & (1 << 2)) != 0 && (flag_sort & 1) == 0)
                    //{
                    //    transparent = true;
                    //}
                    //else if ((flag_sort & (1 << 1)) != 0)
                    //{
                    //    halftransparent = true;
                    //}

                    //if ((dir & (1 << 7)) != 0)
                    //{
                    //    //halftransparent = true;
                    //}

                    // flag
                    bool doubleSided = false;
                    if ((flag_sort >> 8) == 1)
                    {
                        doubleSided = true;
                    }
                    //doubleSided = true; // debug test for inverted polygons

                    // atrb
                    int colorMode = (atrb & 0b111000) >> 3;
                    // dir
                    bool hflip = false;
                    bool vflip = false;
                    if ((dir & (1 << 4)) != 0)
                    {
                        hflip = true;
                    }
                    if ((dir & (1 << 5)) != 0)
                    {
                        vflip = true;
                    }
                    vflip = !vflip;
                    //hflip = !hflip;

                    // geometry
                    //
                    int a, b, c, d;
                    //bool negA, negB, negC, negD;

                    Vector3 faceNormal = Vector3.one;
                    float x = memory.GetFloat(offsetPolygons);
                    float y = memory.GetFloat(offsetPolygons + 4);
                    float z = memory.GetFloat(offsetPolygons + 8);
                    faceNormal = new Vector3(x, y, z);
                    offsetPolygons += 12;

                    a = memory.GetInt16(offsetPolygons);
                    b = memory.GetInt16(offsetPolygons + 2);
                    c = memory.GetInt16(offsetPolygons + 4);
                    d = memory.GetInt16(offsetPolygons + 6);
                    offsetPolygons += 8;

                    //Debug.Log("Polygon: " + countPolygons + " a: " + a + " b: " + b + " c: " + c + " d: " + d);

                    // color
                    Color rgbColor = ColorConversion.ConvertColor(color);
                    Color colorA = Color.gray;
                    Color colorB = Color.gray;
                    Color colorC = Color.gray;
                    Color colorD = Color.gray;

                    if (debugOutput)
                    {
                        //Debug.Log("word-obj: " + count + " = texture: " + texno + " gstb: " + gstb);
                        Debug.Log("TEXTURE: " + (texno - 1));
                        //Debug.Log("Colormode: " + colorMode + " tr: " + cutOut + " htr: " + halftransparent);
                        //Debug.Log("Color: " + rgbColor + " GSTBL: " + gstb);
                    }

                    if (Textures == null)
                    {
                        texno = 0;
                    }
                    else
                    {
                        // texno out of bounds?
                        if (texno < 0 || (texno >= Textures.Count))
                        {
                            texno = 0; // => no texture
                        }
                    }

                    Vector2 uvA, uvB, uvC, uvD;
                    uvA = Vector2.zero;
                    uvB = Vector2.zero;
                    uvC = Vector2.zero;
                    uvD = Vector2.zero;

                    if (texno >= 0 && texno < Textures.Count)
                    {
                        // texture handling
                        //
                        Texture2D texture = Textures[texno];

                        if (texture != null)
                        {
                            if (modelTexture.ContainsTexture(texture) == false)
                            {
                                modelTexture.AddTexture(texture, cutOut, halftransparent);
                            }

                            // add texture uv
                            bool rotate = false;
                            modelTexture.AddUv(texture, hflip, vflip, rotate, out uvA, out uvB, out uvC, out uvD);

                            colorA = Color.white;
                            colorB = Color.white;
                            colorC = Color.white;
                            colorD = Color.white;

                            rgbColor = Color.white;
                        }
                    }
                    else
                    {
                        // no texture, just color
                        //
                        // no texture, just color => create colored texture
                        //
                        Texture2D colorTex = new Texture2D(2, 2);
                        Color[] colors = new Color[4];
                        colors[0] = rgbColor;
                        colors[1] = rgbColor;
                        colors[2] = rgbColor;
                        colors[3] = rgbColor;
                        colorTex.SetPixels(colors);
                        colorTex.Apply();

                        modelTexture.AddTexture(colorTex, true, false);
                        bool rotate = false;
                        modelTexture.AddUv(colorTex, hflip, vflip, rotate, out uvA, out uvB, out uvC, out uvD);

                        colorA = Color.white;
                        colorB = Color.white;
                        colorC = Color.white;
                        colorD = Color.white;
                    }

                    offsetAttributes += 12;

                    Vector3 vA, vB, vC, vD;
                    Vector3 nA, nB, nC, nD;
                    nA = faceNormal;
                    nB = faceNormal;
                    nC = faceNormal;
                    nD = faceNormal;

                    //nA = normals[a];
                    //nB = normals[b];
                    //nC = normals[c];
                    //nD = normals[d];

                    //if (a < points.Count && b < points.Count && c < points.Count && d < points.Count)
                    {
                        vA = points[d];
                        vB = points[c];
                        vC = points[b];
                        vD = points[a];

                        //part.AddPolygon(vA, vB, vC, vD,
                        //                halftransparent, doubleSided,
                        //                colorA, colorB, colorC, colorD,
                        //                uvA, uvB, uvC, uvD,
                        //                nA, nB, nC, nD, _subDivide);
                        halftransparent = false;    // debug test -> opaque only
                        part.AddPolygon(vA, vD, vC, vB,
                                        halftransparent, doubleSided,
                                        colorA, colorD, colorC, colorB,
                                        uvA, uvD, uvC, uvB,
                                        nA, nD, nC, nB, _subDivide);
                    }
                }

                // get translation
                //
                float transX = 0f, transY = 0f, transZ = 0f;
                if (offsetTranslations != 0)
                {
                    transX = memory.GetFloat(offsetTranslations);
                    transY = memory.GetFloat(offsetTranslations + 4);
                    transZ = memory.GetFloat(offsetTranslations + 8);
                    offsetTranslations += 12;
                }
                else
                {
                    transX = (count / 10) * gridOffset;
                    transZ = (count % 10) * gridOffset;
                    transY = 0f;
                }

                part.Translation = new Vector3(transX, transY, transZ);
                model.Parts.Add(part);  // add part to part list
            }
            catch (Exception e)
            {
                Debug.LogError("Exception: " + e.Message + " on OBJ: " + count);
            }
        }

        model.ModelTexture.ApplyTexture();

        byte[] bytes = model.ModelTexture.Texture.EncodeToPNG();
        File.WriteAllBytes("textures/" + modelName + ".png", bytes);

        return model;
    }

    public Texture2D FlipYAndRemoveAlpha(Texture2D original)
    {
        Texture2D flipped = new Texture2D(original.width, original.height, TextureFormat.ARGB32, false);

        int xN = original.width;
        int yN = original.height;

        for (int i = 0; i < xN; i++)
        {
            for (int j = 0; j < yN; j++)
            {
                Color pixel = original.GetPixel(i, j);
                pixel.a = 1f;
                flipped.SetPixel(i, yN - j - 1, pixel);
            }
        }

        flipped.Apply();

        return flipped;
    }

    public GameObject CreateObject(ModelData modelData, string name, bool mapShader = true,
                                bool noRoot = false, List<int> partList = null, 
                                List<Vector3> translatonList = null,
                                List<Quaternion> rotationList = null,
                                List<Vector3> scaleList = null,
                                string textureFolder = "")
    {
        GameObject parent = new GameObject(name);
        GameObject root;

        if (noRoot == false)
        {
            root = new GameObject("root");
            modelData.Root = root;
            root.transform.parent = parent.transform;
            root.transform.localPosition = Vector3.zero;
            root.transform.localEulerAngles = Vector3.zero;
        }
        else
        {
            root = parent;
        }

        if (mapShader)
        {
            modelData.OpaqueMaterial = new Material(_mapMaterial);
        }
        else
        {
            modelData.OpaqueMaterial = new Material(_objectMaterial);
        }

        modelData.OpaqueMaterial.mainTexture = modelData.ModelTexture.Texture;

        if (mapShader)
        {
            modelData.TransparentMaterial = new Material(_mapTransparentMaterial);
        }
        else
        {
            modelData.TransparentMaterial = new Material(_objectTransparentMaterial);
        }

        modelData.TransparentMaterial.mainTexture = modelData.ModelTexture.Texture;

        GameObject partObject;

        int parts = modelData.Parts.Count;

        if (partList != null)
        {
            parts = partList.Count;
        }

        for (int partIndex = 0; partIndex < parts; partIndex++)
        {
            ModelPart part = modelData.Parts[partIndex];

            //if (partIndex == 1595)
            //{
            //    Debug.Log("...");
            //}

            if (partList != null)
            {
                part = modelData.Parts[partList[partIndex]];
            }

            Mesh mesh = part.CreateMesh();

            if (mesh != null)
            {
                if (part.DidNotProvideNormals)
                {
                    //mesh.Optimize();
                    mesh.RecalculateNormals(40f);
                    //Debug.Log(" CALC NORMALS FOR PART: " + partIndex);
                }
            }

            partObject = new GameObject("part" + partIndex);
            partObject.SetActive(true);

            part.OpaqueObject = partObject;
            if (part.Parent == -1)
            {
                partObject.transform.parent = root.transform;
            }
            else
            {
                partObject.transform.parent = modelData.Parts[part.Parent].Pivot.transform;
            }
            partObject.transform.localPosition = part.Translation;
            partObject.transform.localScale = new Vector3(1f, 1f, 1f);

            if (mesh != null)
            {
                MeshFilter filter = partObject.AddComponent<MeshFilter>();
                filter.mesh = mesh;

                MeshRenderer renderer = partObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = modelData.OpaqueMaterial;
            }

            // transparent
            mesh = part.CreateTransparentMesh();

            if (mesh != null)
            {
                if (part.DidNotProvideNormals)
                {
                    mesh.RecalculateNormals(60f);
                    //Debug.Log(" CALC NORMALS FOR PART: " + partIndex);
                }

                partObject = new GameObject("part_trans_" + partIndex);
                partObject.SetActive(true);

                part.TransparentObject = partObject;

                if (part.Parent == -1)
                {
                    partObject.transform.parent = root.transform;
                }
                else
                {
                    partObject.transform.parent = modelData.Parts[part.Parent].Pivot.transform;
                }
                partObject.transform.localPosition = part.Translation;
                partObject.transform.localScale = new Vector3(1f, 1f, 1f);

                MeshFilter filter = partObject.AddComponent<MeshFilter>();
                filter.mesh = mesh;

                MeshRenderer renderer = partObject.AddComponent<MeshRenderer>();

                renderer.sharedMaterial = modelData.TransparentMaterial;
            }

            if (part.OpaqueObject || part.TransparentObject)
            {
                // pivoting
                GameObject partPivot = new GameObject("pivot" + partIndex);

                if (part.OpaqueObject)
                {
                    partPivot.transform.position = part.OpaqueObject.transform.position;
                    partPivot.transform.parent = part.OpaqueObject.transform.parent;
                }
                else
                {
                    partPivot.transform.position = part.TransparentObject.transform.position;
                    partPivot.transform.parent = part.TransparentObject.transform.parent;
                }

                partPivot.transform.localEulerAngles = Vector3.zero;

                if (part.OpaqueObject)
                {
                    part.OpaqueObject.transform.parent = partPivot.transform;
                }

                if (part.TransparentObject)
                {
                    part.TransparentObject.transform.parent = partPivot.transform;
                }

                part.Pivot = partPivot;

                // translations provided?
                if (translatonList != null)
                {
                    partPivot.transform.position = translatonList[partIndex];
                }

                if (rotationList != null)
                {
                    partPivot.transform.localRotation = rotationList[partIndex];
                }

                if (scaleList != null)
                {
                    Vector3 scale = scaleList[partIndex];
                    // test scale
                    //partPivot.transform.localScale = new Vector3(0.0039f * scale.x, 0.0039f * scale.y, 0.0039f * scale.z);
                    partPivot.transform.localScale = scale;
                }
            }
        }

        parent.transform.eulerAngles = new Vector3(180f, 0f, 0f);
        parent.transform.localScale = new Vector3(-0.1f, 0.1f, 0.1f);
        return parent;
    }

    private Mesh CreateMesh(ModelPart part)
    {
        List<Vector3> vertices = part.Vertices;
        List<Vector3> normals = part.Normals;
        List<int> indices = part.Indices;
        List<Color> colors = part.Colors;
        List<Vector2> uvs = part.Uvs;

        Mesh mesh = new Mesh();

        if (indices.Count <= 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        }
        else
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);

        mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);

        mesh.name = "mesh";

        return mesh;
    }
}
