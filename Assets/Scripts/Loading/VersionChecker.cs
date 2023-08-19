using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class VersionChecker
{
    public static string ImagePath = "";

    public static bool _usVersion = false;
    public static bool _euVersion = false;
    public static bool _jpVersion = false;
    public static bool _prototypeVersion = false;
    public static bool _finalVersion = false;
    public static bool _christmasVersion = false;

    public static string GetFilePath(string fileName)
    {
        return ImagePath + "/" + fileName;
    }

    public static bool CheckImagePath()
    {
        string imageFolder = "Image";

        bool pathValid = IsPathValid(imageFolder);

        if (pathValid == true)
        {
            ImagePath = imageFolder;
            return true;
        }

        DriveInfo[] allDrives = DriveInfo.GetDrives();

        foreach (DriveInfo drive in allDrives)
        {
            pathValid = IsPathValid(drive.Name);

            if (pathValid == true)
            {
                ImagePath = drive.Name;
                return true;
            }
        }

        Debug.LogError("No valid image path found!");
        return false;
    }

    private static bool IsPathValid(string imageFolder)
    {
        string mainFile = "1ST.BIN";
        int mainFileRequiredSize = 112952;

        string mainFilePath = imageFolder + "/" + mainFile;

        if (File.Exists(mainFilePath))
        {
            FileInfo fileInfo = new FileInfo(mainFilePath);

            if (fileInfo.Length == mainFileRequiredSize)
            {
                Debug.Log("Valid image path: " + imageFolder);

                return true;
            }
        }

        return false;
    }
}
