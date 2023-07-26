using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Unity.Properties;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Color = UnityEngine.Color;
using ColorUtility = UnityEngine.ColorUtility;
using Debug = UnityEngine.Debug;
using Input = UnityEngine.Input;

//this method handles click detection action and the stages involved in generating map data
public class ClickDetect : MonoBehaviour
{
    [SerializeField] private GameObject tileScanTarget;
    [SerializeField] private Material selectedMaterial;

    public string selectedProvName;
    public string selectedContName;
    public ProvinceData selectedProvince;
    public TileData selectedTile;
    public CultureDef selectedTileCulture;

    private MapGen mapSource;

    private List<CultureDef> culturesList;

    private Texture2D highlightTexture;
    private Color clearColour;
    private Color[] clearColours;
    private Color[] highlightColours;

    // Claim Data
    private HashSet<string> selectedTiles = new HashSet<string>();
    public float totalArea = 0;
    public int totalPopulation = 0;
    public int claimValue = 0;
    public string claimName;
    public List<ResourceDef> resources = new List<ResourceDef>();

    [SerializeField] private int claimLimit = 100;

    [SerializeField] private UIControl UICanvas;

    private List<Color> tilePixels = new List<Color>();
    int width, height;

    // Start is called before the first frame update
    void Start()
    {
        mapSource = GetComponent<MapGen>();
        // Import Culture Definitions
        culturesList = mapSource.culturesList;

        // Gotta store these for painting
        tilePixels = mapSource.SerialiseMap(mapSource.tileMap.GetPixels());
        
        width = mapSource.tileMap.width; 
        height = mapSource.tileMap.height;

        // Initialize the hightlight Texture with the same dimensions as the map image
        highlightTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

        // Create a predefined empty image
        clearColour = new Color(0, 0, 0, 0);
        clearColours = new Color[width * height];
        for (int i = 0; i < clearColours.Length; i++)
        {
            clearColours[i] = clearColour;
        }
        // Copy the empty aray into the highlight array by default
        highlightColours = new Color[width * height];
        Array.Copy(clearColours, highlightColours, clearColours.Length);
        UpdatePlaneTexture();

    }

    void Update()
    {
        bool isOverUI = EventSystem.current.IsPointerOverGameObject();

        // Check for left mouse button click
        if (Input.GetMouseButtonDown(0) && !isOverUI) 
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Perform the raycast and check for collision with the tiles
            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == tileScanTarget)
            {
                //get the position where the ray hit
                Vector2 textureCoord = hit.textureCoord;
                // Convert texture coordinates to pixel coordinates
                int x = Mathf.RoundToInt(textureCoord.x * width * 2);
                int y = Mathf.RoundToInt(textureCoord.y * height);
                GameModeLC(x, y);
            }
        }

        // Check for right mouse button click
        if (Input.GetMouseButtonDown(1) && !isOverUI)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Perform the raycast and check for collision with the tiles
            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == tileScanTarget)
            {
                //get the position where the ray hit
                Vector2 textureCoord = hit.textureCoord;
                // Convert texture coordinates to pixel coordinates
                int x = Mathf.RoundToInt(textureCoord.x * width * 2);
                int y = Mathf.RoundToInt(textureCoord.y * height);
                GameModeRC(x, y);
            }
        }

    }

    void GameModeLC (int x, int y)
    {
        int situation = UICanvas.mapModeValue;
        switch (situation)
        {
            case 0: //Inspect Mode
                if (selectedTile != null) //Clear Old selection
                {
                    //Clear the whole map cause only one selection at a time
                    Array.Copy(clearColours, highlightColours, clearColours.Length);
                }
                SelectTile(x, y);
                // Retrieve the cultural index and get its full tree
                selectedTileCulture = FindCulture(selectedTile.Culture);
                // Get the localised name from the MapGen script
                selectedProvName = mapSource.RetrieveName(false, selectedTile.ProvinceParent);
                selectedContName = mapSource.RetrieveName(true, selectedTile.ContinentParent);

                // if shift clicked then highlight all tiles in province
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    PaintProvince(highlightTexture, Color.blue);
                }
                // Paint the main one after so it's differently coloured
                PaintTile(selectedTile, highlightTexture, Color.cyan);

                UpdatePlaneTexture();
                //PrintMap("Selection.png");
                break;
            case 1:
                SelectTile(x, y);
                // if shift clicked then add all tiles in province
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    foreach (TileData t in selectedProvince.Tiles)
                    {
                        if (!selectedTiles.Contains(t.HexCode))
                        {
                            TileClaim(t, true);
                        }
                    }
                } 
                else //else just do the one
                {
                    if (!selectedTiles.Contains(selectedTile.HexCode))
                    {
                        TileClaim(selectedTile, true);
                    }
                }
                UICanvas.UpdateClaimUI((float)claimValue / claimLimit, totalArea.ToString("N0") + "km^2\n\n" + totalPopulation + "\n\n" + "NaN");
                UpdatePlaneTexture();
                break;
            case 2:
                Debug.Log("Distance calc is a work in progress");
                break;
            default:
                //nothing
                break;
        }
    }
    void GameModeRC(int x, int y)
    {
        int situation = UICanvas.mapModeValue;
        switch (situation)
        {
            case 0: //Inspect Mode
                //Right click doesn't do anything
                break;
            case 1:
                SelectTile(x, y);
                // if shift clicked then add all tiles in province
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    foreach (TileData t in selectedProvince.Tiles)
                    {
                        if (selectedTiles.Contains(t.HexCode))
                        {
                            TileClaim(t, false);
                        }
                    }
                }
                else //else just do the one
                {
                    if (selectedTiles.Contains(selectedTile.HexCode))
                    {
                        TileClaim(selectedTile, false);
                    }
                }
                UICanvas.UpdateClaimUI((float)claimValue / claimLimit, totalArea.ToString("N0") + "km^2\n\n" + totalPopulation + "\n\n" + "NaN");
                UpdatePlaneTexture();
                break;
            case 2:
                Debug.Log("Distance calc is a work in progress");
                break;
            default:
                //nothing
                break;
        }
    }

    void TileClaim(TileData t, bool add)
    {
        if (add)
        {
            totalArea += t.Area;
            totalPopulation += t.Population;
            claimValue += t.ClaimValue;

            PaintTile(t, highlightTexture, Color.red);
            selectedTiles.Add(t.HexCode);
        }
        else
        {
            totalArea -= t.Area;
            totalPopulation -= t.Population;
            claimValue -= t.ClaimValue;

            PaintTile(t, highlightTexture, clearColour);
            selectedTiles.Remove(t.HexCode);
        }
    }

    public void clearSelection()
    {
        totalArea = 0;
        totalPopulation = 0;
        claimValue = 0;
        selectedTiles.Clear();
        resources.Clear();

        UICanvas.UpdateClaimUI(0, totalArea.ToString("N0") + "km^2\n\n" + totalPopulation + "\n\n" + "NaN");

        Array.Copy(clearColours, highlightColours, clearColours.Length);
        UpdatePlaneTexture();
    }
    void SaveClaimData()
    {
        // Create a list to store the lines of the text
        List<string> lines = new List<string>();

        // Add the claim data to the list
        lines.Add("{{MinorNation");
        lines.Add("| nation = " + claimName);
        lines.Add("| localized_name = " + "[Unknown]");
        lines.Add("| flag = " + claimName + "_flag.png");
        lines.Add("| flagdesc = ");
        lines.Add("| full_name = " + claimName);
        lines.Add("| map = " + claimName + "_Globe.png");
        lines.Add("| mapdesc = ");
        lines.Add("| government = " + "[Unknown]");
        //lines.Add("| foundation = " + GetDate(DateTime.UtcNow));
        lines.Add("| population = " + totalPopulation.ToString());
        lines.Add("| denonym = " + claimName + "ian");
        lines.Add("| area = " + totalArea.ToString("N0"));
        lines.Add("| predecessor = " + "[Unknown]");
        lines.Add("| successor = " + "");
        lines.Add("| capital = " + mapSource.RetrieveName(false, selectedTile.ProvinceParent));
        lines.Add("}}");

        // Add an empty line after the claim data
        lines.Add("");

        // Add the selectedTiles hashset to the list
        lines.Add("Selected Tiles: " + string.Join(", ", selectedTiles));

        // Save the lines to a file 
        string filePath = Application.dataPath + "/Exports/Maps/Claim.txt";
        File.WriteAllLines(filePath, lines);
    }

    public void exportClaim()
    {
        SaveClaimData();
        PrintMap("Territory.png");
    }

    #region Painters

    public void UpdatePlaneTexture()
    {
        // Set the map to the currently stored highlights array
        highlightTexture.SetPixels(0, 0, width, height, highlightColours);
        highlightTexture.Apply();

        // Update the visible plane material for the new highlight
        selectedMaterial.mainTexture = highlightTexture;
    }

    void PrintMap(string filename)
    {
        // Save the final texture to a file 
        string filePath = Application.dataPath + "/Exports/Maps/" + filename;
        byte[] pngBytes = highlightTexture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngBytes);
    }

    void PaintTile(TileData tile, Texture2D targetTex, Color paintColor)
    {
        // Calculate the pixel position of the mean position
        int pixelX = Mathf.RoundToInt(tile.Position.x);
        int pixelY = Mathf.RoundToInt(tile.Position.y);
        int totalArea = tile.ProjectedArea;

        int searchOffset = 1;
        Color targetColor = tilePixels[pixelY * width + pixelX];

        // Set the mean painted
        highlightColours[pixelY * width + pixelX] = paintColor;
        int foundArea = 1;

        while (foundArea < totalArea)
        {
            int startX = pixelX - searchOffset;
            int endX = pixelX + searchOffset;
            int startY = pixelY - searchOffset;
            int endY = pixelY + searchOffset;

            // Limit startY and endY to be within the texture's height boundaries
            // The heigh extremes are all icecap so won't flag as duplicates
            startY = Mathf.Clamp(startY, 0, height - 1);
            endY = Mathf.Clamp(endY, 0, height - 1);

            // Check top and bottom edges
            for (int x = startX; x <= endX; x++)
            {
                int topIndex = startY * width + x;
                int bottomIndex = endY * width + x;
                if (tilePixels[topIndex] == targetColor)
                {
                    highlightColours[topIndex] = paintColor;
                    foundArea++;
                }
                if (tilePixels[bottomIndex] == targetColor)
                {
                    highlightColours[bottomIndex] = paintColor;
                    foundArea++;
                }
            }

            // Check left and right edges
            for (int y = startY + 1; y < endY; y++) // Start from startY + 1 to avoid rechecking the corners
            {
                int leftIndex = y * width + startX;
                int rightIndex = y * width + endX;
                if (tilePixels[leftIndex] == targetColor)
                {
                    highlightColours[leftIndex] = paintColor;
                    foundArea++;
                }
                if (tilePixels[rightIndex] == targetColor)
                {
                    highlightColours[rightIndex] = paintColor;
                    foundArea++;
                }
            }

            searchOffset++;
        }
    }

    void PaintProvince(Texture2D targetTex, Color paintColor)
    {
        foreach (TileData t in selectedProvince.Tiles) 
        {
            PaintTile(t, targetTex, paintColor);
        }
    }
    #endregion

    #region Finders

    public DateTime GetDate(DateTime inputDate)
    { 
        // Define the starting date for the sped-up calendar
        DateTime startingDate = new DateTime(2200, 1, 1);

        // Calculate the difference in days between the inputDate and the startingDate
        TimeSpan timeDifference = inputDate - startingDate;
        int daysElapsed = (int)timeDifference.TotalDays;

        // Calculate the year and day in the sped-up calendar
        int spedUpYear = daysElapsed / 14;
        int spedUpDay = daysElapsed % 14;

        // Create the sped-up calendar date
        DateTime spedUpDate = startingDate.AddDays(daysElapsed);

        // Set the year and day in the sped-up calendar
        spedUpDate = spedUpDate.AddYears(spedUpYear);
        spedUpDate = spedUpDate.AddDays(spedUpDay);

        return inputDate;
    }

    void SelectTile(int x, int y)
    {
        // Get the hexcode of the continent pixel
        Color textureColor = mapSource.continentMap.GetPixel(x, y);
        string continentColour = ColorUtility.ToHtmlStringRGB(textureColor);
        // Get the hexcode of the province pixel
        textureColor = mapSource.provinceMap.GetPixel(x, y);
        string provinceColour = ColorUtility.ToHtmlStringRGB(textureColor);
        // Get the hexcode of the tile pixel
        textureColor = tilePixels[y * width + x];
        string tileColour = ColorUtility.ToHtmlStringRGB(textureColor);

        FindTile(provinceColour, tileColour);
    }

    void FindTile(string provinceColour, string tileColour)
    {
        //method searches an appropriate subdatabase for the right tile data, reduces search time
        // Find right continent
        foreach (ContinentData c in mapSource.continents)
        {
            //if (c.HexCode != continentColour) continue;
            // Find right province inside continent
            foreach (ProvinceData p in c.Provinces)
            {
                if (p.HexCode != provinceColour) continue;
                // Find the tile
                foreach (TileData t in p.Tiles)
                {
                    if (t.HexCode == tileColour)
                    { 
                        selectedTile = t;
                        selectedProvince = p;
                        return;
                    }
                }
            }
        }

        Debug.Log("Tile " + tileColour + " not Found");
    }

    private CultureDef FindCulture(string hexCode)
    {
        foreach (CultureDef c in culturesList)
        {
            if (c.HexCode == hexCode) return c;
        }
        //If nothing return undefined values
        return new CultureDef
        {
            HexCode = hexCode,
            Dialect = "Undefined",
            Language = "Undefined",
            Group = "Undefined",
            SubGroup = "Undefined",
            Family = "Undefined",
        };
    }
    #endregion

}
