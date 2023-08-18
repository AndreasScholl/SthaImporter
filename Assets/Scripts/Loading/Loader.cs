using ModelBuilder;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Loader
{
    const int _vdpTable = 0x060ED000;
    private static string _textureFolder = "";

    public static GameObject ImportMap(string mapFile)
    {
        byte[] mdxData = File.ReadAllBytes(mapFile);
        int polyData = ByteArray.GetInt32(mdxData, 0x20);
        int polySize = mdxData.Length - polyData;

        Debug.Log("data: " + polyData.ToString("X8"));
        Debug.Log("size: " + polySize.ToString("X8"));

        int fileMemoryOffset = 0x00200000;
        int memoryOffset = 0x00200000;

        MemoryManager memory = Importer.Instance.GetMemoryManager(fileMemoryOffset);
        //int size = memory.LoadFile(file, fileMemoryOffset);
        int mapDataLocation = 0x234000;
        memory.LoadArray(mdxData, polyData, polySize, mapDataLocation);

        int objMemory = mapDataLocation + 0x08;
        int objCount = memory.GetInt16(objMemory);
        //objCount = 500;

        objMemory += 4;
        List<int> xpData = new List<int>();
        List<Vector3> positions = new List<Vector3>();
        List<Quaternion> angles = new List<Quaternion>();
        List<Vector3> scales = new List<Vector3>();
        for (int count = 0; count < objCount; count++)
        {
            Debug.Log("Obj: " + count);

            int modelPointer0 = memory.GetInt32(objMemory);
            int data = (modelPointer0 >> 24) & 0xff;        // some info, maybe a flag
            Debug.Log("data: " + data.ToString("X2"));
            modelPointer0 &= 0x00ffffff;
            xpData.Add(modelPointer0);

            //float x = memory.GetFloat16(objMemory + 0x20);
            //float y = memory.GetFloat16(objMemory + 0x22);
            //float z = memory.GetFloat16(objMemory + 0x24);
            short x = (short)memory.GetInt16(objMemory + 0x20);
            short y = (short)memory.GetInt16(objMemory + 0x22);
            short z = (short)memory.GetInt16(objMemory + 0x24);
            Vector3 position = new Vector3(x, y, z);
            Debug.Log("pos: " + position);
            positions.Add(position);

            float angleX = (memory.GetFloat16(objMemory + 0x26) * 360f) / 256f;
            float angleY = (memory.GetFloat16(objMemory + 0x28) * 360f) / 256f;
            float angleZ = (memory.GetFloat16(objMemory + 0x2a) * 360f) / 256f;
            Quaternion rotation = Quaternion.identity;
            rotation *= Quaternion.Euler(0, 0, angleZ);
            rotation *= Quaternion.Euler(0, angleY, 0);
            rotation *= Quaternion.Euler(angleX, 0, 0);
            Vector3 angle = new Vector3(angleX, angleY, angleZ);
            Debug.Log("ang: " + angle);
            angles.Add(rotation);

            float scaleX = memory.GetFloat(objMemory + 0x2c);
            float scaleY = memory.GetFloat(objMemory + 0x30);
            float scaleZ = memory.GetFloat(objMemory + 0x34);
            Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);
            Debug.Log("scale: " + scaleX + " | " +
                                  scaleY + " | " +
                                  scaleZ + " | ");
            scales.Add(scale);

            objMemory += 0x38;
        }

        //List<int> xpData = SearchForXPDataNights(memoryOffset, 0x00000, 0xfff00, true);

        bool pointHeaderCheck = false;
        float gridOffset = 256;

        ModelData modelData = Importer.Instance.ImportModelsFromMemory(xpData, pointHeaderCheck, memoryOffset, 0, gridOffset, 512, 512, true);

        GameObject obj = Importer.Instance.CreateObject(modelData, "map", false, false, null, positions, angles, scales);
        obj.transform.position = new Vector3(0f, 0f, 0f);
        obj.transform.eulerAngles = new Vector3(180f, 0f, 0f);
        obj.transform.localScale = new Vector3(-0.1f, 0.1f, 0.1f);

        //PostprocessModel(obj, true, false, 90f);

        return obj;
    }

    public static int ImportTextureList(byte[] data, int textureIndex)
    {
        //byte[] mdxData = File.ReadAllBytes(file);
        int fileMemoryOffset = 0x06000000;
        int textureTable = 0x0607c000;
        MemoryManager memory = Importer.Instance.GetMemoryManager(fileMemoryOffset);
        memory.LoadArray(data, 0, data.Length, textureTable);
        //memory.LoadFile(file, fileMemoryOffset);

        int numTextures = memory.GetInt32(textureTable);
        Debug.Log("textures: " + numTextures.ToString("X8"));

        if (numTextures == 0)
        {
            return textureIndex;
        }

        int textureInfo = textureTable + 8;
        int textureData = textureInfo + (numTextures * 8);

        for (int index = 0; index < numTextures; index++)
        {
            int width = memory.GetInt16(textureInfo);
            int height = memory.GetInt16(textureInfo + 2);
            int offset = memory.GetInt32(textureInfo + 4);

            Debug.Log("width: " + width.ToString("X4"));
            Debug.Log("height: " + height.ToString("X4"));
            Debug.Log("offset: " + offset.ToString("X8"));

            textureInfo += 8;

            Texture2D texture = new Texture2D(width, height);
            texture.filterMode = FilterMode.Point;

            Importer.Instance.Textures.Add(texture);
            textureIndex++;

            int textureMemory = textureData + offset;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int colorValue = memory.GetInt16(textureMemory);
                    Color colorRgb = ColorConversion.ConvertColor(colorValue);
                    if (colorValue == 0 || colorValue == 0x7fff)
                    {
                        colorRgb = Color.black;
                        colorRgb.a = 0f;
                    }
                    texture.SetPixel(x, y, colorRgb);

                    textureMemory += 2;
                }
            }
         
            texture.Apply();

            byte[] bytes = Importer.Instance.FlipYAndRemoveAlpha(texture).EncodeToPNG();
            //byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes("textures/tex_" + textureIndex + ".png", bytes);
        }

        return textureIndex;
    }
}
