using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine;
using System.IO;
using UnityEngine.Windows;

using Color = UnityEngine.Color;
using ColorUtility = UnityEngine.ColorUtility;
using Input = UnityEngine.Input;
using Debug = UnityEngine.Debug;
using File = System.IO.File;

using System.Collections;
using System.Xml.Linq;
using static UnityEditor.Timeline.TimelinePlaybackControls;
using System.Diagnostics;
using UnityEngine.Analytics;

//this method handles click detection action and the stages involved in generating map data
public class MapGen : MonoBehaviour
{
    [SerializeField] public GameObject tileScanTarget;
    [SerializeField] public Texture2D tileMap;
    [SerializeField] private Texture2D biomeMap;
    [SerializeField] private Texture2D heightMap;
    [SerializeField] private Texture2D temperatureMap;
    [SerializeField] private Texture2D languageMap;
    [SerializeField] public Texture2D continentMap;
    [SerializeField] private Texture2D timezoneMap;

    [SerializeField] private bool newGenerate = true;
    private Color[] pixels;

    List<TileData> tilesDoren, tilesKolus, tilesKafrica, tilesVeiid, tilesFiresvar, tilesCaledon;

    // Start is called before the first frame update
    void Start()
    {
        if (newGenerate)
        {
            //create new map data using images
            Debug.Log("Generating New TileData");
            GenerateConfigs();
        }
        else
        {
            //load data from config files
            Debug.Log("Loading Existing TileData");
            tilesDoren = ImportTilesJson("Doren.json");
            tilesKolus = ImportTilesJson("Kolus.json");
            tilesKafrica = ImportTilesJson("Kafrica.json");
            tilesVeiid = ImportTilesJson("Veiid.json");
            tilesFiresvar = ImportTilesJson("Firesvar.json");
            tilesCaledon = ImportTilesJson("Caledon.json");
        }

    }
    void Update()
    {
        
    }
    
    void GenerateConfigs()
    {
        // If we're generating tile config files we need to create empties
        tilesDoren = new List<TileData>();
        tilesKolus = new List<TileData>();
        tilesKafrica = new List<TileData>();
        tilesVeiid = new List<TileData>();
        tilesFiresvar = new List<TileData>();
        tilesCaledon = new List<TileData>();

        // Generate list of map hexcodes
        pixels = tileMap.GetPixels();
        List<Color> tileColours = GetUniqueHexColors(pixels);
        // For each create a tile definition and chuck it into a doc
        StartCoroutine(delayBuild(tileColours));


    }

    IEnumerator delayBuild(List<Color> tileColours)
    {
        Color newColor;
        int tileProgress = 0;
        int tileCount = tileColours.Count;

        Debug.Log("Colours: " + tileCount);
        for (int i = 0; i < tileCount; i++)
        {
            tileProgress++;

            newColor = tileColours[i];
            if (tileColours.IndexOf(newColor) > 5) break; //temporary
            DefineTile(newColor);
            yield return new WaitForSeconds(0.01f);

            Debug.Log(tileProgress + " / " + tileCount);
        }


        
        Debug.Log("Writing Tiles to New Config Files");
        // write all to json
        WriteToJson(tilesDoren, "Doren.json");
        WriteToJson(tilesKolus, "Kolus.json");
        WriteToJson(tilesKafrica, "Kafrica.json");
        WriteToJson(tilesVeiid, "Veiid.json");
        WriteToJson(tilesFiresvar, "Firesvar.json");
        WriteToJson(tilesCaledon, "Caledon.json");

        yield return null;
    }


    public void WriteToJson(List<TileData> tilesGroup, string endFile)
    {
        string filePath = Application.dataPath + "/Maps/MapData/Tiles/" + endFile;
        FileStream fileStream = new FileStream(filePath, FileMode.Create);
        // Turn the arraylists into usable outputs
        string jsonOutput = JsonHelper.ToJson<TileData>(tilesGroup.ToArray(), true);

        // Create a new filestream and write the usable json to it
        using (StreamWriter writer = new StreamWriter(fileStream))
        {
            writer.Write(jsonOutput);
        }
    }

    List<TileData> ImportTilesJson(string sourceFile)
    {
        string filePath = Application.dataPath + "/Maps/MapData/Tiles/" + sourceFile;
        string content;

        if (File.Exists(filePath))
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                content = reader.ReadToEnd();
            }
        } 
        else
        {
            // If no read file create an empty and send that back
            return new List<TileData>();
        }

        if (string.IsNullOrEmpty(content) || content == "{}")
        {
            // If the file is blank or empty send back an empty
            return new List<TileData>();
        }

        // Parse any valid content to an arraylist and return it
        List<TileData> data = JsonHelper.FromJson<TileData>(content).ToList();
        return data;
    }

    public static List<Color> GetUniqueHexColors(Color[] pixels)
    {
        HashSet<Color> uniqueColorsHash = new HashSet<Color>();

        // Loop through each pixel of the texture
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            // Check if the color is not transparent
            if (color.a > 0f)
            {
                // Add the color to the set of unique colors
                uniqueColorsHash.Add(color);
            }
        }

        return uniqueColorsHash.ToList();
    }

    void DefineTile(Color targetColor)
    {
        // Count the number of pixels that match the target color
        int matchingPixels = 0;
        float totalX = 0f;
        float totalY = 0f;
        int width = tileMap.width;
        int height = tileMap.height;

        // Create a new tile object and set some values
        TileData newTile = new TileData{ HexCode = ColorUtility.ToHtmlStringRGB(targetColor) };

        // This is massively inefficent even still
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = y * width + x;
                Color pixelColor = pixels[pixelIndex];

                if (pixelColor.Equals(targetColor))
                {
                    totalX += x;
                    totalY += y;
                    matchingPixels++;
                }
            }
        }
        //get mean pixel position for tile center
        int meanX = Mathf.RoundToInt(totalX / matchingPixels);
        int meanY = Mathf.RoundToInt(totalY / matchingPixels);

        newTile.pxArea = matchingPixels;
        newTile.Position = new Vector2(meanX, meanY);


        Debug.Log("#" + newTile.HexCode + "\nArea: " + matchingPixels + "px");
        //public float Area;
        //public float Temperature;
        //public string Biome;
        //public string Language;
        //public string Altitude;


        // get Continent of the mean position
        Color continentColor = continentMap.GetPixel(meanX, meanY);


        int colorValue =
            ((int)(continentColor.r * 255) << 16) |
            ((int)(continentColor.g * 255) << 8) |
            (int)(continentColor.b * 255); //this is sus but it as least keeps the door open for smaller divisions

        getTileList(colorValue).Add(newTile);
    }
    public List<TileData> getTileList(int colorValue)
    {
        //method to feed the integer of a continent colour from the map and return its tile list
        switch (colorValue)
        {
            case 16711680: // Doren
                return tilesDoren;
            case 255: // Kolus
                return tilesKolus;
            case 65280: // Kafrica
                return tilesKafrica;
            case 16711935: // Veiid
                return tilesVeiid;
            case 16777215: // Firesvar
                return tilesFiresvar;
            case 65535: // Caledon
                return tilesCaledon;
            default:
                return new List<TileData>();
        }
    }
}
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        JsonWrapper<T> wrapper = JsonUtility.FromJson<JsonWrapper<T>>(json);
        return wrapper.Items;
    }

    public static string ToJson<T>(T[] array, bool prettyPrint)
    {
        JsonWrapper<T> wrapper = new JsonWrapper<T>();
        wrapper.Items = array;
        return JsonUtility.ToJson(wrapper, prettyPrint);
    }

    [Serializable]
    private class JsonWrapper<T>
    {
        public T[] Items;
    }
}
