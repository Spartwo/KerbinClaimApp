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
using UnityEditor;
using System.Drawing;
using UnityEngine.UIElements;

//this method handles click detection action and the stages involved in generating map data
public class MapGen : MonoBehaviour
{
    #region Accessibles
    [SerializeField] public GameObject tileScanTarget;
    [SerializeField] public Texture2D tileMap;
    [SerializeField] private Texture2D tileAreaMap;
    [SerializeField] public Texture2D provinceMap;
    [SerializeField] private Texture2D languageMap;
    [SerializeField] public Texture2D biomeMap;
    [SerializeField] private Texture2D heightMap;
    [SerializeField] private Texture2D populationMap;
    [SerializeField] public Texture2D continentMap;

    // When true will produce new definitions from the map, takes ages
    [SerializeField] private bool generateMap = true;
    // Defined map scaling
    private float pixelWidthKilometres;
    public float bodyCircumferanceKilometres = 3769.911f;
    private int width;
    private int height;
    private int mapSize; //Pixel area
    // Other Scalers
    [SerializeField] private int searchIncriment = 100;
    [SerializeField] private int populationScaler = 1;
    #endregion

    private List<Color> tileColoursRemaining = new List<Color>();

    // The actual data
    public List<ContinentData> continents = new List<ContinentData>();
    // Localisation
    private List<MapLocalisation> continentNames = new List<MapLocalisation>();
    private List<MapLocalisation> provinceNames = new List<MapLocalisation>();

    // Start is called before the first frame update
    void Start()
    {
        width = tileMap.width;
        height = tileMap.height;

        // Import Localisation in both cases
        continentNames = ImportNamesJson(Application.dataPath + "/Maps/Localisation/Continents.json");
        provinceNames = ImportNamesJson(Application.dataPath + "/Maps/Localisation/Provinces.json");

        if (generateMap)
        {
            // Create new map data using images
            Debug.Log("Generating Map Data");
            GenerateConfigs();
        }
        else
        {
            // Load dmap ata from config files
            Debug.Log("Loading Map Data");
            ImportContinentsJson(Application.dataPath + "/Maps/MapData/Tiles/");
        }

    }

    void GenerateConfigs()
    {
        // Store map scale
        pixelWidthKilometres = bodyCircumferanceKilometres / tileMap.width;
        // For each create a tile definition and chuck it into a doc
        StartCoroutine(delayBuild());
    }

    IEnumerator delayBuild()
    {
        // Generate Continents
        List<Color> continentPixels = SerialiseMap(continentMap.GetPixels());
        List<Color> continentOpaque = RemoveMapAlpha(continentPixels);
        List<Color> continentColours = UniqueMapColours(continentPixels);

        // Generate search scaling
        mapSize = continentOpaque.Count;

        // Define Each Continent
        int continentCount = continentColours.Count;
        Debug.Log("Continents: " + (continentCount - 1));
        for (int i = 0; i < continentCount; i++)
        {
            DefineContinent(continentColours[i]);
        }
        // Give a little buffer
        yield return new WaitForSeconds(0.01f);


        // Generate Provinces
        List<Color> provincePixels = RemoveMapAlpha(SerialiseMap(provinceMap.GetPixels()));
        List<Color> provinceColours = UniqueMapColours(provincePixels);
        // Define Each Province
        int provinceCount = provinceColours.Count;
        Debug.Log("Provinces: " + provinceCount);
        int provinceProgress = 0;
        for (int i = 0; i < provinceCount; i++)
        {
            provinceProgress++;
            Debug.Log(provinceProgress + " / " + provinceCount);

            DefineProvince(provinceColours[i], provincePixels, continentOpaque);
            // Give a little buffer
            yield return new WaitForSeconds(0.01f);
        }


        // Generate Tiles
        // Equal Area for size getting
        tileColoursRemaining = RemoveMapAlpha(SerialiseMap(tileAreaMap.GetPixels()));
        tileColoursRemaining.Reverse();
        // Equirectangular for global position
        List<Color> tilePixels = SerialiseMap(tileMap.GetPixels());
        List<Color> tileColours = UniqueMapColours(RemoveMapAlpha(tilePixels));
        //Define Each Tile
        int tileCount = tileColours.Count;
        int tileProgress = 0;
        Debug.Log("Tiles: " + tileCount);
        for (int i = 0; i < tileCount; i++)
        {
            tileProgress++;
            Debug.Log(tileProgress + " / " + tileCount);
            // ***
            if (i > 10) break; //temporary for only short test generations
            // ***
            DefineTile(tileColours[i], tilePixels);

            // Give a little buffer
            yield return new WaitForSeconds(0.01f);
        }


        // write all to json
        WriteToJson(Application.dataPath + "/Maps/MapData/Tiles/");
        SaveNamesJson();

        yield return null;
    }

    #region Definitions
    void DefineContinent(Color targetColor)
    {
        string newHex = ColorUtility.ToHtmlStringRGB(targetColor);
        // Create the continent object
        ContinentData newContinent = new ContinentData
        {
            HexCode = newHex,
            Provinces = new List<ProvinceData>(),
        };
        // Add to list
        continents.Add(newContinent);

        // Check for localisation
        string returnedName = RetrieveName(true, newHex);
        // If no value found add it in, if found no need
        if (returnedName == "Undefined") 
        { 
            MapLocalisation localisation = new MapLocalisation { HexCode = newHex, Name = newHex };
            continentNames.Add(localisation);
        }
    }

    void DefineProvince(Color targetColor, List<Color> provinceColours, List<Color> continentColours)
    {
        string parentContinent = "000000";
        ContinentData newParent = new ContinentData();

        bool found = false;
        
        // Get the index position of the first matching pixel
        for (int i = 0; i < mapSize; i += searchIncriment)
        {
            if (found) break; // It should be breaking on its own from the lowest loop right?
            if (provinceColours[i] == targetColor)
            {
                string contPixel = ColorUtility.ToHtmlStringRGB(continentColours[i]);
                // Find the continent using the first matching pixel
                foreach (ContinentData c in continents)
                {
                    // If the colours are what's being looked for, get this object and exit the loop
                    if (c.HexCode == contPixel)
                    {
                        newParent = c;
                        parentContinent = c.HexCode;
                        found = true;
                        break;
                    }
                }
            }
        }
        // If default value continent wasn't found
        if (parentContinent.Equals("000000")) Debug.Log("Province Continent not found");


        string newHex = ColorUtility.ToHtmlStringRGB(targetColor);
        // Create the province object
        ProvinceData newProvince = new ProvinceData
        {
            HexCode = newHex,
            ContinentParent = parentContinent,
            Tiles = new List<TileData>(),
        };

        // Chuck Province into right continent
        newParent.Provinces.Add(newProvince);

        // Check for localisation
        string returnedName = RetrieveName(false, newHex);
        // If no value found add it in with a continent identifier to help find it, if found no need
        if (returnedName == "Undefined")
        {
            MapLocalisation localisation = new MapLocalisation { HexCode = newHex, Name = newHex + "-" + parentContinent };
            provinceNames.Add(localisation);
        }
    }

    void DefineTile(Color targetColor, List<Color> tilePixels)
    {
        // Count the number of pixels that match the target color
        int matchingPixels = 0;
        int matchingArea = 0;
        float totalX = 0f;
        float totalY = 0f;
        // First for retrieving province, centre of tile can sometimes be a lake
        bool firstFound = false;
        int firstX = 0;
        int firstY = 0;


        // Get area using the true area map
        for (int i = tileColoursRemaining.Count - 1; i >= 0; i--)
        {
            if (targetColor == tileColoursRemaining[i])
            {
                matchingArea++;
                // Remove the matching element from the array to cut down subsequent searches
                tileColoursRemaining.RemoveAt(i);
            }
        }
        // Convert the number to area
        float newArea = matchingArea * (pixelWidthKilometres * pixelWidthKilometres);

        // This is massively inefficent even still but the position is good to know
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = y * width + x;

                if (tilePixels[pixelIndex].Equals(targetColor))
                {
                    totalX += x;
                    totalY += y;
                    matchingPixels++;

                    if(!firstFound) 
                    {
                        firstX = x;
                        firstY = y;
                        firstFound = true;
                    }
                }
            }
        }
        // Get mean pixel position for tile center
        int meanX = Mathf.RoundToInt(totalX / matchingPixels);
        int meanY = Mathf.RoundToInt(totalY / matchingPixels);
        // TODO: Convert pixel position to lat and long
        float latitude = 0;
        float longitude = 0;

        // Get Culture by finding the undertile value in a comparitive object
        Color cultureValue = languageMap.GetPixel(meanX, meanY);
        CultureDef newCulture = new CultureDef();
        newCulture = FindCulture(ColorUtility.ToHtmlStringRGB(cultureValue));
        // Get altitude by converting the heightmap brightness
        int colorValue = (int)(heightMap.GetPixel(meanX, meanY).r * 255);
        float newAlt = colorValue * 50f;
        // Get Biome by comparing the biomemap with a dictionary
        Color biomeValue = biomeMap.GetPixel(meanX, meanY);
        string newTerrain = biomeCodeMappings[ColorUtility.ToHtmlStringRGB(biomeValue)];
        // Get Population of the tile by scaling the true area against the heatmap
        //Color heatValue = populationMap.GetPixel(meanX, meanY);
        int newPopulation = (int)(30 * populationScaler * newArea); //30 is a standin heatmap value for persons per km
        // Get Province of the tile
        ProvinceData newParent = getProvinceParent(firstX, firstY);

        // Claim Value is an aggregate of local values
        int claimValue = 5;

        // Create a new tile object and set values
        TileData newTile = new TileData
        {
            HexCode = ColorUtility.ToHtmlStringRGB(targetColor),
            Culture = newCulture,
            Coordinates = new Vector2(latitude, longitude),
            Position = new Vector2(meanX, meanY),
            Altitude = newAlt,
            Terrain = newTerrain,
            Area = newArea,
            Population = newPopulation,
            ProvinceParent = newParent.HexCode,
            ContinentParent = newParent.ContinentParent,
            // Claim Value is an aggregate of local values
            ClaimValue = claimValue,
        };

        // Chuck tile into right province
        newParent.Tiles.Add(newTile);
        // Add tile area to province area
        newParent.Area += newArea;
        newParent.Population += newPopulation;
    }
    #endregion

    #region Find Objects
    public ProvinceData getProvinceParent(int x, int y)
    {
        string continentColour = ColorUtility.ToHtmlStringRGB(continentMap.GetPixel(x, y));
        string provinceColour = ColorUtility.ToHtmlStringRGB(provinceMap.GetPixel(x, y));

        Debug.Log("Searching\nCon: " + continentColour + "\tProv:" + provinceColour);

        //method searches an appropriate subdatabase for the right province
        // Find right continent
        foreach (ContinentData c in continents)
        {
            if (c.HexCode != continentColour) continue;
            // Find right province inside continent
            foreach (ProvinceData p in c.Provinces)
            {
                if (p.HexCode != provinceColour) continue;
                return p;
            }
        }

        Debug.Log("Province not Found");
        return null;
    }
    #endregion

    #region Localisation
    public void SaveNamesJson()
    {
        Debug.Log("Writing Localisation to Files");

        // Write Continents File
        string filePath = Application.dataPath + "/Maps/Localisation/Continents.json";
        FileStream fileStream = new FileStream(filePath, FileMode.Create);
        string jsonOutput = JsonHelper.ToJson<MapLocalisation>(continentNames.ToArray(), true);

        // Create a new filestream and write the usable json to it
        using (StreamWriter writer = new StreamWriter(fileStream))
        {
            writer.Write(jsonOutput);
        }

        // Write Provinces File
        string filePath2 = Application.dataPath + "/Maps/Localisation/Provinces.json";
        FileStream nordStream2 = new FileStream(filePath2, FileMode.Create);
        string jsonOutput2 = JsonHelper.ToJson<MapLocalisation>(provinceNames.ToArray(), true);

        // Create a new filestream and write the usable json to it
        using (StreamWriter writer = new StreamWriter(nordStream2))
        {
            writer.Write(jsonOutput2);
        }
    }

    List<MapLocalisation> ImportNamesJson(string filePath)
    {
        string content;
        if (File.Exists(filePath))
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                content = reader.ReadToEnd();
            }

            // Process the file contents...
            if (!string.IsNullOrEmpty(content) && content != "{}")
            {
                return JsonHelper.FromJson<MapLocalisation>(content).ToList();
            } 
        }
        // If file doesn't exist or is invalid return an empty list
        return new List<MapLocalisation>();
    }

    public string RetrieveName(bool Continents, string hexCode)
    {
        int searchCount;
        if (Continents)
        {
            searchCount = continentNames.Count;
            for (int i = 0; i < searchCount; i++)
            {
                if (continentNames[i].HexCode == hexCode) return continentNames[i].Name;
            }
        }
        else
        {
            searchCount = provinceNames.Count;
            for (int i = 0; i < searchCount; i++)
            {
                if (provinceNames[i].HexCode == hexCode) return provinceNames[i].Name;
            }
        }
        return "Undefined";
    }
    #endregion

    void ImportContinentsJson(string filePath)
    {
        string[] files = System.IO.Directory.GetFiles(filePath, $"*.json");
        string content;
        continents = new List<ContinentData>();


        foreach (string file in files)
        {
            if (File.Exists(file))
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    content = reader.ReadToEnd();
                }
            }
            else
            {
                // If no read file create an empty and send that back
                content = "{}";
            }

            // Process the file contents...
            if (!string.IsNullOrEmpty(content) && content != "{}")
            {
                continents.AddRange(JsonHelper.FromJson<ContinentData>(content).ToList());
            }
        }
    }
    public void WriteToJson(string filePath)
    {
        Debug.Log("Writing Data to New Config Files");
        foreach (ContinentData c in continents)
        {
            string newPath = filePath + c.HexCode + ".json";
            FileStream fileStream = new FileStream(newPath, FileMode.Create);
            // Turn the arraylists into usable outputs

            ContinentData[] toSend = new ContinentData[1];
            toSend[0] = c;

            string jsonOutput = JsonHelper.ToJson<ContinentData>(toSend, true);
            // Create a new filestream and write the usable json to it
            using (StreamWriter writer = new StreamWriter(fileStream))
            {
                writer.Write(jsonOutput);
            }
        }
    }

    #region Colours
    List<Color> SerialiseMap(Color[] pixels)
    {
        List<Color> colors = new List<Color>();
        int pixelCount = pixels.Length;
        // Loop through each pixel of the texture
        for (int i = 0; i < pixelCount; i++)
        {
            Color color = pixels[i];
            // Add the color to the set of colors
            colors.Add(color);
        }

        return colors;
    }

    List<Color> RemoveMapAlpha(List<Color> colors) 
    {
        List<Color> newList = new List<Color>();
        int colourCount = colors.Count;
        // Check every pixel
        for (int i = 0; i < colourCount; i++)
        {
            Color color = colors[i];
            // Check if the color is not transparent
            if (color.a > 0f) newList.Add(color);
        }
        // Return new list that's only opaque
        return newList;
    }

    List<Color> UniqueMapColours(List<Color> colors)
    {
        HashSet<Color> uniqueColoursHash = new HashSet<Color>(colors);
        return new List<Color>(uniqueColoursHash);
    }
    #endregion

    private CultureDef FindCulture(string hexCode)
    {
        //TODO: Create cultural data structures
        return new CultureDef 
        {
            HexCode = hexCode,
            Dialect = "Kr*man",
            Language = "Kr*man",
            Group = "Kr*man",
            Family = "Kr*man",
        };
    }
    private Dictionary<string, string> biomeCodeMappings = new Dictionary<string, string>()
    {
        { "3762AB", "Ocean" },
        { "4A85E2", "Shallows" },
        { "5498FF", "Freshwater" },
        { "A7A7A7", "Mountain" },
        { "C78FDF", "Tundra" },
        { "D8D8D8", "Ice Cap" },
        { "E4FDFF", "Ice Sheet" },
        { "EABF6F", "Desert" },
        { "974F23", "Badlands" },
        { "83BC2E", "Grasslands" },
        { "5D852A", "Highlands" },
        { "FAF2B7", "Shores" },
    };
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        JsonWrapper<T> wrapper = JsonUtility.FromJson<JsonWrapper<T>>(json);
        return wrapper.Content;
    }

    public static string ToJson<T>(T[] array, bool prettyPrint)
    {
        JsonWrapper<T> wrapper = new JsonWrapper<T>();
        wrapper.Content = array;
        return JsonUtility.ToJson(wrapper, prettyPrint);
    }

    [Serializable]
    private class JsonWrapper<T>
    {
        public T[] Content;
    }
}
