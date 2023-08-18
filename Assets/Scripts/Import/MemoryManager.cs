﻿using System.IO;
using UnityEngine;

public class MemoryManager
{
    public byte[] Data;
    public const int DataSize = 0x0100000;
    public byte[] BackupData;

    public int Base;

    public MemoryManager(int baseAddress)
    {
        Base = baseAddress;

        Data = new byte[DataSize];
        BackupData = new byte[DataSize];
    }

    public int LoadFile(string filePath, int address/*, bool compressed = true*/)
    {
        byte[] data;
        
        //if (compressed == true)
        //{
        //    //data = Prs.Decompress(filePath);
        //}
        //else
        //{
        data = File.ReadAllBytes(filePath);
        //}

        if (data == null)
        {
            Debug.Log(" MEMORYMANAGER: FILE NOT FOUND " + filePath);
            return 0;
        }

        data.CopyTo(Data, address - Base);

        return data.Length;
    }

    public bool IsAddressValid(int address)
    {
        if (address < Base)
        {
            return false;
        }
        else if (address >= Base + DataSize)
        {
            return false;
        }

        return true;
    }

    public int GetInt32(int address)
    {
        int value;

        address -= Base;
        value = (Data[address] << 24) + (Data[address + 1] << 16) + (Data[address + 2] << 8) + Data[address + 3];

        return value;
    }

    public void SetInt32(int address, int value)
    {
        address -= Base;
        Data[address] = (byte)(value >> 24);
        Data[address + 1] = (byte)(value >> 16);
        Data[address + 2] = (byte)(value >> 8);
        Data[address + 3] = (byte)value;
    }

    public int GetInt16(int address)
    {
        int value;

        address -= Base;
        value = (Data[address] << 8) + Data[address + 1];

        return value;
    }

    public byte GetByte(int address)
    {
        return Data[address - Base];
    }

    public void SetByte(int address, byte value)
    {
        Data[address - Base] = value;
    }

    public float GetFloat(int address)
    {
        int intValue = GetInt32(address);

        float value = 0f;

        short integral = (short)(intValue >> 16);
        int fraction = intValue & 0xffff;

        value = integral + ((float)fraction / (float)0x10000);

        return value;
    }

    public float GetFloat16(int address)
    {
        int intValue = GetInt16(address);

        sbyte integral = (sbyte)(intValue >> 8);
        int fraction = intValue & 0xff;

        float value = integral + ((float)fraction / (float)0x100);

        return value;
    }

    public float GetAngle16(int address)
    {
        int intValue = GetInt16(address);

        byte integral = (byte)(intValue >> 8);
        int fraction = intValue & 0xff;

        float value = integral + ((float)fraction / (float)0x100);

        return value;
    }

    public int GetPolygonIndex(int address, out bool negate)
    {
        ushort value = (ushort)GetInt16(address);

        if ((value & 0x8000) != 0)
        {
            value ^= 0xffff;
            negate = true;
        }
        else
        {
            negate = false;
        }

        int index = value >> 3;

        return index;
    }

    public int GetPolygonIndex8(int address, int numPoints, out bool negate)
    {
        byte value = (byte)Data[address - Base];

        if ((value & 0x80) != 0)
        {
            value ^= 0xff;
            negate = true;
        }
        else
        {
            negate = false;
        }

        int index = value;

        if (index >= numPoints)
        {
            index -= numPoints;
        }

        return index;
    }

    public Vector3 GetTranslation(int address)
    {
        float transX, transY, transZ;
        transX = GetFloat(address);
        transY = GetFloat(address + 4);
        transZ = GetFloat(address + 8);

        return new Vector3(transX, transY, transZ);
    }

    public void Backup()
    {
        Data.CopyTo(BackupData, 0);
    }

    public void Restore()
    {
        BackupData.CopyTo(Data, 0);
    }

    public void LoadArray(byte[] sourceData, int offset, int length, int destination)
    {
        int destOffset = destination - Base;

        for (int index = 0; index < length; index++)
        {
            Data[destOffset + index] = sourceData[offset + index];
        }
    }
}

