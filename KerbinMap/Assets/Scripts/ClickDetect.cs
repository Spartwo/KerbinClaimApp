using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine;
using System.IO;

using Color = UnityEngine.Color;
using ColorUtility = UnityEngine.ColorUtility;
using Debug = UnityEngine.Debug;
using Input = UnityEngine.Input;
using File = System.IO.File;

using System.Collections;
using System.Diagnostics;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

//this method handles click detection action and the stages involved in generating map data
public class ClickDetect : MonoBehaviour
{
    [SerializeField] private GameObject tileScanTarget;
    [SerializeField] private GameObject selectionDisplayPlane;

    public TileData selectedTile;
    public CultureDef selectedTileCulture;
    public ProvinceData selectedProvince;
    public string selectedProvName;
    public string selectedContName;

    private MapGen mapSource;
    private Texture2D tileMap;

    private List<CultureDef> culturesList;

    private Texture2D selectionTexture;
    private Texture2D claimingTexture;

    private List<Color> tilePixels= new List<Color>();
    int width;

    // Start is called before the first frame update
    void Start()
    {
        mapSource = GetComponent<MapGen>();
        tileMap = mapSource.tileMap;
        // Import Culture Definitions
        culturesList = mapSource.culturesList;

        // Gotta store these for painting
        tilePixels = mapSource.SerialiseMap(Resources.Load<Texture2D>("Maps/DataLayers/Tiles").GetPixels());
        width = tileMap.width;

        // Initialize the meanPositionTexture with the same dimensions as the map image
        selectionTexture = new Texture2D(width, tileMap.height, TextureFormat.ARGB32, false);
        claimingTexture = new Texture2D(width, tileMap.height, TextureFormat.ARGB32, false);

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
                int x = Mathf.RoundToInt(textureCoord.x * tileMap.width * 2);
                int y = Mathf.RoundToInt(textureCoord.y * tileMap.height);
                GameModeLC(x, y);
            }
        }

    }

    void GameModeLC (int x, int y)
    {
        int situation = 0;
        switch (situation)
        {
            case 0: //Inspect Mode
                if (selectedTile != null && selectedProvince != null) //Clear Old selection
                {
                    PaintProvince(selectionTexture, Color.clear);
                }
                SelectTile(x, y);
                selectedTileCulture = FindCulture(selectedTile.Culture);

                // if shift clicked then highlight all tiles in province
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    PaintProvince(selectionTexture, Color.blue);
                    // Get the localised name from the MapGen script
                    selectedProvName = mapSource.RetrieveName(false, selectedTile.ProvinceParent);
                    selectedContName = mapSource.RetrieveName(true, selectedTile.ContinentParent);
                }
                // Paint the main one after so it's differently coloured
                PaintTile(selectedTile, selectionTexture, Color.cyan);
                //selectionDisplayPlane.GetComponent<Renderer>().material.mainTexture = selectionTexture;


                Debug.Log("Printing Test Image");
                // Save the final texture to a file when the object is destroyed (you can adjust this to your needs)
                string filePath = Application.dataPath + "/Exports/Maps/Selection.png";
                byte[] pngBytes = selectionTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, pngBytes);
                break;
            default:
                //nothing
                break;
        }
    }

    void PaintTile(TileData tile, Texture2D targetTex, Color paintColor)
    {
        // Calculate the pixel position of the mean position
        int pixelX = Mathf.RoundToInt(tile.Position.x);
        int pixelY = Mathf.RoundToInt(tile.Position.y);
        int totalArea = tile.ProjectedArea;
        int foundArea = 1;

        int searchOffset = 1;
        Color targetColor = tileMap.GetPixel(pixelX, pixelY);

        targetTex.SetPixel(pixelX, pixelY, paintColor);
        targetTex.Apply();

        while (foundArea < totalArea)
        {
            int startX = pixelX - searchOffset;
            int endX = pixelX + searchOffset;
            int startY = pixelY - searchOffset;
            int endY = pixelY + searchOffset;

            // Check top and bottom edges
            for (int x = startX; x <= endX; x++)
            {
                if (tilePixels[startY * width + x] == targetColor)
                {
                    targetTex.SetPixel(x, startY, paintColor);
                    foundArea++;
                }
                if (tilePixels[endY * width + x] == targetColor)
                {
                    targetTex.SetPixel(x, endY, paintColor);
                    foundArea++;
                }
            }

            // Check left and right edges
            for (int y = startY + 1; y < endY; y++) // Start from startY + 1 to avoid rechecking the corners
            {
                if (tilePixels[y * width + startX] == targetColor)
                {
                    targetTex.SetPixel(startX, y, paintColor);
                    foundArea++;
                }
                if (tilePixels[y * width + endX] == targetColor)
                {
                    targetTex.SetPixel(endX, y, paintColor);
                    foundArea++;
                }
            }

            searchOffset++;
        }

        targetTex.Apply();

    }


    void PaintProvince(Texture2D targetTex, Color paintColor)
    {
        foreach (TileData t in selectedProvince.Tiles) 
        {
            PaintTile(t, targetTex, paintColor);
        }
    }

    void ClearProvince(Vector2 searchPosition, Texture2D targetTex)
    {

    }

    void ClearTile(Vector2 paintPosition, Texture2D targetTex)
    {

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
        textureColor = tileMap.GetPixel(x, y);
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
    ProvinceData FindProvince(string continentColour, string provinceColour)
    {
        //method searches an appropriate subdatabase for the right tile data, reduces search time
        // Find right continent
        foreach (ContinentData c in mapSource.continents)
        {
            if (c.HexCode != continentColour) continue;
            // Find right province inside continent
            foreach (ProvinceData p in c.Provinces)
            {
                if (p.HexCode == provinceColour)
                {
                    return p;
                }
                
            }
        }

        return null;
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
