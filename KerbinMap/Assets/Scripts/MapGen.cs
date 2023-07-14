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
using static UnityEngine.GraphicsBuffer;

//this method handles click detection action and the stages involved in generating map data
public class MapGen : MonoBehaviour
{
    #region Accessibles
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
    [SerializeField] private bool refreshData = true;
    // Defined map scaling
    private float pixelWidthKilometres;
    public float bodyCircumferanceKilometres = 3769.911f;
    private int width;
    private int height;
    private int mapArea, mapLandArea; //Pixel area
    // XY of the map center
    private float centerX;
    private float centerY;
    // Other Scalers
    [SerializeField] private Vector2 biggestTile = new Vector2(750,400);
    [SerializeField] private int searchIncriment = 100;
    [SerializeField] private int populationScaler = 1;
    #endregion

    private List<Color> tileColoursRemaining = new List<Color>();

    // The actual data
    public List<ContinentData> continents = new List<ContinentData>();
    // Localisation
    public List<MapLocalisation> continentNames = new List<MapLocalisation>();
    public List<MapLocalisation> provinceNames = new List<MapLocalisation>();
    public List<CultureDef> culturesList = new List<CultureDef>();

    // Start is called before the first frame update
    void Start()
    {
        width = tileMap.width;
        height = tileMap.height;
        // Store map scale
        pixelWidthKilometres = bodyCircumferanceKilometres / tileMap.width;
        // Store central position
        centerX = width / 2f;
        centerY = height / 2f;

        // Import Localisation in both cases
        continentNames = ImportNamesJson(Application.dataPath + "/Maps/Localisation/Continents.json");
        provinceNames = ImportNamesJson(Application.dataPath + "/Maps/Localisation/Provinces.json");
        culturesList = ImportCultureJson(Application.dataPath + "/Maps/Localisation/Cultures.json");

        if (generateMap)
        {
            // Create new map data using images
            Debug.Log("Generating Map Data");
            StartCoroutine(delayBuild());

        }
        else
        {
            // Load map data from config files
            Debug.Log("Loading Map Data");
            ImportContinentsJson(Application.dataPath + "/Maps/MapData/Tiles/");
        }

        // This cannot be enabled simultaneous with generateMap
        if (refreshData)
        {
            Debug.Log("Updating Map Data");
            StartCoroutine(UpdateTileData());

            // write all to json
            WriteToJson(Application.dataPath + "/Maps/MapData/Tiles/");
        }
    }

    IEnumerator delayBuild()
    {
        // Generate Continents
        List<Color> continentPixels = SerialiseMap(continentMap.GetPixels());
        List<Color> continentOpaque = RemoveMapAlpha(continentPixels);
        List<Color> continentColours = UniqueMapColours(continentPixels);

        // Generate search scaling
        mapLandArea = continentOpaque.Count;
        mapArea = continentPixels.Count; // width * height might be more efficient

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
        List<Color> provincePixels = SerialiseMap(provinceMap.GetPixels());
        List<Color> provinceColours = RemoveMapAlpha(UniqueMapColours(provincePixels));
        // Define Each Province
        int provinceCount = provinceColours.Count;
        Debug.Log("Provinces: " + provinceCount);
        int provinceProgress = 0;
        for (int i = 0; i < provinceCount; i++)
        {
            provinceProgress++;
            Debug.Log(provinceProgress + " / " + provinceCount);

            DefineProvince(provinceColours[i], provincePixels);
            // Give a little buffer
            yield return new WaitForSeconds(0.01f);
        }


        // Generate Tiles
        // Equal Area for size getting
        tileColoursRemaining = RemoveMapAlpha(SerialiseMap(tileAreaMap.GetPixels()));

        // Give a little buffer
        yield return new WaitForSeconds(0.01f);

        // Equirectangular for global position
        List<Color> tilePixels = SerialiseMap(tileMap.GetPixels());
        List<Color> tileColours = RemoveMapAlpha(UniqueMapColours(tilePixels));
        //Define Each Tile
        int tileCount = tileColours.Count;

        // Give a little buffer
        yield return new WaitForSeconds(0.01f);

        Debug.Log("Tiles: " + tileCount);
        int tileProgress = 0;
        for (int i = tileCount - 1; i >= 0; i--)
        {
            tileProgress++;
            Debug.Log(tileProgress + " / " + tileCount);
            DefineTile(tileColours[i], tilePixels);

            // Give a little buffer
            yield return new WaitForSeconds(0.01f);
        }

        // Give a little buffer
        yield return new WaitForSeconds(0.1f);
        //Adjust position of edge tiles
        List<Color> edgeColours = GetEdgeTiles(tilePixels);
        int edgeCount = edgeColours.Count;

        Debug.Log("Edge Tiles: " + edgeCount);
        int edgeProgress = 0;
        for (int i = 0; i < edgeCount; i++)
        {
            edgeProgress++;
            Debug.Log(edgeProgress + " / " + tileCount);
            OffsetMean(BruteFindTile(edgeColours[i]));

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

    }

    void DefineProvince(Color targetColor, List<Color> provincePixels)
    {
        string parentContinent = "000000";
        ContinentData newParent = new ContinentData();


        // Get the index position of the first matching pixel
        Vector2 firstPosition = pixelSweep(provincePixels, targetColor, mapLandArea, searchIncriment, 0);
        if (firstPosition == Vector2.zero) firstPosition = pixelSweep(provincePixels, targetColor, mapArea, searchIncriment / 2, 0.25f);
        if (firstPosition == Vector2.zero) firstPosition = pixelSweep(provincePixels, targetColor, mapArea, searchIncriment / 10, 0.5f);
        if (firstPosition == Vector2.zero) firstPosition = pixelSweep(provincePixels, targetColor, mapArea, 1, 0);

        // Get the continent at this position
        Color continentValue = continentMap.GetPixel((int)firstPosition.x, (int)firstPosition.y);
        string contPixel = ColorUtility.ToHtmlStringRGB(continentValue);

        // Find the continent using the first matching pixel
        foreach (ContinentData c in continents)
        {
            // If the colours are what's being looked for, get this object and exit the loop
            if (c.HexCode == contPixel)
            {
                newParent = c;
                parentContinent = c.HexCode;
                break;
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

    }

    void DefineTile(Color targetColor, List<Color> tilePixels)
    {
        // Count the number of pixels that match the target color
        int matchingArea = 0;
        int matchingPixels = 0;
        float totalX = 0f;
        float totalY = 0f;
        // First for retrieving province, centre of tile can sometimes be a lake

        // Get area using the true area map
        for (int i = tileColoursRemaining.Count - 1; i > 0; i--)
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
        Debug.Log("Area: " + matchingArea + "px / "+ newArea + "km^2");

        // Calculate the mean position of the tile
        // Find a matching pixel somewhere, try increasing levels of detail 
        Vector2 firstPosition = pixelSweep(tilePixels, targetColor, mapArea, searchIncriment, 0);
        if (firstPosition == Vector2.zero) firstPosition = pixelSweep(tilePixels, targetColor, mapArea, searchIncriment/2, 0.25f);
        if (firstPosition == Vector2.zero) firstPosition = pixelSweep(tilePixels, targetColor, mapArea, searchIncriment/10, 0.5f);
        if (firstPosition == Vector2.zero) firstPosition = pixelSweep(tilePixels, targetColor, mapArea, 1, 0);

        // This is massively inefficent even still but the position is good to know
        /*for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int pixelIndex = y * width + x;

                if (tilePixels[pixelIndex] == targetColor)
                {
                    totalX += x;
                    totalY += y;
                    matchingPixels++;
                    break;
                }
            }
        }*/

        // Adjust the search area around the first position
        int startX = Mathf.RoundToInt(firstPosition.x) - (int)biggestTile.x;
        int endX = startX + ((int)biggestTile.x * 2);
        int startY = Mathf.Max(Mathf.RoundToInt(firstPosition.y) - (int)biggestTile.y, 0);
        int endY = Mathf.Min(startY + ((int)biggestTile.y*2), height);

        // This is massively inefficient even still but the position is good to know
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                int pixelIndex = y * width + x;

                if (tilePixels[pixelIndex] == targetColor)
                {
                    totalX += x;
                    totalY += y;
                    matchingPixels++;
                    break;
                }
            }
        }
        // Get mean pixel position for tile center
        float meanX = Mathf.RoundToInt(totalX / matchingPixels);
        float meanY = Mathf.RoundToInt(totalY / matchingPixels);
        Vector2 meanPosition = new Vector2(meanX, meanY);

        // Get Province of the tile
        ProvinceData newParent = getProvinceParent(firstPosition);

        // Create a new tile object and set values
        TileData newTile = new TileData
        {
            HexCode = ColorUtility.ToHtmlStringRGB(targetColor),
            Position = meanPosition,
            FirstPosition = firstPosition,
            Area = newArea,
            ProvinceParent = newParent.HexCode,
            ContinentParent = newParent.ContinentParent,
        };

        // Chuck tile into right province
        newParent.Tiles.Add(newTile);
    }

    IEnumerator UpdateTileData()
    {
        int tileProgress = 0;

        foreach (ContinentData c in continents)
        {
            string conHex = c.HexCode;
            // Check for localisation
            string returnedName = RetrieveName(true, conHex);
            // If no value found add it in with a continent identifier to help find it, if found no need
            if (returnedName == "Undefined")
            {
                MapLocalisation localisation = new MapLocalisation { HexCode = conHex, Name = conHex + "-" };
                continentNames.Add(localisation);
            }
            foreach (ProvinceData p in c.Provinces)
            {
                string newHex = p.HexCode;
                // Check for localisation
                returnedName = RetrieveName(false, newHex);
                // If no value found add it in with a continent identifier to help find it, if found no need
                if (returnedName == "Undefined")
                {
                    MapLocalisation localisation = new MapLocalisation { HexCode = newHex, Name = newHex + "-" + conHex };
                    provinceNames.Add(localisation);
                }

                p.Population = 0;
                p.Area = 0;
                foreach (TileData t in p.Tiles)
                {
                    // Use first position as the data peg if the mean position is over water
                    Vector2 position = t.Position;
                    int baseX = (int)position.x;
                    int baseY = (int)position.y;

                    // If transparent(not land)
                    if (continentMap.GetPixel(baseX, baseY).a < 0.5f) 
                    {
                        position = t.FirstPosition;
                        baseX = (int)position.x;
                        baseY = (int)position.y;
                    }

                    // Correct the kerbin coordinates of the man, regardless of water
                    t.Coordinates = getCoordinates(t.Position);
                    // Add tile area to province area
                    p.Area += t.Area;
                    // Get Culture by finding the undertile value in a comparitive object
                    Color cultureValue = languageMap.GetPixel(baseX, baseY);
                    t.Culture = ColorUtility.ToHtmlStringRGB(cultureValue);
                    // Get altitude by converting the heightmap brightness
                    int colorValue = (int)(heightMap.GetPixel(baseX, baseY).r * 255);
                    t.Altitude = colorValue * 50f;
                    // Get Biome by comparing the biomemap with a dictionary
                    Color biomeValue = biomeMap.GetPixel(baseX, baseY);
                    t.Terrain = biomeCodeMappings[ColorUtility.ToHtmlStringRGB(biomeValue)];
                    // Get Population of the tile by scaling the true area against the heatmap
                    //Color heatValue = populationMap.GetPixel(meanX, meanY);
                    int newPopulation = (int)(30 * populationScaler * t.Area); //30 is a standin heatmap value for persons per km
                    p.Population += newPopulation;
                    t.Population = newPopulation;
                    // Claim Value is an aggregate of local values
                    int claimValue = 5;
                    t.ClaimValue = claimValue;

                    tileProgress++;
                    Debug.Log(t.HexCode + "Updated (#" + tileProgress + ")");
                    // Give a little buffer
                    yield return new WaitForSeconds(0.01f);
                }

            }
        }

        // write all to json
        WriteToJson(Application.dataPath + "/Maps/MapData/Tiles/");

        yield return null;
    }
    #endregion

    #region Find Objects
    public ProvinceData getProvinceParent(Vector2 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;

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
    
    private void OffsetMean(TileData t)
    {
        Vector2 pos = t.Position;
        // If tile exists on both edges it's a looper
        // If the tile goes beyond the map bounds reduce or add to it to compensate
        if (pos.x < 0) pos.x += width;
        if (pos.x >= width) pos.x -= width;
        // Set the tile position again
        t.Position = pos;
    }
    private TileData BruteFindTile(Color searchColour)
    {
        string searchTerm = ColorUtility.ToHtmlStringRGB(searchColour);
        foreach (ContinentData c in continents)
        {
            foreach (ProvinceData p in c.Provinces)
            {
                foreach (TileData t in p.Tiles)
                {
                    if (t.HexCode == searchTerm) 
                    {
                        return t;
                    }
                }
            }
        }
        Debug.Log("Tile " + searchTerm + " not found");
        return new TileData();
    }
    private List<Color> GetEdgeTiles(List<Color> tilePixels)
    {
        List<Color> leftEdge = tileMap.GetPixels(0, 0, 1, height).ToList();
        List<Color> rightEdge = tileMap.GetPixels(width-1, 0, 1, height).ToList();
        List<Color> bothEdges = new List<Color>();

        // Remove same column duplicates and transparent cells
        leftEdge = RemoveMapAlpha(UniqueMapColours(leftEdge));
        rightEdge = RemoveMapAlpha(UniqueMapColours(rightEdge));

        // Check for colours that appear in both columns
        foreach (Color color in leftEdge)
        {
            if (rightEdge.Contains(color))
            {
                bothEdges.Add(color);
            }
        }

        return bothEdges;
    }
    private Vector2 pixelSweep(List<Color> mapPixels, Color targetColor, int sweepArea, int sweepIncriment, float offset)
    {
        int startPosition = (int)(sweepIncriment * offset);

        // Get the index position of the first matching pixel
        for (int i = startPosition; i < sweepArea; i += sweepIncriment)
        {
            if (mapPixels[i] == targetColor)
            {
                int y = i / width;
                int x = i - (y * width);
                return new Vector2(x, y);
            }
        }

        return new Vector2(0, 0);
    }
    private Vector2 getCoordinates(Vector2 position)
    {
        float pixelX = position.x; 
        float pixelY = position.y;

        float longitude = (pixelX - centerX) / centerX * (180f);
        float latitude = (pixelY - centerY) / centerY * (90f);

        return new Vector2(longitude, latitude);
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
    List<CultureDef> ImportCultureJson(string filePath)
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
                return JsonHelper.FromJson<CultureDef>(content).ToList();
            }
        }
        // If file doesn't exist or is invalid return an empty list
        return new List<CultureDef>();
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

    #region json
    void ImportContinentsJson(string filePath)
    {
        string[] files = System.IO.Directory.GetFiles(filePath, $"*.json");
        string content;
        continents = new List<ContinentData>();


        foreach (string file in files)
        {
            Debug.Log("Loading " + file);
            if (File.Exists(file))
            {
                using (StreamReader reader = new StreamReader(file))
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
    #endregion

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
