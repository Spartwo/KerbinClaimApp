using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
    public List<ResourceDef> resources = new List<ResourceDef>();

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
        // Check for left mouse button click
        if (Input.GetMouseButtonDown(0)) 
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
        if (Input.GetMouseButtonDown(1))
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
                //UICanvas.updateClaimDisplay();
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
                //UICanvas.updateClaimDisplay();
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

    // Call this function to update the visible texture of the plane
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

    public void exportClaim()
    {
        PrintMap("Territory.png");
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


    #region Finders
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
